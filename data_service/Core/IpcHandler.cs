using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using GSRP.Daemon.Services;
using GSRP.Daemon.Models;
using GSRP.Backend.Models;

namespace GSRP.Daemon.Core
{
    public class IpcHandler
    {
        private readonly StorageService _storage;
        private readonly DatabaseMigrationService _migration;
        private readonly SteamApiService _steamApi;
        private readonly EnrichmentCoordinator _coordinator;
        private readonly UdpConsoleService _udp; // New
        private readonly Action<string, object?> _sendToElectron;

        public IpcHandler(StorageService storage, DatabaseMigrationService migration, SteamApiService steamApi, EnrichmentCoordinator coordinator, UdpConsoleService udp, Action<string, object?> sendToElectron)
        {
            _storage = storage;
            _migration = migration;
            _steamApi = steamApi;
            _coordinator = coordinator;
            _udp = udp;
            _sendToElectron = sendToElectron;
        }

        public async Task InitializeAsync()
        {
            await _storage.InitializeAsync();
            
            int legacyCount = await _migration.GetTotalAffectedCountAsync();
            if (legacyCount > 0) {
                _sendToElectron("MIGRATION_REQUIRED", new MigrationData(legacyCount));
            } else {
                _sendToElectron("READY", null);
            }
        }

        public async Task HandleMessageAsync(string json)
        {
            try {
                using var doc = JsonDocument.Parse(json);
                var type = doc.RootElement.GetProperty("type").GetString();

                switch (type) {
                    case "START_MIGRATION":
                        {
                            bool backup = doc.RootElement.TryGetProperty("payload", out var p) && p.TryGetProperty("backup", out var b) && b.GetBoolean();
                            _sendToElectron("MIGRATION_PROGRESS", new ProgressData(50, "Cleaning legacy color markers..."));
                            await _migration.RunMigrationsAsync(backup);
                            _sendToElectron("MIGRATION_SUCCESS", null);
                        }
                        break;

                    case "UPDATE_SETTING":
                        {
                            // Backend just needs to reload settings from disk
                            // (In this monolithic setup, the coordinator already has access to settings if we pass them)
                        }
                        break;

                    case "SEARCH_DB":
                        {
                            if (doc.RootElement.TryGetProperty("payload", out var payload)) {
                                var term = payload.TryGetProperty("t", out var tProp) ? tProp.GetString() ?? "" : "";
                                bool cs = payload.TryGetProperty("caseSensitive", out var csProp) && csProp.GetBoolean();
                                var color = payload.TryGetProperty("color", out var cProp) ? cProp.GetString() : null;
                                
                                bool vac = payload.TryGetProperty("vacBanned", out var vP) && vP.GetBoolean();
                                bool game = payload.TryGetProperty("gameBanned", out var gP) && gP.GetBoolean();
                                bool comm = payload.TryGetProperty("communityBanned", out var cmP) && cmP.GetBoolean();
                                bool eco = payload.TryGetProperty("economyBanned", out var eP) && eP.GetBoolean();
                                
                                var results = await _storage.SearchPlayersAsync(term, cs, color, vac, game, comm, eco);
                                _sendToElectron("SEARCH_RESULT", new SearchResultsData(results));
                            }
                        }
                        break;

                    case "REFRESH_PLAYER":
                        {
                            var rpId = doc.RootElement.GetProperty("payload").GetProperty("steamId").GetString();
                            if (!string.IsNullOrEmpty(rpId)) {
                                var p = await _storage.GetPlayerAsync(rpId) ?? new Player { SteamId64 = rpId };
                                await _coordinator.ProcessDetectedPlayersAsync(new List<Player> { p });
                            }
                        }
                        break;

                    case "SET_ALIAS":
                        {
                            var saId = doc.RootElement.GetProperty("payload").GetProperty("steamId").GetString();
                            var alias = doc.RootElement.GetProperty("payload").GetProperty("alias").GetString();
                            if (!string.IsNullOrEmpty(saId)) {
                                var p = await _storage.GetPlayerAsync(saId) ?? new Player { SteamId64 = saId };
                                p.Alias = alias;
                                await _storage.SavePlayerAsync(p);
                                _sendToElectron("UPDATE_PLAYER", p);
                            }
                        }
                        break;

                    case "SET_COLOR":
                        {
                            var scId = doc.RootElement.GetProperty("payload").GetProperty("steamId").GetString();
                            var color = doc.RootElement.GetProperty("payload").GetProperty("color").GetString();
                            var target = doc.RootElement.GetProperty("payload").GetProperty("target").GetString(); 
                            if (!string.IsNullOrEmpty(scId)) {
                                var p = await _storage.GetPlayerAsync(scId) ?? new Player { SteamId64 = scId };
                                if (target == "game") p.PlayerColor = color;
                                else if (target == "steam") p.PersonaNameColor = color;
                                else if (target == "alias") p.AliasColor = color;
                                else if (target == "card") p.CardColor = color;
                                await _storage.SavePlayerAsync(p);
                                _sendToElectron("UPDATE_PLAYER", p);
                            }
                        }
                        break;

                    case "SET_API_KEY":
                        {
                            var key = doc.RootElement.GetProperty("payload").GetProperty("key").GetString();
                            if (!string.IsNullOrEmpty(key)) _steamApi.SaveApiKey(key);
                        }
                        break;

                    case "SEND_UDP":
                        {
                            if (doc.RootElement.TryGetProperty("payload", out var payload)) {
                                var ip = payload.GetProperty("ip").GetString() ?? "127.0.0.1";
                                var port = payload.GetProperty("port").GetInt32();
                                var msg = payload.GetProperty("message").GetString() ?? "";
                                if (!string.IsNullOrEmpty(msg)) await _udp.SendMessageAsync(ip, port, msg);
                            }
                        }
                        break;

                    case "UDP_START":
                        {
                            if (doc.RootElement.TryGetProperty("payload", out var payload)) {
                                var port = payload.GetProperty("port").GetInt32();
                                _udp.Start(port);
                            }
                        }
                        break;

                    case "UDP_STOP":
                        {
                            _udp.Stop();
                        }
                        break;
                }
            } catch (Exception ex) {
                _sendToElectron("CONSOLE_LOG", new LogData("SYS", $"IPC Error: {ex.Message}"));
            }
        }
    }
}