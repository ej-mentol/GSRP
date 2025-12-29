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

namespace GSRP.Daemon
{
    class Program
    {
        private static StorageService? _storage;
        private static DatabaseMigrationService? _migration;
        private static IpcHandler? _ipcHandler;
        private static PlayerParser? _parser;
        private static ClipboardMonitor? _clipboard;
        private static EnrichmentCoordinator? _coordinator;
        private static SteamApiService? _steamApi;
        private static UdpConsoleService? _udpService;

        private static DaemonState _currentState = new DaemonState("Booting", "Start", 0, "Launching web server...");

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
                
                // Bind strictly to IPv4 to avoid localhost resolution issues
                builder.WebHost.ConfigureKestrel(o => o.Listen(IPAddress.Parse("127.0.0.1"), 5000));
                
                var app = builder.Build();
                app.UseCors("AllowElectron");

                app.MapGet("/health", () => Results.Json(_currentState, JsonContext.Default.DaemonState));
                
                app.MapPost("/shutdown", (IHostApplicationLifetime l) => { 
                    ShutdownResources(); 
                    l.StopApplication(); 
                    return Results.Ok(); 
                });

                _ = Task.Run(async () => {
                    try {
                        _currentState = new DaemonState("Initializing", "Database", 10, "Opening storage...");
                        _storage = new StorageService();
                        await _storage.InitializeAsync();

                        _currentState = _currentState with { Step = "Services", Progress = 40, Details = "Loading GSRP modules..." };
                        _migration = new DatabaseMigrationService();
                        _steamApi = new SteamApiService();
                        _coordinator = new EnrichmentCoordinator(_storage, _steamApi, SendToElectron);
                        _udpService = new UdpConsoleService(SendToElectron);
                        _ipcHandler = new IpcHandler(_storage, _migration, _steamApi, _coordinator, _udpService, SendToElectron, (count) => {
                            _currentState = _currentState with { MigrationRequiredCount = count };
                        });
                        _parser = new PlayerParser();
                        _clipboard = new ClipboardMonitor();

                        _currentState = _currentState with { Step = "Config", Progress = 70, Details = "Reading settings..." };
                        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                        var settingsPath = Path.Combine(appData, "GSRP", "settings.json");
                        if (File.Exists(settingsPath)) {
                            var settings = JsonSerializer.Deserialize(File.ReadAllText(settingsPath), JsonContext.Default.AppSettings);
                            if (settings != null) _coordinator.Settings = settings;
                        }

                        _currentState = _currentState with { Step = "Migration", Progress = 90, Details = "Checking data integrity..." };
                        await _ipcHandler.InitializeAsync();
                        
                        _clipboard.ClipboardChanged += async (text) => {
                            var res = _parser.Parse(text);
                            if (res.Players.Count > 0) await _coordinator.ProcessDetectedPlayersAsync(res.Players);
                        };
                        _clipboard.Start();

                        _currentState = new DaemonState("Ready", "Idle", 100, "Core Engine is stable.");
                    } catch (Exception ex) {
                        _currentState = new DaemonState("Error", "Fatal", 0, ex.Message);
                    }
                });

                _ = Task.Run(ListenIpcAsync);
                await app.RunAsync();
            } catch (Exception fatalEx) {
                Console.Error.WriteLine($"FATAL CRASH: {fatalEx.Message}");
            }
        }

        private static void ShutdownResources()
        {
            try { _clipboard?.Stop(); _udpService?.Stop(); } catch { }
        }

        static async Task ListenIpcAsync()
        {
            try {
                while (true) {
                    var line = await Console.In.ReadLineAsync();
                    if (line == null) break; 
                    if (!string.IsNullOrEmpty(line) && _ipcHandler != null) await _ipcHandler.HandleMessageAsync(line);
                }
            } catch { }
        }

        static void SendToElectron(string type, object? data)
        {
            try {
                var msg = new IpcMessage(type, data);
                var json = JsonSerializer.Serialize(msg, JsonContext.Default.IpcMessage);
                Console.WriteLine(json);
            } catch { }
        }
    }
}
