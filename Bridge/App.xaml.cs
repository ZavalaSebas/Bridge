using System.IO;
using System.Net.Http;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Bridge.Converters;
using Bridge.Core.Contracts;
using Bridge.Core.Entities;
using Bridge.Emulation;
using Bridge.Emulation.Dat;
using Bridge.Import.Epic;
using Bridge.Import.Steam;
using Bridge.Metadata;
using Bridge.Resources;
using Bridge.Services;
using Bridge.Settings;
using Bridge.Storage;
using Bridge.Storage.Repositories;
using Bridge.ViewModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Wpf.Ui.Controls;

namespace Bridge
{
    public partial class App : Application
    {
        public static IServiceProvider Services { get; private set; } = null!;

        internal static TrayIconService TrayIcon { get; } = new();

        private static readonly Uri AppIconUri = new("pack://application:,,,/Assets/Bridge.ico", UriKind.Absolute);

        internal static void ApplyWindowIcon(Window window)
        {
            if (window.Icon is null)
                window.Icon = BitmapFrame.Create(AppIconUri);
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            if (!ApplicationSingleInstance.TryBecomeOwner())
            {
                Shutdown();
                return;
            }

            LanguageSettingsStore.ApplySavedLanguage();
            WindowsStartupRegistration.ApplySavedPreference();

            EventManager.RegisterClassHandler(
                typeof(Window),
                FrameworkElement.LoadedEvent,
                new RoutedEventHandler(static (sender, _) =>
                {
                    if (sender is Window window)
                        ApplyWindowIcon(window);
                }),
                true);

            DispatcherUnhandledException += OnDispatcherUnhandledException;

            // Background/task exceptions never reach DispatcherUnhandledException,
            // so before this they left no trace at all. Log them (best-effort)
            // without changing process lifetime: the non-UI handler can't mark
            // anything handled, and the task handler deliberately doesn't call
            // SetObserved, so runtime behavior stays exactly as before.
            AppDomain.CurrentDomain.UnhandledException += static (_, args) =>
            {
                if (args.ExceptionObject is Exception ex)
                    AppLog.Error("Unhandled non-UI exception.", ex);
            };
            TaskScheduler.UnobservedTaskException += static (_, args) =>
                AppLog.Error("Unobserved task exception.", args.Exception);

            var splash = new SplashWindow();
            splash.Show();
            splash.PumpFrame();

            try
            {
                RunStartup(splash);
            }
            finally
            {
                splash.Close();
            }
        }

        private void RunStartup(SplashWindow splash)
        {
            // The UI thread's task scheduler, captured once so RemoteImageCache's
            // decode continuations always marshal callbacks back to the UI thread
            // (setting an HTTP UriSource on a pool thread never completes).
            RemoteImageCache.UiScheduler = TaskScheduler.FromCurrentSynchronizationContext();

            Directory.CreateDirectory(Config.AppDataPath);
            AppUpdateService.HandleUpdateHandshake();
            AppDataBackupService.ApplyPendingRestore();

            // Numbered file/folder migrations under AppData (settings layout, legacy
            // paths). See AppDataMigrator — runs before bridge.db EF migrations.
            AppDataMigrator.MigrateToLatest();

            var databaseRecovery = BridgeDatabaseRecovery.TryRestoreFromUpdateBackup();
            if (databaseRecovery == BridgeDatabaseRecovery.RecoveryResult.FileLocked)
            {
                MessageDialogWindow.Show(
                    Strings.DatabaseFileLocked,
                    Config.AppName,
                    SymbolRegular.ErrorCircle24);
                Shutdown();
                return;
            }

            if (databaseRecovery == BridgeDatabaseRecovery.RecoveryResult.BackupUnavailable &&
                File.Exists(Config.DatabasePath) &&
                !BridgeDatabaseRecovery.IsValidSqliteFile(Config.DatabasePath))
            {
                MessageDialogWindow.Show(
                    Strings.Format(nameof(Strings.DatabaseCorruptNoBackupFormat), Config.AppDataPath),
                    Config.AppName,
                    SymbolRegular.ErrorCircle24);
                Shutdown();
                return;
            }

            var services = new ServiceCollection();
            ConfigureServices(services);
            Services = services.BuildServiceProvider();

            // Real EF migrations (see Bridge.Storage — Migrations/ and
            // BridgeDbMigrator). A fresh DB gets InitialCreate applied by
            // Migrate(); a DB created by the pre-migrations era (EnsureCreated,
            // no __EFMigrationsHistory) is baselined first so its existing
            // schema is treated as the initial migration, then only future
            // migrations apply on top.
            var factory = Services.GetRequiredService<IDbContextFactory<BridgeDbContext>>();
            using var ctx = factory.CreateDbContext();
            ctx.MigrateToLatest();

            // View-ViewModel wiring per DEVELOPMENT.md's MVVM section: build the
            // ViewModel via DI, assign it as the View's DataContext, then show it.
            // No StartupUri in App.xaml — this is the one and only place MainWindow
            // gets created. The saved theme accent is applied before the window
            // loads so the first render already uses it.
            ThemeManager.Load();

            // If anything here throws (corrupt DB making EnsureCreated/VM fail,
            // broken XAML), the dispatcher handler below swallows it and the app
            // would linger invisible. Shut down instead — a usable error beats a
            // zombie process.
            try
            {
                var viewModel = Services.GetRequiredService<MainViewModel>();
                var mainWindow = new MainWindow
                {
                    DataContext = viewModel
                };
                MainWindow = mainWindow;
                // Paint the selected game's hero from the disk cache before Show so the
                // window opens with it already up (no black flash). Disk/local only —
                // returns immediately if it would need a download.
                viewModel.WarmSelectedHeroFromDisk();
                // Start warming images while the splash is still up (non-blocking).
                _ = viewModel.WaitForStartupArtworkAsync();
                splash.Close();
                mainWindow.Show();
                TrayIcon.Attach(mainWindow);
                ApplicationSingleInstance.ListenForShowWindowRequests(TrayIcon.ShowMainWindow);

                if (databaseRecovery == BridgeDatabaseRecovery.RecoveryResult.RestoredFromUpdateBackup)
                {
                    MessageDialogWindow.Show(
                        Strings.DatabaseRestoredFromBackup,
                        Config.AppName,
                        SymbolRegular.Info24);
                }

                // The new exe has now proven it starts (the window is up), so a
                // pending update's rollback copy (.old) and handshake marker can
                // be cleared. Only reached when nothing above threw.
                AppUpdateService.ConfirmUpdateApplied();
            }
            catch (Exception ex)
            {
                LogException(ex);

                // If this is the first launch after an update and it failed to
                // start (bad XAML, DB/DI failure), restore the previous exe and
                // relaunch it so the user isn't stuck with a broken build.
                if (AppUpdateService.RollbackToPrevious())
                {
                    Shutdown();
                    return;
                }

                Shutdown();
            }

            // Fase 1 (UI overhaul): apply the Wpf.Ui Dark theme and window
            // backdrop. Mica on Win11 (WindowBackdropType.Mica); on Win10 the
            // library's ApplyBackdrop compatibility check falls back to the
            // solid window background (set on FluentWindow). updateAccent:false
            // keeps our own SystemAccentColor* (default amber) instead of letting the
            // OS accent overwrite it. Must run after MainWindow is set so
            // UiApplication.Current.MainWindow resolves for the backdrop.
            Wpf.Ui.Appearance.ApplicationThemeManager.Apply(
                Wpf.Ui.Appearance.ApplicationTheme.Dark,
                TranslucentBackgroundSettingsStore.Load()
                    ? Wpf.Ui.Controls.WindowBackdropType.Mica
                    : Wpf.Ui.Controls.WindowBackdropType.None,
                updateAccent: false);

            Exit += (_, _) =>
            {
                ApplicationSingleInstance.Dispose();
                TrayIcon.Dispose();
                if (Services.GetService<WatchedScanFolderService>() is IDisposable watchedScanFolders)
                    watchedScanFolders.Dispose();
                if (Services.GetService<MetadataHttpClient>() is IDisposable metadataClient)
                    metadataClient.Dispose();
                if (Services.GetService<DownloadHttpClient>() is IDisposable downloadClient)
                    downloadClient.Dispose();
                if (Services is IDisposable disposable)
                    disposable.Dispose();
            };
        }

        private static void ConfigureServices(IServiceCollection services)
        {
            // IDbContextFactory: repositories create a short-lived context per
            // operation so concurrent background work (metadata sync, imports)
            // doesn't share one non-thread-safe DbContext.
            services.AddDbContextFactory<BridgeDbContext>(options =>
                options.UseSqlite($"Data Source={Config.DatabasePath}"));
            services.AddSingleton(typeof(IRepository<>), typeof(Repository<>));
            services.AddSingleton<IGameRepository, GameRepository>();

            // Singleton: it's a stateless launcher, and MainViewModel needs to
            // subscribe to its events once for the lifetime of the app.
            services.AddSingleton<GameLauncher>();
            services.AddSingleton<IDialogService, DialogService>();
            services.AddSingleton(sp => new RomDatStore(sp.GetRequiredService<DownloadHttpClient>().Client));
            services.AddSingleton<RomDatMatcher>();
            services.AddSingleton<RomScanner>();
            services.AddSingleton<SteamLibraryImporter>();
            services.AddSingleton<EpicLibraryImporter>();
            services.AddSingleton<InstalledGameDetector>();
            services.AddSingleton<InstalledGameImportService>();
            services.AddSingleton<WatchedScanFolderService>();
            services.AddSingleton<MetadataSyncService>();
            services.AddSingleton(sp => new HowLongToBeatClient(sp.GetRequiredService<MetadataHttpClient>().Client));
            services.AddSingleton(sp => new SteamGlobalAchievementStatsClient(sp.GetRequiredService<MetadataHttpClient>().Client));
            services.AddSingleton(sp => new SteamCommunityAchievementsClient(sp.GetRequiredService<MetadataHttpClient>().Client));
            services.AddSingleton(sp => new EpicAuthClient(sp.GetRequiredService<MetadataHttpClient>().Client));
            services.AddSingleton(sp => new EpicAchievementsClient(sp.GetRequiredService<MetadataHttpClient>().Client));
            services.AddSingleton<HowLongToBeatService>();
            services.AddSingleton<SteamAchievementsService>();
            services.AddSingleton<EpicAchievementsService>();
            services.AddSingleton(sp => new RetroAchievementsClient(sp.GetRequiredService<MetadataHttpClient>().Client));
            services.AddSingleton<RetroAchievementsHashIndex>();
            services.AddSingleton<RetroAchievementsAchievementsService>();
            services.AddSingleton<GameAchievementsService>();

            // IGDB: settings loaded from disk once at startup (see
            // IgdbSettingsStore — separate JSON file, not bridge.db), then
            // shared as a singleton so IgdbSettingsWindow's edits are visible
            // to MainViewModel without extra plumbing. Metadata and download
            // HttpClients are separate so long RetroArch downloads never share
            // timeout state with quick metadata/API calls.
            services.AddSingleton(IgdbSettingsStore.Load());
            services.AddSingleton(RetroAchievementsSettingsStore.Load());
            services.AddSingleton(SteamGridDbSettingsStore.Load());
            services.AddSingleton<MetadataHttpClient>();
            services.AddSingleton<DownloadHttpClient>();

            services.AddSingleton(sp => new BridgeIgdbProvider(sp.GetRequiredService<MetadataHttpClient>().Client));
            services.AddSingleton<IGameMetadataProvider>(sp => sp.GetRequiredService<BridgeIgdbProvider>());

            services.AddSingleton(sp => new PlayniteIgdbProvider(sp.GetRequiredService<MetadataHttpClient>().Client));
            services.AddSingleton<IGameMetadataProvider>(sp => sp.GetRequiredService<PlayniteIgdbProvider>());

            services.AddSingleton(sp => new IgdbAuthClient(
                sp.GetRequiredService<MetadataHttpClient>().Client,
                sp.GetRequiredService<IgdbSettings>()));
            services.AddSingleton(sp => new IgdbMetadataProvider(
                sp.GetRequiredService<MetadataHttpClient>().Client,
                sp.GetRequiredService<IgdbSettings>(),
                sp.GetRequiredService<IgdbAuthClient>()));
            services.AddSingleton<IGameMetadataProvider>(sp => sp.GetRequiredService<IgdbMetadataProvider>());

            services.AddSingleton(sp => new SteamMetadataProvider(sp.GetRequiredService<MetadataHttpClient>().Client));
            services.AddSingleton<IGameMetadataProvider>(sp => sp.GetRequiredService<SteamMetadataProvider>());

            services.AddSingleton(sp => new WebImageSearchService(sp.GetRequiredService<MetadataHttpClient>().Client));
            services.AddSingleton(sp => new SteamGridDbClient(
                sp.GetRequiredService<MetadataHttpClient>().Client,
                sp.GetRequiredService<SteamGridDbSettings>()));
            services.AddSingleton(sp => new FreeGamesService(sp.GetRequiredService<MetadataHttpClient>().Client));
            services.AddSingleton(sp => new AppUpdateService(sp.GetRequiredService<MetadataHttpClient>().Client));
            services.AddSingleton(new EmulationPaths(
                Config.EmulatorInstallPath,
                Config.EmulatorDownloadPath,
                Config.RetroArchVersionPath));
            services.AddSingleton(sp => new RetroArchService(
                sp.GetRequiredService<IRepository<Emulator>>(),
                sp.GetRequiredService<IRepository<Platform>>(),
                sp.GetRequiredService<DownloadHttpClient>().Client,
                sp.GetRequiredService<EmulationPaths>()));
            services.AddSingleton(sp => new RetroArchCheatService(
                sp.GetRequiredService<DownloadHttpClient>().Client,
                Config.CheatsPath));
            services.AddSingleton<RetroArchCheevosService>();
            services.AddSingleton<CheatsWindowOpener>();
            services.AddSingleton<GameEditWindowOpener>();

            // Transient, per the same Lifetime Guidelines ("Transient — ViewModels").
            services.AddTransient<MainViewModel>();
            services.AddTransient<GameEditViewModelFactory>();
            services.AddTransient<EmulatorSetupViewModel>();
            services.AddTransient<EmulationSettingsViewModel>();
            services.AddTransient<CheatsViewModel>();
            services.AddTransient<IgdbSettingsViewModel>();
            services.AddTransient<RetroAchievementsSettingsViewModel>();
            services.AddTransient<SteamGridDbSettingsViewModel>();
        }

        private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            LogException(e.Exception);

            if (IsFatalStartupException(e.Exception))
            {
                AppUpdateService.RollbackToPrevious();
                Shutdown();
                return;
            }

            MessageDialogWindow.Show(e.Exception.Message, Config.AppName, SymbolRegular.ErrorCircle24);
            e.Handled = true;

            // A startup exception (before MainWindow exists) leaves no window to
            // keep the dispatcher alive — ShutdownMode.OnLastWindowClose would
            // strand an invisible process. Exit cleanly instead.
            if (Application.Current.MainWindow is null)
            {
                // If the very first launch after an update failed (e.g. a corrupt
                // DB threw inside MigrateToLatest, before OnStartup's try block),
                // restore the previous exe so the user isn't stuck on a broken
                // build.
                AppUpdateService.RollbackToPrevious();
                Shutdown();
            }
        }

        // Errors from the fire-and-forget tasks (_ = ...) never reach the
        // dispatcher, but every UI-thread exception does - log it so bugs aren't
        // silently swallowed by the MessageBox-and-continue handler above.
        internal static void LogException(Exception exception) =>
            AppLog.Error("Unhandled UI-thread exception.", exception);

        private static bool IsFatalStartupException(Exception exception)
        {
            for (var ex = exception; ex is not null; ex = ex.InnerException)
            {
                if (ex is DbUpdateException && Current.MainWindow is null)
                    return true;
            }

            return false;
        }
    }
}
