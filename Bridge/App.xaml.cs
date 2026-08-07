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
            Services.GetRequiredService<BridgeDbContext>().Database.EnsureCreated();

            // View-ViewModel wiring per DEVELOPMENT.md's MVVM section: build the
            // ViewModel via DI, assign it as the View's DataContext, then show it.
            // No StartupUri in App.xaml — this is the one and only place MainWindow
            // gets created.
            var mainWindow = new MainWindow
            {
                DataContext = Services.GetRequiredService<MainViewModel>()
            };
            MainWindow = mainWindow;
            mainWindow.Show();
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
            MessageBox.Show(e.Exception.Message, Config.AppName, MessageBoxButton.OK, MessageBoxImage.Error);
            e.Handled = true;
        }
    }
}
