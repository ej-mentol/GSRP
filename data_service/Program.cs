using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Net;
using Microsoft.AspNetCore.Http.Json;
using GSRP.Daemon.Core;
using GSRP.Daemon.Services;
using GSRP.Backend.Models;
using Microsoft.Extensions.Logging;

namespace GSRP.Daemon
{
    public class DaemonStateService
    {
        public DaemonState CurrentState { get; set; } = new DaemonState("Booting", "Start", 0, "Launching web server...");
    }

    class Program
    {
        static async Task Main(string[] args)
        {
            try {
                var builder = WebApplication.CreateBuilder(args);
                
                builder.Services.AddCors(options => {
                    options.AddPolicy("AllowElectron", policy => {
                        policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader();
                    });
                });

                builder.Logging.SetMinimumLevel(LogLevel.Warning);
                builder.Services.Configure<JsonOptions>(o => o.SerializerOptions.TypeInfoResolverChain.Insert(0, JsonContext.Default));
                
                builder.WebHost.ConfigureKestrel(o => o.Listen(IPAddress.Parse("127.0.0.1"), 5000));
                
                // State
                builder.Services.AddSingleton<DaemonStateService>();

                // IPC Notifier (Resolves the Action<string, object?> dependency)
                builder.Services.AddSingleton<Action<string, object?>>((type, data) => 
                {
                    try {
                        string dataJson = "null";
                        if (data != null)
                        {
                            dataJson = JsonSerializer.Serialize(data, data.GetType(), JsonContext.Default);
                        }
                        
                        var json = $"{{\"type\":\"{type}\",\"data\":{dataJson}}}";
                        Console.WriteLine(json);
                        Console.Out.Flush(); // Ensure immediate delivery to Electron
                    } catch { }
                });

                // Core Services
                builder.Services.AddSingleton<StorageService>();
                builder.Services.AddSingleton<DatabaseMigrationService>();
                builder.Services.AddSingleton<SteamApiService>();
                builder.Services.AddSingleton<PlayerParser>();
                builder.Services.AddSingleton<ClipboardMonitor>();
                builder.Services.AddSingleton<EnrichmentCoordinator>();
                builder.Services.AddSingleton<UdpConsoleService>();

                // IpcHandler requires a custom factory because of the Action<int> delegate
                builder.Services.AddSingleton<IpcHandler>(sp => 
                {
                    var storage = sp.GetRequiredService<StorageService>();
                    var migration = sp.GetRequiredService<DatabaseMigrationService>();
                    var steamApi = sp.GetRequiredService<SteamApiService>();
                    var coordinator = sp.GetRequiredService<EnrichmentCoordinator>();
                    var udpService = sp.GetRequiredService<UdpConsoleService>();
                    var sendToElectron = sp.GetRequiredService<Action<string, object?>>();
                    var stateService = sp.GetRequiredService<DaemonStateService>();

                    return new IpcHandler(storage, migration, steamApi, coordinator, udpService, sendToElectron, (count) => {
                        stateService.CurrentState = stateService.CurrentState with { MigrationRequiredCount = count };
                    });
                });

                // Application Lifecycle
                builder.Services.AddHostedService<DaemonLifecycleService>();

                var app = builder.Build();
                app.UseCors("AllowElectron");

                app.MapGet("/health", (DaemonStateService state) => Results.Json(state.CurrentState, JsonContext.Default.DaemonState));
                
                app.MapPost("/shutdown", (IHostApplicationLifetime l, ClipboardMonitor clip, UdpConsoleService udp) => { 
                    try { clip.Stop(); udp.Stop(); } catch { }
                    l.StopApplication(); 
                    return Results.Ok(); 
                });

                await app.RunAsync();
            } catch (Exception fatalEx) {
                Console.Error.WriteLine($"FATAL CRASH: {fatalEx.Message}");
            }
        }
    }

    public class DaemonLifecycleService : BackgroundService
    {
        private readonly DaemonStateService _stateService;
        private readonly StorageService _storage;
        private readonly DatabaseMigrationService _migration;
        private readonly SteamApiService _steamApi;
        private readonly EnrichmentCoordinator _coordinator;
        private readonly UdpConsoleService _udpService;
        private readonly IpcHandler _ipcHandler;
        private readonly PlayerParser _parser;
        private readonly ClipboardMonitor _clipboard;

        public DaemonLifecycleService(
            DaemonStateService stateService, StorageService storage, DatabaseMigrationService migration, 
            SteamApiService steamApi, EnrichmentCoordinator coordinator, UdpConsoleService udpService, 
            IpcHandler ipcHandler, PlayerParser parser, ClipboardMonitor clipboard)
        {
            _stateService = stateService;
            _storage = storage;
            _migration = migration;
            _steamApi = steamApi;
            _coordinator = coordinator;
            _udpService = udpService;
            _ipcHandler = ipcHandler;
            _parser = parser;
            _clipboard = clipboard;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try {
                _stateService.CurrentState = new DaemonState("Initializing", "Database", 10, "Opening storage...");
                await _storage.InitializeAsync();

                _stateService.CurrentState = _stateService.CurrentState with { Step = "Config", Progress = 70, Details = "Reading settings..." };
                var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                var settingsPath = Path.Combine(appData, "GSRP", "settings.json");
                if (File.Exists(settingsPath)) {
                    var settings = JsonSerializer.Deserialize(File.ReadAllText(settingsPath), JsonContext.Default.AppSettings);
                    if (settings != null) _coordinator.Settings = settings;
                }

                _stateService.CurrentState = _stateService.CurrentState with { Step = "Migration", Progress = 90, Details = "Checking data integrity..." };
                await _ipcHandler.InitializeAsync();
                
                _clipboard.ClipboardChanged += async (text) => {
                    var res = _parser.Parse(text);
                    if (res.Players.Count > 0) await _coordinator.ProcessDetectedPlayersAsync(res.Players);
                };
                _clipboard.Start();

                _stateService.CurrentState = new DaemonState("Ready", "Idle", 100, "Core Engine is stable.");

                // Start IPC listening loop
                _ = Task.Run(() => ListenIpcAsync(stoppingToken), stoppingToken);
            } catch (Exception ex) {
                _stateService.CurrentState = new DaemonState("Error", "Fatal", 0, ex.Message);
            }
        }

        private async Task ListenIpcAsync(CancellationToken stoppingToken)
        {
            try {
                using var reader = new StreamReader(Console.OpenStandardInput());
                while (!stoppingToken.IsCancellationRequested) {
                    var line = await reader.ReadLineAsync(stoppingToken);
                    if (line == null) break; 
                    if (!string.IsNullOrEmpty(line)) await _ipcHandler.HandleMessageAsync(line);
                }
            } catch { }
        }
    }
}
