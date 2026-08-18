using System.IO;
using System.Net.Http;
using System.Windows;
using System.Windows.Threading;
using Bridge.Converters;
using Bridge.Core.Contracts;
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

            // Fase 1 MVP: EnsureCreated, not real migrations — see DEVELOPMENT.md
            // "Bridge.Storage — what's in it" for why, and when to switch.
            var dbContext = Services.GetRequiredService<BridgeDbContext>();
            dbContext.Database.EnsureCreated();

            // Mini-migration: EnsureCreated won't alter an existing database, so
            // add columns added after the initial schema (DescriptionImages, then
            // DescriptionBlocks) if a pre-existing DB is missing them. Raw text
            // columns defaulting to an empty JSON list — JsonValueConverter reads
            // those as empty lists. Each step is individually guarded so a DB
            // corruption issue can't crash startup (the app still runs; the
            // missing column just stays empty).
            EnsureColumn(dbContext, "DescriptionImages");
            EnsureColumn(dbContext, "DescriptionBlocks");
            EnsureColumn(dbContext, "Screenshots");

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

                // Decode the library's artwork (from the disk cache when
                // available) BEFORE the first paint, so the Grid's covers and
                // the selected game's background are already loaded when the
                // window shows instead of rendering black and popping in a
                // second later. Bounded by a timeout inside the VM.
                viewModel.WaitForStartupArtworkAsync().GetAwaiter().GetResult();

                mainWindow.Show();

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
        }

        private static void ConfigureServices(IServiceCollection services)
        {
            var dbOptions = new DbContextOptionsBuilder<BridgeDbContext>()
                .UseSqlite($"Data Source={Config.DatabasePath}")
                .Options;

            // Singleton, not Scoped: this is a desktop app with one long-lived
            // session, not a web app handling independent requests. Matches
            // DEVELOPMENT.md's Lifetime Guidelines ("Scoped — Rarely used in WPF").
            services.AddSingleton(new BridgeDbContext(dbOptions));
            services.AddSingleton(typeof(IRepository<>), typeof(Repository<>));
            services.AddSingleton<IGameRepository, GameRepository>();

            // Singleton: it's a stateless launcher, and MainViewModel needs to
            // subscribe to its events once for the lifetime of the app.
            services.AddSingleton<GameLauncher>();
            services.AddSingleton<RomScanner>();
            services.AddSingleton<RetroArchService>();
            services.AddSingleton<SteamLibraryImporter>();
            services.AddSingleton<EpicLibraryImporter>();
            services.AddSingleton<WebImageSearchService>();
            services.AddSingleton<InstalledGameDetector>();
            services.AddSingleton<AppUpdateService>();

            // IGDB: settings loaded from disk once at startup (see
            // IgdbSettingsStore — separate JSON file, not bridge.db), then
            // shared as a singleton so IgdbSettingsWindow's edits are visible
            // to MainViewModel without extra plumbing. One shared HttpClient
            // per .NET guidance (don't new one up per request).
            services.AddSingleton(IgdbSettingsStore.Load());
            services.AddSingleton(sp =>
            {
                var client = new HttpClient
                {
                    Timeout = TimeSpan.FromSeconds(Config.RequestTimeoutSeconds)
                };
                return client;
            });

            // Bridge's own IGDB proxy (Cloudflare Worker): the IGDB/Twitch
            // credentials live as Worker Secrets server-side, so Bridge gets
            // IGDB metadata with zero user configuration — the same architecture
            // Playnite uses, but our own infra. First in the chain.
            services.AddSingleton<BridgeIgdbProvider>();
            services.AddSingleton<IGameMetadataProvider>(sp => sp.GetRequiredService<BridgeIgdbProvider>());

            // Fallback: Playnite's public IGDB proxy (same zero-config behavior)
            // in case our own Worker is unreachable.
            services.AddSingleton<PlayniteIgdbProvider>();
            services.AddSingleton<IGameMetadataProvider>(sp => sp.GetRequiredService<PlayniteIgdbProvider>());

            // User-configured IGDB (optional): only used if both proxies fail
            // or return nothing.
            services.AddSingleton<IgdbAuthClient>();
            services.AddSingleton<IgdbMetadataProvider>();
            services.AddSingleton<IGameMetadataProvider>(sp => sp.GetRequiredService<IgdbMetadataProvider>());

            // Steam Store metadata: 100% HTTP, anonymous, no API key needed.
            // Registered as both concrete (for appid-specific lookups on
            // Steam-imported games) and via IGameMetadataProvider for the
            // multi-provider fallback chain in MainViewModel.
            services.AddSingleton<SteamMetadataProvider>();
            services.AddSingleton<IGameMetadataProvider>(sp => sp.GetRequiredService<SteamMetadataProvider>());

            // Transient, per the same Lifetime Guidelines ("Transient — ViewModels").
            services.AddTransient<MainViewModel>();
            services.AddTransient<EmulatorSetupViewModel>();
            services.AddTransient<EmulationSettingsViewModel>();
            services.AddTransient<IgdbSettingsViewModel>();
        }

        private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            LogException(e.Exception);
            MessageBox.Show(e.Exception.Message, Config.AppName, MessageBoxButton.OK, MessageBoxImage.Error);
            e.Handled = true;

            // A startup exception (before MainWindow exists) leaves no window to
            // keep the dispatcher alive — ShutdownMode.OnLastWindowClose would
            // strand an invisible process. Exit cleanly instead.
            if (Application.Current.MainWindow is null)
            {
                // If the very first launch after an update failed (e.g. a corrupt
                // DB threw inside EnsureCreated, before OnStartup's try block),
                // restore the previous exe so the user isn't stuck on a broken
                // build.
                AppUpdateService.RollbackToPrevious();
                Shutdown();
            }
        }

        // Adds a column to Games when it's missing, logging (not throwing) on any
        // failure so a schema problem never blocks startup. column is validated
        // against a compile-time whitelist before being interpolated into SQL.
        private static void EnsureColumn(BridgeDbContext dbContext, string column)
        {
            if (column is not ("DescriptionImages" or "DescriptionBlocks" or "Screenshots"))
            {
                LogException(new InvalidOperationException($"Unknown migration column: {column}"));
                return;
            }

            // EF1002: interpolated SQL is normally an injection risk, but column
            // is whitelisted above (two compile-time constants), never user input.
#pragma warning disable EF1002
            try
            {
                dbContext.Database.ExecuteSqlRaw($"SELECT {column} FROM Games LIMIT 1");
            }
            catch
            {
                try
                {
                    dbContext.Database.ExecuteSqlRaw($"ALTER TABLE Games ADD COLUMN {column} TEXT NOT NULL DEFAULT '[]'");
                }
                catch (Exception ex)
                {
                    LogException(ex);
                }
            }
#pragma warning restore EF1002
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
    }
}
