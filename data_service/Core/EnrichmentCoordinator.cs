using System;
using System.IO;
using System.Net.Http;
using System.Collections.Generic;
using System.Threading.Channels;
using System.Threading.Tasks;
using GSRP.Backend.Models;
using GSRP.Daemon.Services;
using GSRP.Daemon.Models;

namespace GSRP.Daemon.Core
{
    public class EnrichmentCoordinator
    {
        private readonly StorageService _storage;
        private readonly SteamApiService _steamApi;
        private readonly HttpClient _httpClient;
        private readonly Action<string, object?> _sendToElectron;
        private readonly Channel<string> _workChannel = Channel.CreateBounded<string>(new BoundedChannelOptions(1000) { FullMode = BoundedChannelFullMode.Wait });
        private readonly string _cachePath;
        private readonly Dictionary<string, Player> _activePlayers = new(); // Track current session players
        
        public AppSettings Settings { get; set; } = new AppSettings();

        public EnrichmentCoordinator(StorageService storage, SteamApiService steamApi, Action<string, object?> sendToElectron)
        {
            _storage = storage;
            _steamApi = steamApi;
            _sendToElectron = sendToElectron;
            _httpClient = new HttpClient();
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            _cachePath = Path.Combine(appData, "GSRP", "cache");
            if (!Directory.Exists(_cachePath)) Directory.CreateDirectory(_cachePath);
            _ = Task.Run(ProcessQueueAsync);
        }

        public async Task ProcessDetectedPlayersAsync(List<Player> players)
        {
            _sendToElectron("CONSOLE_LOG", new LogData("SYS", $"Enriching {players.Count} players..."));
            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var toUpdate = new List<string>();

            lock(_activePlayers) {
                _activePlayers.Clear();
                foreach(var p in players) _activePlayers[p.SteamId64] = p;
            }

            foreach (var p in players)
            {
                var gameName = p.DisplayName;
                var dbPlayer = await _storage.GetPlayerAsync(p.SteamId64);
                
                if (dbPlayer != null)
                {
                    p.Alias = dbPlayer.Alias;
                    p.PlayerColor = dbPlayer.PlayerColor;
                    p.PersonaName = dbPlayer.PersonaName;
                    p.AvatarHash = dbPlayer.AvatarHash;
                    p.TimeCreated = dbPlayer.TimeCreated;
                    p.LastVacCheck = dbPlayer.LastVacCheck;
                    p.NumberOfVacBans = dbPlayer.NumberOfVacBans;
                    p.NumberOfGameBans = dbPlayer.NumberOfGameBans;
                    p.IsCommunityBanned = dbPlayer.IsCommunityBanned;
                    p.EconomyBan = dbPlayer.EconomyBan;
                    p.LastUpdated = dbPlayer.LastUpdated;
                    p.IconName = dbPlayer.IconName;
                    p.PersonaNameColor = dbPlayer.PersonaNameColor;
                    p.AliasColor = dbPlayer.AliasColor;
                    p.CardColor = dbPlayer.CardColor;
                    p.ProfileStatus = dbPlayer.ProfileStatus;
                }

                // AVATAR INTEGRITY CHECK
                bool hasAvatarFile = false;
                if (!string.IsNullOrEmpty(p.AvatarHash) && p.AvatarHash != "0")
                {
                    var avatarPath = Path.Combine(_cachePath, $"{p.AvatarHash}.jpg");
                    hasAvatarFile = File.Exists(avatarPath);
                }

                bool needsSummary = (now - p.LastUpdated) > 1200 || p.TimeCreated == 0 || !hasAvatarFile;
                
                bool needsBans = p.LastVacCheck == null;
                if (!needsBans && Settings.EnablePeriodicVacCheck && (now - (p.LastVacCheck ?? 0)) > 86400) needsBans = true;

                if (needsSummary || needsBans) toUpdate.Add(p.SteamId64);
            }

            _sendToElectron("PLAYERS_DETECTED", new DetectedData(null, players));
            foreach (var id in toUpdate) await _workChannel.Writer.WriteAsync(id);
        }

        private async Task ProcessQueueAsync()
        {
            while (await _workChannel.Reader.WaitToReadAsync())
            {
                await Task.Delay(500);
                var batch = new List<string>();
                while (batch.Count < 100 && _workChannel.Reader.TryRead(out var id)) if (!batch.Contains(id)) batch.Add(id);
                if (batch.Count == 0) continue;

                try {
                    var summaries = await _steamApi.GetSummariesAsync(batch);
                    var bans = await _steamApi.GetBansAsync(batch);

                    foreach (var id in batch)
                    {
                        var s = summaries.Find(x => x.Steamid == id);
                        var b = bans.Find(x => x.SteamId == id);

                        if (s == null && b == null) continue;

                        Player? p;
                        lock(_activePlayers) {
                            _activePlayers.TryGetValue(id, out p);
                        }
                        
                        if (p == null) p = await _storage.GetPlayerAsync(id) ?? new Player { SteamId64 = id };
                        
                        if (s != null) {
                            p.ProfileStatus = s.Communityvisibilitystate == 3 ? "Public" : "Private";
                            p.PersonaName = s.Personaname;
                            p.TimeCreated = s.Timecreated;
                            p.AvatarHash = s.Avatarhash;
                            p.LastUpdated = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                            
                            if (string.IsNullOrEmpty(p.DisplayName)) p.DisplayName = s.Personaname;
                        }

                        if (b != null) {
                            p.IsCommunityBanned = b.CommunityBanned;
                            p.NumberOfVacBans = b.NumberOfVACBans;
                            p.NumberOfGameBans = b.NumberOfGameBans;
                            p.LastVacCheck = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                            
                            // If any ban exists, calculate the date (even if it happened 0 days ago)
                            if (b.NumberOfVACBans > 0 || b.NumberOfGameBans > 0 || b.CommunityBanned) {
                                p.BanDate = p.LastVacCheck.Value - (b.DaysSinceLastBan * 86400L);
                            } else {
                                p.BanDate = 0; // Clear date if no bans
                            }

                            // CLEAN ECONOMY BAN
                            var eco = b.EconomyBan?.ToLower();
                            p.EconomyBan = (eco == "none" || string.IsNullOrWhiteSpace(eco) || eco == "0") ? "none" : b.EconomyBan;
                        }

                        await _storage.SavePlayerAsync(p);
                        _sendToElectron("UPDATE_PLAYER", p);

                        if (s != null && !string.IsNullOrEmpty(s.Avatarhash) && s.Avatarhash != "0") {
                            var hash = s.Avatarhash;
                            _ = Task.Run(async () => {
                                try {
                                    var avatarFile = Path.Combine(_cachePath, $"{hash}.jpg");
                                    if (!File.Exists(avatarFile)) await DownloadAvatarAsync(hash, avatarFile);
                                } catch { }
                            });
                        }
                    }
                } catch { }
            }
        }

        private async Task DownloadAvatarAsync(string hash, string path)
        {
            try {
                var url = $"https://avatars.steamstatic.com/{hash}_medium.jpg";
                var bytes = await _httpClient.GetByteArrayAsync(url);
                await File.WriteAllBytesAsync(path, bytes);
                _sendToElectron("CONSOLE_LOG", new LogData("SYS", $"[Cache] Downloaded avatar: {hash}"));
            } catch (Exception ex) { 
                _sendToElectron("CONSOLE_LOG", new LogData("SYS", $"[Cache] Failed to download {hash}: {ex.Message}"));
            }
        }
    }
}
