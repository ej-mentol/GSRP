using GSRP.Services;
using GSRP.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Windows;
using System.Threading; // Still needed for Mutex in SingleInstanceService

namespace GSRP
{
    public partial class App : Application
    {
        public static IHost? AppHost { get; private set; }

        public App()
        {
            AppHost = Host.CreateDefaultBuilder()
                .ConfigureServices((hostContext, services) =>
                {
                    services.AddSingleton<MainWindow>();
                    services.AddSingleton<MainViewModel>();
                    services.AddSingleton<IPathProvider, PathProvider>();
                    services.AddSingleton<ISettingsService, SettingsService>();
                    services.AddSingleton<IDatabaseService, DatabaseService>();
                    services.AddSingleton<IIconService, IconService>();
                    services.AddSingleton<IPlayerListParser, PlayerListParser>();
                    services.AddSingleton<IPlayerRepository, PlayerRepository>();
                    services.AddSingleton<IClipboardService, ClipboardService>();
                    services.AddSingleton<IUdpConsoleService, UdpConsoleService>();
                    services.AddSingleton<IDialogService, DialogService>();
                    services.AddSingleton<IDatabaseMigrationService, DatabaseMigrationService>();
                    services.AddSingleton<IApiKeyService>(x => ApiKeyServiceFactory.Create(x.GetRequiredService<IPathProvider>()));
                    services.AddSingleton<IHttpClientService, HttpClientService>();
                    services.AddSingleton<ISingleInstanceService, SingleInstanceService>(); // Register the new service
                })
                .Build();
        }

        protected override async void OnStartup(StartupEventArgs e)
        {
            // Get the single instance service
            var singleInstanceService = AppHost!.Services.GetRequiredService<ISingleInstanceService>();

            if (!singleInstanceService.IsFirstInstance())
            {
                // Another instance is already running, the service already showed a message box
                Application.Current.Shutdown();
                return;
            }

            await AppHost.StartAsync();

            var startupForm = AppHost.Services.GetRequiredService<MainWindow>();
            startupForm.Show();

            base.OnStartup(e);
        }

        protected override async void OnExit(ExitEventArgs e)
        {
            await AppHost!.StopAsync();
            // Release the mutex when the application exits
            var singleInstanceService = AppHost.Services.GetRequiredService<ISingleInstanceService>();
            singleInstanceService.ReleaseInstance();
            // The Dispose method of SingleInstanceService will be called by the DI container when AppHost is disposed.
            base.OnExit(e);
        }
    }
}