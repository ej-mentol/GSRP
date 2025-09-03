using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;
using GSRP.Models;
using GSRP.Models.SteamApi;

namespace GSRP.Services
{
    public class PlayerRepository : IPlayerRepository, IDisposable
    {
        private readonly IDatabaseService _database;
        private readonly SteamApiService _steamApi;
        private readonly IDialogService _dialogService;
        private readonly IIconService _iconService;
        private readonly IPlayerListParser _playerListParser;
        private readonly IPathProvider _pathProvider;
        private readonly ISettingsService _settingsService;
        private readonly Dictionary<string, Player> _currentPlayers;
        private readonly object _lock = new();
        private readonly IHttpClientService _httpClientService;
        private readonly CancellationTokenSource _cts = new();
        private static readonly SemaphoreSlim _avatarDownloadSemaphore = new SemaphoreSlim(4);

        public event EventHandler<List<Player>>? PlayersUpdated;

        public PlayerRepository(IDatabaseService database, IHttpClientService httpClientService, IApiKeyService apiKeyService, IDialogService dialogService, IIconService iconService, IPlayerListParser playerListParser, IPathProvider pathProvider, ISettingsService settingsService)
        {
            _database = database ?? throw new ArgumentNullException(nameof(database));
            _httpClientService = httpClientService ?? throw new ArgumentNullException(nameof(httpClientService));
            _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
            _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
            _steamApi = new SteamApiService(_httpClientService, apiKeyService);
            _iconService = iconService ?? throw new ArgumentNullException(nameof(iconService));
            _playerListParser = playerListParser ?? throw new ArgumentNullException(nameof(playerListParser));
            _pathProvider = pathProvider ?? throw new ArgumentNullException(nameof(pathProvider));
            _currentPlayers = new Dictionary<string, Player>();
        }

        public async Task ProcessClipboardDataAsync(string clipboardText, IProgress<string> progress)
        {
            progress?.Report("Parsing player list...");
            var parsedPlayers = _playerListParser.ParsePlayers(clipboardText);
            if (!parsedPlayers.Any()) return;

            var steamIds = parsedPlayers.Select(p => p.SteamId64).Where(id => !string.IsNullOrEmpty(id)).ToList();
            var dbData = await _database.GetPlayersDataAsync(steamIds);

            foreach (var player in parsedPlayers)
            {
                if (dbData.TryGetValue(player.SteamId64, out var data))
                {
                    player.Alias = data.Alias;
                    player.PlayerColor = data.PlayerColor;
                    player.PersonaNameColor = data.PersonaNameColor;
                    player.AliasColor = data.AliasColor;
                    player.AvatarHash = data.AvatarHash;
                    player.TimeCreated = data.TimeCreated;
                    player.PersonaName = data.PersonaName;
                    player.IconName = data.IconName;
                    player.IconPath = _iconService.ResolveIconPath(data.IconName) ?? string.Empty;
                    player.AvatarPath = GetAvatarPath(data.AvatarHash);
                    player.IsAvatarCached = !string.IsNullOrEmpty(player.AvatarPath);
                    player.ProfileStatus = data.ProfileStatus;
                    player.IsCommunityBanned = data.IsCommunityBanned;
                    player.NumberOfVacBans = data.NumberOfVacBans;
                    player.LastVacCheck = data.LastVacCheck;
                    player.EconomyBan = data.EconomyBan;
                    player.BanDate = data.BanDate;
                    player.LastUpdated = data.LastUpdated; // New line
                }
            }

            NotifyPlayersUpdated();

            progress?.Report("Updating player list...");
            lock (_lock)
            {
                _currentPlayers.Clear();
                foreach (var player in parsedPlayers)
                {
                    if (!string.IsNullOrEmpty(player.SteamId64))
                    {
                        _currentPlayers[player.SteamId64] = player;
                    }
                }
            }

            NotifyPlayersUpdated();

            progress?.Report("Enriching data...");
            await EnrichPlayersAsync(parsedPlayers, false, false, _cts.Token);
        }

        public async Task ForceEnrichCurrentPlayersAsync(CancellationToken token)
        {
            List<Player> playersToUpdate;
            lock (_lock)
            {
                playersToUpdate = new List<Player>(_currentPlayers.Values);
            }

            if (playersToUpdate.Any())
            {
                await DoEnrichmentAsync(playersToUpdate, token);
            }
        }

        public async Task EnrichSinglePlayerVacStatusAsync(Player player, CancellationToken token)
        {
            if (player == null) return;

            // Use the existing EnrichPlayersAsync with forceBans = true for this single player
            await EnrichPlayersAsync(new List<Player> { player }, false, true, token);
        }

        public async Task EnrichPlayersAsync(IEnumerable<Player> players, bool forceSummary, bool forceBans, CancellationToken token)
        {
            var playerList = players.ToList();
            if (!playerList.Any()) return;

            var playersForSummary = new List<Player>();
            var playersForBans = new List<Player>();

            try
            {
                // Decide who needs updates
                foreach (var player in playerList)
                {
                    if (forceSummary || (DateTimeOffset.Now.ToUnixTimeSeconds() - player.LastUpdated) > 1200) // 20 mins
                    {
                        playersForSummary.Add(player);
                    }
                    if (forceBans || player.LastVacCheck == 0 || (_settingsService.CurrentSettings.EnablePeriodicVacCheck && (DateTimeOffset.Now.ToUnixTimeSeconds() - player.LastVacCheck) > 86400))
                    {
                        playersForBans.Add(player);
                    }
                }

                // Set status flags before starting API calls
                foreach (var p in playersForSummary)
                {
                    // Only show the main "Updating" spinner if we don't have an avatar to display.
                    // Otherwise, the refresh happens silently in the background.
                    if (!p.IsAvatarCached)
                    {
                        p.IsUpdating = true;
                    }
                }
                foreach (var p in playersForBans)
                {
                    // Only show the specific ban checking text if we aren't already showing the main spinner.
                    if (!p.IsUpdating)
                    {
                        p.IsCheckingBans = true;
                    }
                }

                // Fetch data from API
                var summaryTask = _steamApi.EnrichPlayersAsync(playersForSummary, token);
                var bansTask = _steamApi.GetPlayerBansAsync(playersForBans, token);
                await Task.WhenAll(summaryTask, bansTask);

                var summaryResult = await summaryTask;
                var bansResult = await bansTask;

                // Process results and update DB
                var dbTasks = new List<Task>();
                foreach (var player in playerList)
                {
                    if (summaryResult?.Data.TryGetValue(player.SteamId64, out var summary) == true)
                    {
                        ApplySummaryDataToPlayer(player, summary);
                        dbTasks.Add(UpdatePlayerSummaryInDb(player));
                    }

                    if (bansResult?.FirstOrDefault(b => b.SteamId == player.SteamId64) is { } banData)
                    {
                        ApplyBanDataToPlayer(player, banData);
                        dbTasks.Add(UpdatePlayerBansInDb(player));
                    }
                }

                await Task.WhenAll(dbTasks);
            }
            catch (Exception ex)
            {
                // Log error
                System.Diagnostics.Debug.WriteLine($"[Enrichment] Error: {ex.Message}");
            }
            finally
            {
                foreach (var p in playerList)
                {
                    p.IsUpdating = false;
                    p.IsCheckingBans = false;
                }
            }
        }

        private async Task DoEnrichmentAsync(List<Player> playersToEnrich, CancellationToken token)
        {
            if (!playersToEnrich.Any()) return;

            const int batchSize = 100;
            var totalSteamData = new Dictionary<string, PlayerData>();
            bool anyBatchFailed = false;

            for (int i = 0; i < playersToEnrich.Count; i += batchSize)
            {
                token.ThrowIfCancellationRequested();
                var batch = playersToEnrich.Skip(i).Take(batchSize).ToList();
                if (!batch.Any()) continue;

                try
                {
                    var enrichmentResult = await _steamApi.EnrichPlayersAsync(batch, token);
                    if (!enrichmentResult.Success)
                    {
                        if (token.IsCancellationRequested) break;
                        _dialogService.ShowMessageDialog("Steam API Error", enrichmentResult.ErrorMessage ?? "Unknown error during API request.");
                        anyBatchFailed = true;
                        break;
                    }
                    foreach (var entry in enrichmentResult.Data)
                        totalSteamData[entry.Key] = entry.Value;
                }
                catch (OperationCanceledException)
                {
                    anyBatchFailed = true;
                    break;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error enriching batch: {ex.Message}");
                    _dialogService.ShowMessageDialog("Processing Error", $"An error occurred while processing a batch: {ex.Message}");
                    anyBatchFailed = true;
                    break;
                }
            }

            if (anyBatchFailed) return;

            var allUpdateTasks = new List<Task>();

            foreach (var player in playersToEnrich)
            {
                if (!long.TryParse(player.SteamId64, out var steamId64)) continue;

                if (totalSteamData.TryGetValue(player.SteamId64, out var data))
                {
                    // Player was found in the API response
                    player.ProfileStatus = data.IsPrivate ? ProfileStatus.Private : ProfileStatus.Public;

                    // Update all available data
                    player.PersonaName = data.PersonaName;
                    allUpdateTasks.Add(_database.SetPersonaNameAsync(steamId64, data.PersonaName));
                    player.TimeCreated = data.TimeCreated;
                    allUpdateTasks.Add(_database.SetTimeCreatedAsync(steamId64, data.TimeCreated));
                    player.AvatarHash = data.AvatarHash;
                    allUpdateTasks.Add(_database.SetAvatarHashAsync(steamId64, data.AvatarHash));

                    if (!string.IsNullOrEmpty(data.AvatarHash) && !player.IsAvatarCached)
                    {
                        allUpdateTasks.Add(DownloadAvatarAsync(player, data.AvatarHash, token));
                    }
                }
                else
                {
                    // Player was NOT in the API response. Treat as not found.
                    player.ProfileStatus = ProfileStatus.NotFound;
                }

                allUpdateTasks.Add(_database.SetProfileStatusAsync(steamId64, player.ProfileStatus));

                // Always update the 'last_updated' timestamp to ensure the record is created for everyone.
                allUpdateTasks.Add(_database.SetLastUpdatedAsync(steamId64, DateTimeOffset.Now.ToUnixTimeSeconds()));
            }

            await Task.WhenAll(allUpdateTasks);
        }

        

        private Task UpdatePlayerSummaryInDb(Player player)
        {
            if (!long.TryParse(player.SteamId64, out var steamId64)) return Task.CompletedTask;
            var tasks = new List<Task>
            {
                _database.SetPersonaNameAsync(steamId64, player.PersonaName),
                _database.SetTimeCreatedAsync(steamId64, player.TimeCreated),
                _database.SetAvatarHashAsync(steamId64, player.AvatarHash),
                _database.SetProfileStatusAsync(steamId64, player.ProfileStatus),
                _database.SetLastUpdatedAsync(steamId64, DateTimeOffset.Now.ToUnixTimeSeconds())
            };
            if (!string.IsNullOrEmpty(player.AvatarHash) && !player.IsAvatarCached)
            {
                tasks.Add(DownloadAvatarAsync(player, player.AvatarHash, _cts.Token));
            }
            return Task.WhenAll(tasks);
        }

        private Task UpdatePlayerBansInDb(Player player)
        {
            if (!long.TryParse(player.SteamId64, out var steamId64)) return Task.CompletedTask;
            return _database.UpdatePlayerBanStatusAsync(steamId64, player.IsCommunityBanned, player.EconomyBan, player.NumberOfVacBans, player.BanDate, player.LastVacCheck);
        }

        private void NotifyPlayersUpdated()
        {
            var handler = PlayersUpdated;
            if (handler == null) return;

            List<Player> playersCopy;
            lock (_lock) { playersCopy = new List<Player>(_currentPlayers.Values); }

            if (System.Windows.Application.Current?.Dispatcher.CheckAccess() ?? true)
                handler(this, playersCopy);
            else
                System.Windows.Application.Current?.Dispatcher.BeginInvoke(() => handler(this, playersCopy));
        }

        private async Task DownloadAvatarAsync(Player player, string avatarHash, CancellationToken token)
        {
            if (string.IsNullOrEmpty(avatarHash)) return;

            await _avatarDownloadSemaphore.WaitAsync(token);
            player.IsUpdating = true;
            try
            {
                token.ThrowIfCancellationRequested();

                var avatarUrl = $"https://avatars.steamstatic.com/{avatarHash}_medium.jpg";
                var avatarPath = Path.Combine(_pathProvider.GetCachePath(), $"{avatarHash}.jpg");

                if (File.Exists(avatarPath))
                {
                    player.AvatarPath = avatarPath;
                    player.IsAvatarCached = true;
                    player.AvatarDownloadFailed = false;
                    return;
                }

                var httpClient = _httpClientService.GetClient();
                var response = await httpClient.GetAsync(avatarUrl, token);

                if (response.IsSuccessStatusCode)
                {
                    var imageBytes = await response.Content.ReadAsByteArrayAsync(token);
                    await File.WriteAllBytesAsync(avatarPath, imageBytes, token);
                    player.AvatarPath = avatarPath;
                    player.IsAvatarCached = true;
                    player.AvatarDownloadFailed = false;
                }
                else
                {
                    player.AvatarDownloadFailed = true;
                }
            }
            catch (OperationCanceledException)
            {
                player.AvatarDownloadFailed = false;
            }
            catch (Exception ex)
            {
                player.AvatarDownloadFailed = true;
                System.Diagnostics.Debug.WriteLine($"Failed to download avatar {avatarHash}: {ex.Message}");
            }
            finally
            {
                player.IsUpdating = false;
                _avatarDownloadSemaphore.Release();
            }
        }

        public List<Player> GetCurrentPlayers()
        {
            lock (_lock) return new List<Player>(_currentPlayers.Values);
        }

        public async Task SetPlayerAliasAsync(Player player, string alias)
        {
            if (!long.TryParse(player.SteamId64, out var steamId64)) return;

            var oldAlias = player.Alias;
            player.Alias = alias; // Optimistic update

            try
            {
                await _database.SetAliasAsync(steamId64, alias);
                lock (_lock)
                {
                    if (_currentPlayers.TryGetValue(player.SteamId64, out var existing))
                    {
                        existing.Alias = alias;
                    }
                }
            }
            catch (Exception ex)
            {
                player.Alias = oldAlias; // Rollback on failure
                _dialogService.ShowMessageDialog("Database Error", "Failed to update player alias.");
                System.Diagnostics.Debug.WriteLine($"[Repository] Failed to set alias: {ex.Message}");
            }
        }

        public async Task SetPlayerColorAsync(Player player, Color color)
        {
            if (!long.TryParse(player.SteamId64, out var steamId64)) return;

            var oldColor = player.PlayerColor;
            player.PlayerColor = color; // Optimistic update

            try
            {
                await _database.SetTextColorAsync(steamId64, color);
                lock (_lock)
                {
                    if (_currentPlayers.TryGetValue(player.SteamId64, out var existing))
                    {
                        existing.PlayerColor = color;
                    }
                }
            }
            catch (Exception ex)
            {
                player.PlayerColor = oldColor; // Rollback on failure
                _dialogService.ShowMessageDialog("Database Error", "Failed to update player color.");
                System.Diagnostics.Debug.WriteLine($"[Repository] Failed to set player color: {ex.Message}");
            }
        }
        
        public async Task SetPlayerPersonaNameColorAsync(Player player, Color color)
        {
            if (!long.TryParse(player.SteamId64, out var steamId64)) return;

            var oldColor = player.PersonaNameColor;
            player.PersonaNameColor = color; // Optimistic update

            try
            {
                await _database.SetPersonaNameColorAsync(steamId64, color);
                lock (_lock)
                {
                    if (_currentPlayers.TryGetValue(player.SteamId64, out var existing))
                    {
                        existing.PersonaNameColor = color;
                    }
                }
            }
            catch (Exception ex)
            {
                player.PersonaNameColor = oldColor; // Rollback on failure
                _dialogService.ShowMessageDialog("Database Error", "Failed to update Steam name color.");
                System.Diagnostics.Debug.WriteLine($"[Repository] Failed to set Steam name color: {ex.Message}");
            }
        }

        public async Task RemovePlayerColorAsync(Player player)
        {
            if (!long.TryParse(player.SteamId64, out var steamId64)) return;

            var oldColor = player.PlayerColor;
            player.PlayerColor = null; // Optimistic update

            try
            {
                await _database.RemoveTextColorAsync(steamId64);
                lock (_lock)
                {
                    if (_currentPlayers.TryGetValue(player.SteamId64, out var existing))
                    {
                        existing.PlayerColor = null;
                    }
                }
            }
            catch (Exception ex)
            {
                player.PlayerColor = oldColor; // Rollback on failure
                _dialogService.ShowMessageDialog("Database Error", "Failed to remove player color.");
                System.Diagnostics.Debug.WriteLine($"[Repository] Failed to remove player color: {ex.Message}");
            }
        }

        public async Task RemovePlayerPersonaNameColorAsync(Player player)
        {
            if (!long.TryParse(player.SteamId64, out var steamId64)) return;

            var oldColor = player.PersonaNameColor;
            player.PersonaNameColor = null; // Optimistic update

            try
            {
                await _database.RemovePersonaNameColorAsync(steamId64);
                lock (_lock)
                {
                    if (_currentPlayers.TryGetValue(player.SteamId64, out var existing))
                    {
                        existing.PersonaNameColor = null;
                    }
                }
            }
            catch (Exception ex)
            {
                player.PersonaNameColor = oldColor; // Rollback on failure
                _dialogService.ShowMessageDialog("Database Error", "Failed to remove Steam name color.");
                System.Diagnostics.Debug.WriteLine($"[Repository] Failed to remove Steam name color: {ex.Message}");
            }
        }

        public async Task SetPlayerAliasColorAsync(Player player, Color color)
        {
            if (!long.TryParse(player.SteamId64, out var steamId64)) return;

            var oldColor = player.AliasColor;
            player.AliasColor = color; // Optimistic update

            try
            {
                await _database.SetAliasColorAsync(steamId64, color);
                lock (_lock)
                {
                    if (_currentPlayers.TryGetValue(player.SteamId64, out var existing))
                    {
                        existing.AliasColor = color;
                    }
                }
            }
            catch (Exception ex)
            {
                player.AliasColor = oldColor; // Rollback on failure
                _dialogService.ShowMessageDialog("Database Error", "Failed to update alias color.");
                System.Diagnostics.Debug.WriteLine($"[Repository] Failed to set alias color: {ex.Message}");
            }
        }

        public async Task RemovePlayerAliasColorAsync(Player player)
        {
            if (!long.TryParse(player.SteamId64, out var steamId64)) return;

            var oldColor = player.AliasColor;
            player.AliasColor = null; // Optimistic update

            try
            {
                await _database.RemoveAliasColorAsync(steamId64);
                lock (_lock)
                {
                    if (_currentPlayers.TryGetValue(player.SteamId64, out var existing))
                    {
                        existing.AliasColor = null;
                    }
                }
            }
            catch (Exception ex)
            {
                player.AliasColor = oldColor; // Rollback on failure
                _dialogService.ShowMessageDialog("Database Error", "Failed to remove alias color.");
                System.Diagnostics.Debug.WriteLine($"[Repository] Failed to remove alias color: {ex.Message}");
            }
        }

        public async Task SetPlayerIconAsync(Player player, string iconName)
        {
            if (!long.TryParse(player.SteamId64, out var steamId64)) return;

            var oldIconName = player.IconName;
            var oldIconPath = player.IconPath;

            // Optimistic update
            player.IconName = iconName;
            player.IconPath = _iconService.ResolveIconPath(iconName) ?? string.Empty;

            try
            {
                await _database.SetIconNameAsync(steamId64, iconName);
                lock (_lock)
                {
                    if (_currentPlayers.TryGetValue(player.SteamId64, out var existing))
                    {
                        existing.IconName = iconName;
                        existing.IconPath = player.IconPath;
                    }
                }
            }
            catch (Exception ex)
            {
                // Rollback on failure
                player.IconName = oldIconName;
                player.IconPath = oldIconPath;
                _dialogService.ShowMessageDialog("Database Error", "Failed to update player icon.");
                System.Diagnostics.Debug.WriteLine($"[Repository] Failed to set icon: {ex.Message}");
            }
        }

        private void ApplySummaryDataToPlayer(Player player, PlayerData summary)
        {
            player.ProfileStatus = summary.IsPrivate ? ProfileStatus.Private : ProfileStatus.Public;
            player.PersonaName = summary.PersonaName;
            player.TimeCreated = summary.TimeCreated;
            player.AvatarHash = summary.AvatarHash;
            player.AvatarPath = GetAvatarPath(player.AvatarHash);
            player.IsAvatarCached = !string.IsNullOrEmpty(player.AvatarPath);
        }

        private void ApplyBanDataToPlayer(Player player, PlayerBanData banData)
        {
            player.IsCommunityBanned = banData.CommunityBanned;
            player.EconomyBan = banData.EconomyBan;
            player.NumberOfVacBans = banData.NumberOfVACBans;
            long banDate = (banData.DaysSinceLastBan > 0)
                ? DateTimeOffset.Now.ToUnixTimeSeconds() - (banData.DaysSinceLastBan * 86400L) // 86400 seconds in a day
                : 0;
            player.BanDate = banDate;
            player.LastVacCheck = DateTimeOffset.Now.ToUnixTimeSeconds();
        }

        public void Dispose()
        {
            _cts.Cancel();
            _cts.Dispose();
            PlayersUpdated = null;
            lock (_lock) _currentPlayers.Clear();
        }

        public string GetAvatarPath(string avatarHash)
        {
            if (string.IsNullOrEmpty(avatarHash))
                return string.Empty;

            var path = Path.Combine(_pathProvider.GetCachePath(), $"{avatarHash}.jpg");
            return File.Exists(path) ? path : string.Empty;
        }

        public async Task<List<Player>> SearchPlayersAsync(string searchTerm, string? steamId64Term, bool exactMatch)
        {
            var dbResults = await _database.SearchPlayersAsync(searchTerm, steamId64Term, exactMatch);
            var players = new List<Player>();

            foreach (var result in dbResults)
            {
                var player = new Player
                {
                    SteamId64 = result.SteamId64,
                    SteamId2 = _playerListParser.SteamId64To2(result.SteamId64),
                    Alias = result.Data.Alias,
                    PlayerColor = result.Data.PlayerColor,
                    PersonaNameColor = result.Data.PersonaNameColor,
                    AliasColor = result.Data.AliasColor,
                    AvatarHash = result.Data.AvatarHash,
                    TimeCreated = result.Data.TimeCreated,
                    PersonaName = result.Data.PersonaName,
                    IconName = result.Data.IconName,
                    IconPath = _iconService.ResolveIconPath(result.Data.IconName) ?? string.Empty,
                    AvatarPath = GetAvatarPath(result.Data.AvatarHash),
                    IsAvatarCached = !string.IsNullOrEmpty(GetAvatarPath(result.Data.AvatarHash)),
                    IsCommunityBanned = result.Data.IsCommunityBanned,
                    NumberOfVacBans = result.Data.NumberOfVacBans,
                    LastVacCheck = result.Data.LastVacCheck,
                    EconomyBan = result.Data.EconomyBan,
                    BanDate = result.Data.BanDate,
                    ProfileStatus = result.Data.ProfileStatus,
                    LastUpdated = result.Data.LastUpdated // New line
                };

                // Name is not stored in the DB, so we use PersonaName as a sensible default for display.
                player.Name = result.Data.PersonaName;
                players.Add(player);
            }

            return players;
        }
    }
}