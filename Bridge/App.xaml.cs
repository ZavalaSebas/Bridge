using System.IO;
using System.Net.Http;
using System.Windows;
using System.Windows.Threading;
using Bridge.Core.Contracts;
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

            Directory.CreateDirectory(Config.AppDataPath);

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
            // those as empty lists.
            try
            {
                dbContext.Database.ExecuteSqlRaw("SELECT DescriptionImages FROM Games LIMIT 1");
            }
            catch
            {
                dbContext.Database.ExecuteSqlRaw("ALTER TABLE Games ADD COLUMN DescriptionImages TEXT NOT NULL DEFAULT '[]'");
            }

            try
            {
                dbContext.Database.ExecuteSqlRaw("SELECT DescriptionBlocks FROM Games LIMIT 1");
            }
            catch
            {
                dbContext.Database.ExecuteSqlRaw("ALTER TABLE Games ADD COLUMN DescriptionBlocks TEXT NOT NULL DEFAULT '[]'");
            }

            // View-ViewModel wiring per DEVELOPMENT.md's MVVM section: build the
            // ViewModel via DI, assign it as the View's DataContext, then show it.
            // No StartupUri in App.xaml — this is the one and only place MainWindow
            // gets created. The saved theme accent is applied before the window
            // loads so the first render already uses it.
            ThemeManager.Load();

            var mainWindow = new MainWindow
            {
                DataContext = Services.GetRequiredService<MainViewModel>()
            };
            MainWindow = mainWindow;
            mainWindow.Show();

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
            services.AddSingleton<SteamLibraryImporter>();

            // IGDB: settings loaded from disk once at startup (see
            // IgdbSettingsStore — separate JSON file, not bridge.db), then
            // shared as a singleton so IgdbSettingsWindow's edits are visible
            // to MainViewModel without extra plumbing. One shared HttpClient
            // per .NET guidance (don't new one up per request).
            services.AddSingleton(IgdbSettingsStore.Load());
            services.AddSingleton<HttpClient>();
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
            services.AddTransient<IgdbSettingsViewModel>();
        }

        private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            LogException(e.Exception);
            MessageBox.Show(e.Exception.Message, Config.AppName, MessageBoxButton.OK, MessageBoxImage.Error);
            e.Handled = true;
        }

        // Errors from the fire-and-forget tasks (_ = ...) never reach the
        // dispatcher, but every UI-thread exception does — log it so bugs aren't
        // silently swallowed by the MessageBox-and-continue handler above.
        private static void LogException(Exception exception)
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
