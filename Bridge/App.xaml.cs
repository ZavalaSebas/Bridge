using System.IO;
using System.Net.Http;
using System.Windows;
using System.Windows.Threading;
using Bridge.Converters;
using Bridge.Core.Contracts;
using Bridge.Core.Entities;
using Bridge.Emulation;
using Bridge.Import.Epic;
using Bridge.Import.Steam;
using Bridge.Metadata;
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

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            DispatcherUnhandledException += OnDispatcherUnhandledException;

            // The UI thread's task scheduler, captured once so RemoteImageCache's
            // decode continuations always marshal callbacks back to the UI thread
            // (setting an HTTP UriSource on a pool thread never completes).
            RemoteImageCache.UiScheduler = TaskScheduler.FromCurrentSynchronizationContext();

            Directory.CreateDirectory(Config.AppDataPath);
            AppUpdateService.HandleUpdateHandshake();

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
                mainWindow.Show();

                // Warm selected-game and first-grid artwork without blocking the UI
                // thread — covers may pop in briefly, but startup stays responsive.
                _ = viewModel.WaitForStartupArtworkAsync();

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
            // keeps our own SystemAccentColor* (#007ACC) instead of letting the
            // OS accent overwrite it. Must run after MainWindow is set so
            // UiApplication.Current.MainWindow resolves for the backdrop.
            //
            // Fase 2 perf experiment (reverted): deferring this to
            // mainWindow.Loaded did NOT improve cold start (2590-2836ms vs
            // 2467-2510ms measured synchronously, same method) — reverted to
            // the synchronous call. Baseline delta vs pre-UI-overhaul (~2s /
            // ~140MB) is documented for Fase 6.
            Wpf.Ui.Appearance.ApplicationThemeManager.Apply(
                Wpf.Ui.Appearance.ApplicationTheme.Dark,
                Wpf.Ui.Controls.WindowBackdropType.Mica,
                updateAccent: false);

            Exit += (_, _) =>
            {
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
            services.AddSingleton<RomScanner>();
            services.AddSingleton<SteamLibraryImporter>();
            services.AddSingleton<EpicLibraryImporter>();
            services.AddSingleton<InstalledGameDetector>();
            services.AddSingleton<MetadataSyncService>();

            // IGDB: settings loaded from disk once at startup (see
            // IgdbSettingsStore — separate JSON file, not bridge.db), then
            // shared as a singleton so IgdbSettingsWindow's edits are visible
            // to MainViewModel without extra plumbing. Metadata and download
            // HttpClients are separate so long RetroArch downloads never share
            // timeout state with quick metadata/API calls.
            services.AddSingleton(IgdbSettingsStore.Load());
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

            // Transient, per the same Lifetime Guidelines ("Transient — ViewModels").
            services.AddTransient<MainViewModel>();
            services.AddTransient<GameEditViewModelFactory>();
            services.AddTransient<EmulatorSetupViewModel>();
            services.AddTransient<EmulationSettingsViewModel>();
            services.AddTransient<IgdbSettingsViewModel>();
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
        internal static void LogException(Exception exception)
        {
            try
            {
                var logDirectory = Path.Combine(Config.AppDataPath, "logs");
                Directory.CreateDirectory(logDirectory);
                File.AppendAllText(
                    Path.Combine(logDirectory, "errors.log"),
                    $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {exception}\r\n\r\n");
            }
            catch
            {
                // Logging must never take the app down.
            }
        }

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
