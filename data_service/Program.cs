using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using GSRP.Daemon.Core;
using GSRP.Daemon.Services;
using GSRP.Daemon.Models;

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

        static async Task Main(string[] args)
        {
            // --- SINGLE INSTANCE LOCK ---
            using var mutex = new System.Threading.Mutex(false, "Global\\GSRP_Daemon_Mutex");
            bool isAnotherInstanceRunning = !mutex.WaitOne(TimeSpan.Zero, true);

            if (isAnotherInstanceRunning)
            {
                // Send a log message before exiting so Electron knows why it failed
                Console.WriteLine("{\"type\":\"CONSOLE_LOG\",\"data\":{\"tag\":\"SYS\",\"text\":\"Another daemon instance is already running. Exiting.\"}}");
                return;
            }

            _storage = new StorageService();
            _migration = new DatabaseMigrationService();
            _steamApi = new SteamApiService();
            
            // EnrichmentCoordinator expects (StorageService, SteamApiService, Action<string, object?>)
            _coordinator = new EnrichmentCoordinator(_storage, _steamApi, SendToElectron);
            
            // UdpConsoleService expects (Action<string, object?>)
            _udpService = new UdpConsoleService(SendToElectron);
            
            // IpcHandler expects (StorageService, DatabaseMigrationService, SteamApiService, EnrichmentCoordinator, UdpConsoleService, Action<string, object?>)
            _ipcHandler = new IpcHandler(_storage, _migration, _steamApi, _coordinator, _udpService, SendToElectron);
            
            _parser = new PlayerParser();
            _clipboard = new ClipboardMonitor();

            // Load settings
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var settingsPath = Path.Combine(appData, "GSRP", "settings.json");
            int udpPort = 26000;
            if (File.Exists(settingsPath)) {
                try {
                    var json = File.ReadAllText(settingsPath);
                    var settings = JsonSerializer.Deserialize(json, JsonContext.Default.AppSettings);
                    if (settings != null) {
                        _coordinator.Settings = settings;
                        udpPort = settings.UdpListenPort;
                    }
                } catch { }
            }

            await _ipcHandler.InitializeAsync();

            _clipboard.ClipboardChanged += async (text) => {
                var res = _parser.Parse(text);
                if (res.Players.Count > 0) {
                    await _coordinator.ProcessDetectedPlayersAsync(res.Players);
                }
            };
            _clipboard.Start();

            _ = Task.Run(ListenIpcAsync);
            // UDP service will be started manually via IPC command

            await Task.Delay(-1);
        }

        static async Task ListenIpcAsync()
        {
            while (true)
            {
                var line = await Console.In.ReadLineAsync();
                if (line == null) break; // Pipe closed, exit gracefully
                
                if (!string.IsNullOrEmpty(line) && _ipcHandler != null)
                {
                    await _ipcHandler.HandleMessageAsync(line);
                }
            }
        }

        static void SendToElectron(string type, object? data)
        {
            try {
                var msg = new IpcMessage(type, data);
                Console.WriteLine(JsonSerializer.Serialize(msg, JsonContext.Default.IpcMessage));
            } catch { }
        }
    }
}
