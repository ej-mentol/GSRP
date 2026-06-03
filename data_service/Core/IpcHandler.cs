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
        private readonly Action<int> _setMigrationCount;
        private TaskCompletionSource<bool>? _migrationTcs;

        public IpcHandler(StorageService storage, DatabaseMigrationService migration, SteamApiService steamApi, EnrichmentCoordinator coordinator, UdpConsoleService udp, Action<string, object?> sendToElectron, Action<int> setMigrationCount)
        {
            _storage = storage;
            _migration = migration;
            _steamApi = steamApi;
            _coordinator = coordinator;
            _udp = udp;
            _sendToElectron = sendToElectron;
            _setMigrationCount = setMigrationCount;
        }

        public async Task InitializeAsync()
        {
            await _storage.InitializeAsync();
            
            int legacyCount = await _migration.GetTotalAffectedCountAsync();
            if (legacyCount > 0) {
                _migrationTcs = new TaskCompletionSource<bool>();
                _setMigrationCount(legacyCount);
                _sendToElectron("MIGRATION_REQUIRED", new MigrationData(legacyCount));
                await _migrationTcs.Task; // Wait for explicit migration command
                _setMigrationCount(0);
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
                            _migrationTcs?.TrySetResult(true);
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

                    case "GET_DB_COLORS":
                        {
                            var colors = await _storage.GetUniqueColorsAsync();
                            _sendToElectron("DB_COLORS_RESULT", new { colors });
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
                            if (doc.RootElement.TryGetProperty("payload", out var payload)) {
                                var saId = payload.GetProperty("steamId").GetString();
                                var alias = payload.TryGetProperty("alias", out var aProp) ? aProp.GetString() : null;
                                if (!string.IsNullOrEmpty(saId)) {
                                    var p = await _storage.GetPlayerAsync(saId) ?? new Player { SteamId64 = saId };
                                    await _storage.UpdatePlayerCustomizationAsync(saId, alias, p.PlayerColor, p.PersonaNameColor, p.AliasColor, p.CardColor);
                                    p.Alias = alias; // Optimistic update
                                    _sendToElectron("UPDATE_PLAYER", p);
                                }
                            }
                        }
                        break;

                    case "SET_COLOR":
                        {
                            if (doc.RootElement.TryGetProperty("payload", out var payload)) {
                                var scId = payload.GetProperty("steamId").GetString();
                                var color = payload.TryGetProperty("color", out var cProp) ? cProp.GetString() : null;
                                var target = payload.GetProperty("target").GetString(); 
                                if (!string.IsNullOrEmpty(scId)) {
                                    var p = await _storage.GetPlayerAsync(scId) ?? new Player { SteamId64 = scId };
                                    if (target == "game") p.PlayerColor = color;
                                    else if (target == "steam") p.PersonaNameColor = color;
                                    else if (target == "alias") p.AliasColor = color;
                                    else if (target == "card") p.CardColor = color;
                                    await _storage.UpdatePlayerCustomizationAsync(scId, p.Alias, p.PlayerColor, p.PersonaNameColor, p.AliasColor, p.CardColor);
                                    _sendToElectron("UPDATE_PLAYER", p);
                                }
                            }
                        }
                        break;

                    case "SET_ICON":
                        {
                            if (doc.RootElement.TryGetProperty("payload", out var payload)) {
                                var scId = payload.GetProperty("steamId").GetString();
                                var icon = payload.TryGetProperty("icon", out var iProp) ? iProp.GetString() : null;
                                if (!string.IsNullOrEmpty(scId)) {
                                    await _storage.UpdatePlayerIconAsync(scId, icon);
                                    var p = await _storage.GetPlayerAsync(scId) ?? new Player { SteamId64 = scId };
                                    _sendToElectron("UPDATE_PLAYER", p);
                                }
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

                    case "GET_LOCAL_IPS":
                        {
                            var ips = new List<string> { "0.0.0.0", "127.0.0.1" };
                            try {
                                foreach (var ni in System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces()) {
                                    // Include interfaces that are Up or just not Down (to be safe)
                                    if (ni.OperationalStatus == System.Net.NetworkInformation.OperationalStatus.Up || 
                                        ni.OperationalStatus == System.Net.NetworkInformation.OperationalStatus.Unknown) {
                                        
                                        var props = ni.GetIPProperties();
                                        foreach (var ip in props.UnicastAddresses) {
                                            if (ip.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork) {
                                                string addr = ip.Address.ToString();
                                                ips.Add(addr);
                                                _sendToElectron("CONSOLE_LOG", new LogData("SYS", $"[Net] Detected interface: {ni.Name} ({addr})"));
                                            }
                                        }
                                    }
                                }
                            } catch (Exception ex) {
                                _sendToElectron("CONSOLE_LOG", new LogData("SYS", $"[Net] Interface detection error: {ex.Message}"));
                            }
                            _sendToElectron("LOCAL_IPS_RESULT", new LocalIpsData(ips.Distinct().OrderBy(x => x).ToList()));
                        }
                        break;

                    case "UDP_START":
                        {
                            if (doc.RootElement.TryGetProperty("payload", out var payload)) {
                                var port = payload.GetProperty("port").GetInt32();
                                var ip = payload.TryGetProperty("ip", out var ipProp) ? ipProp.GetString() ?? "0.0.0.0" : "0.0.0.0";
                                _udp.Start(port, ip);
                            }
                        }
                        break;

                    case "UDP_STOP":
                        {
                            _udp.Stop();
                        }
                        break;

                    case "SAVE_CUSTOMIZATION":
                        {
                            if (doc.RootElement.TryGetProperty("payload", out var payload)) {
                                var sid = payload.GetProperty("steamId").GetString();
                                if (!string.IsNullOrEmpty(sid)) {
                                    string? a = payload.TryGetProperty("alias", out var ap) ? ap.GetString() : null;
                                    string? gc = payload.TryGetProperty("gameColor", out var gcp) ? gcp.GetString() : null;
                                    string? sc = payload.TryGetProperty("steamColor", out var scp) ? scp.GetString() : null;
                                    string? ac = payload.TryGetProperty("aliasColor", out var acp) ? acp.GetString() : null;
                                    string? cc = payload.TryGetProperty("cardColor", out var ccp) ? ccp.GetString() : null;

                                    await _storage.UpdatePlayerCustomizationAsync(sid, a, gc, sc, ac, cc);
                                    
                                    // Send optimistic update back to UI
                                    var p = await _storage.GetPlayerAsync(sid) ?? new Player { SteamId64 = sid };
                                    _sendToElectron("UPDATE_PLAYER", p);
                                }
                            }
                        }
                        break;
                }
            } catch (Exception ex) {
                _sendToElectron("CONSOLE_LOG", new LogData("SYS", $"IPC Error: {ex.Message}"));
            }
        }
    }
}