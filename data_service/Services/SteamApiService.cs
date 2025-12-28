using System;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using GSRP.Daemon.Core;

namespace GSRP.Daemon.Services
{
    public class SteamApiService
    {
        private readonly HttpClient _http;
        private string? _apiKey;

        public SteamApiService()
        {
            _http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
            _http.DefaultRequestHeaders.Add("User-Agent", "GSRP/2.0 (Windows; +https://github.com/necr/GSRP)");
            LoadExistingKey();
        }

        private void LogToConsole(string text) {
            try {
                var msg = new IpcMessage("CONSOLE_LOG", new LogData("SYS", text));
                Console.WriteLine(JsonSerializer.Serialize(msg, JsonContext.Default.IpcMessage));
            } catch { }
        }

        private byte[] GetEntropy() {
            var machineId = Environment.MachineName + 
                           Environment.UserName + 
                           Environment.ProcessorCount.ToString() + 
                           Environment.OSVersion.Version.ToString();
            using var sha256 = SHA256.Create();
            return sha256.ComputeHash(Encoding.UTF8.GetBytes(machineId));
        }

        private void LoadExistingKey()
        {
            try {
                var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                var keyPath = Path.Combine(appData, "GSRP", ".steamapi");
                if (File.Exists(keyPath)) {
                    var data = File.ReadAllBytes(keyPath);
                    try {
                        var dec = ProtectedData.Unprotect(data, GetEntropy(), DataProtectionScope.CurrentUser);
                        _apiKey = Encoding.UTF8.GetString(dec).Trim();
                        LogToConsole($"[SteamAPI] Key active. Real Length: {_apiKey.Length}");
                    } catch {
                        LogToConsole("[SteamAPI] Key found but decryption failed. Salt mismatch?");
                    }
                }
            } catch (Exception ex) {
                LogToConsole($"[SteamAPI] Init Error: {ex.Message}");
            }
        }

        public void SaveApiKey(string key) {
            if (string.IsNullOrWhiteSpace(key)) return;
            key = key.Trim();
            try {
                var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                var keyPath = Path.Combine(appData, "GSRP", ".steamapi");
                if (File.Exists(keyPath)) File.Delete(keyPath);
                var data = Encoding.UTF8.GetBytes(key);
                var enc = ProtectedData.Protect(data, GetEntropy(), DataProtectionScope.CurrentUser);
                File.WriteAllBytes(keyPath, enc);
                _apiKey = key;
                LogToConsole($"[SteamAPI] Key updated and saved. Length: {_apiKey.Length}");
            } catch (Exception ex) {
                LogToConsole($"[SteamAPI] Save error: {ex.Message}");
            }
        }

        public async Task<List<SteamPlayer>> GetSummariesAsync(List<string> steamIds)
        {
            if (string.IsNullOrEmpty(_apiKey) || steamIds.Count == 0) return new();
            int retry = 0;
            while (retry < 3) {
                try {
                    LogToConsole($"[SteamAPI] Fetching summaries (Attempt {retry + 1})...");
                    var url = $"https://api.steampowered.com/ISteamUser/GetPlayerSummaries/v2/?key={_apiKey}&steamids={string.Join(",", steamIds)}";
                    var json = await _http.GetStringAsync(url);
                    using var doc = JsonDocument.Parse(json);
                    var list = new List<SteamPlayer>();
                    
                    if (doc.RootElement.TryGetProperty("response", out var resp) && resp.TryGetProperty("players", out var players)) {
                        foreach (var p in players.EnumerateArray()) {
                            string GetStr(string name) {
                                foreach (var prop in p.EnumerateObject()) 
                                    if (prop.Name.Equals(name, StringComparison.OrdinalIgnoreCase)) return prop.Value.GetString() ?? "";
                                return "";
                            }
                            uint GetUInt(string name) {
                                foreach (var prop in p.EnumerateObject()) 
                                    if (prop.Name.Equals(name, StringComparison.OrdinalIgnoreCase)) return prop.Value.TryGetUInt32(out var v) ? v : 0;
                                return 0;
                            }
                            int GetInt(string name) {
                                foreach (var prop in p.EnumerateObject()) 
                                    if (prop.Name.Equals(name, StringComparison.OrdinalIgnoreCase)) return prop.Value.TryGetInt32(out var v) ? v : 0;
                                return 0;
                            }

                            list.Add(new SteamPlayer(GetStr("steamid"), GetStr("personaname"), GetStr("avatarhash"), GetUInt("timecreated"), GetInt("communityvisibilitystate")));
                        }
                    }
                    return list;
                } catch (Exception ex) { 
                    retry++;
                    if (retry == 3) LogToConsole($"[SteamAPI] Final Request Failure: {ex.Message}");
                    else await Task.Delay(1000);
                }
            }
            return new();
        }

        public async Task<List<SteamBan>> GetBansAsync(List<string> steamIds)
        {
            if (string.IsNullOrEmpty(_apiKey) || steamIds.Count == 0) return new();
            int retry = 0;
            while (retry < 3) {
                try {
                    var url = $"https://api.steampowered.com/ISteamUser/GetPlayerBans/v1/?key={_apiKey}&steamids={string.Join(",", steamIds)}";
                    var json = await _http.GetStringAsync(url);
                    using var doc = JsonDocument.Parse(json);
                    var list = new List<SteamBan>();
                    if (doc.RootElement.TryGetProperty("players", out var players)) {
                        foreach (var p in players.EnumerateArray()) {
                            list.Add(new SteamBan(
                                p.GetProperty("SteamId").GetString() ?? "",
                                p.GetProperty("CommunityBanned").GetBoolean(),
                                p.GetProperty("VACBanned").GetBoolean(),
                                p.GetProperty("NumberOfVACBans").GetInt32(),
                                p.GetProperty("DaysSinceLastBan").GetInt32(),
                                p.GetProperty("NumberOfGameBans").GetInt32(),
                                p.GetProperty("EconomyBan").GetString() ?? "none"
                            ));
                        }
                    }
                    return list;
                } catch { 
                    retry++;
                    if (retry < 3) await Task.Delay(1000);
                }
            }
            return new();
        }
    }
}