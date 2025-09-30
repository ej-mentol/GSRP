using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using GSRP.Models;
using GSRP.Models.SteamApi;
using System.Collections.Concurrent;

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
        private CancellationTokenSource _clipboardCts = new();
        private static readonly SemaphoreSlim _avatarDownloadSemaphore = new SemaphoreSlim(4);
        private static readonly SemaphoreSlim _enrichmentSemaphore = new SemaphoreSlim(1, 1);
        private readonly ConcurrentDictionary<string, string> _avatarPathCache = new();

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
            var newCts = new CancellationTokenSource();
            var oldCts = Interlocked.Exchange(ref _clipboardCts, newCts);
            oldCts.Cancel();
            oldCts.Dispose();
            var token = newCts.Token;

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
                    player.LastUpdated = data.LastUpdated;
                }
            }

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
            await EnrichPlayersAsync(parsedPlayers, false, false, token);
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

            await EnrichPlayersAsync(new List<Player> { player }, true, true, token);
        }

        public async Task EnrichSinglePlayerAsync(Player player, CancellationToken token)
        {
            bool semaphoreAcquired = false;
            try
            {
                await _enrichmentSemaphore.WaitAsync(token);
                semaphoreAcquired = true;

                var summaryTask = _steamApi.EnrichPlayersAsync(new List<Player> { player }, token);
                var bansTask = _steamApi.GetPlayerBansAsync(new List<Player> { player }, token);
                await Task.WhenAll(summaryTask, bansTask);

                var summaryResult = await summaryTask;
                var bansResult = await bansTask;

                if (!summaryResult.Success)
                {
                    throw new Exception(summaryResult.ErrorMessage);
                }

                if (summaryResult.Data.TryGetValue(player.SteamId64, out var summary))
                {
                    ApplySummaryDataToPlayer(player, summary);
                    try { await UpdatePlayerSummaryInDb(player, token); }
                    catch (Exception ex) 
                    {
                        System.Diagnostics.Debug.WriteLine($"[EnrichSinglePlayer] Failed to save summary: {ex.Message}");
                        _dialogService.ShowMessageDialog("Database Error", $"Failed to save summary update for {player.DisplayNameForUI}.");
                    }
                }

                if (bansResult?.FirstOrDefault(b => b.SteamId == player.SteamId64) is { } banData)
                {
                    ApplyBanDataToPlayer(player, banData);
                    try { await UpdatePlayerBansInDb(player); }
                    catch (Exception ex) 
                    {
                        System.Diagnostics.Debug.WriteLine($"[EnrichSinglePlayer] Failed to save bans: {ex.Message}");
                        _dialogService.ShowMessageDialog("Database Error", $"Failed to save ban status for {player.DisplayNameForUI}.");
                    }
                }
            }
            catch (OperationCanceledException)
            {
                System.Diagnostics.Debug.WriteLine($"[EnrichSinglePlayer] Operation was cancelled.");
                // Do not rethrow cancellation exceptions as they are not user-facing errors
            }
            finally
            {
                if (semaphoreAcquired)
                {
                    _enrichmentSemaphore.Release();
                }
            }
        }

        public async Task EnrichPlayersAsync(IEnumerable<Player> players, bool forceSummary, bool forceBans, CancellationToken token)
        {
            bool semaphoreAcquired = false;
            try
            {
                await _enrichmentSemaphore.WaitAsync(token);
                semaphoreAcquired = true;

                var playerList = players.ToList();
                if (!playerList.Any()) return;

                var (playersForSummary, playersForBans) = FilterPlayersForEnrichment(playerList, forceSummary, forceBans);

                foreach (var p in playersForBans) p.IsCheckingBans = true;

                var (summaryResult, bansResult) = await FetchEnrichmentData(playersForSummary, playersForBans, token);
                var failedPlayers = await ProcessEnrichmentResults(playerList, summaryResult, bansResult, token);

                if (failedPlayers.Any())
                {
                    ShowBatchErrorDialog(failedPlayers);
                }
            }
            catch (OperationCanceledException)
            {
                System.Diagnostics.Debug.WriteLine($"[Enrichment] Operation was cancelled.");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Enrichment] Error: {ex.Message}");
            }
            finally
            {
                if(semaphoreAcquired)
                {
                    foreach (var p in players.ToList())
                    {
                        p.IsUpdating = false;
                        p.IsCheckingBans = false;
                    }
                    _enrichmentSemaphore.Release();
                }
            }
        }

        private (List<Player> summary, List<Player> bans) FilterPlayersForEnrichment(List<Player> players, bool forceSummary, bool forceBans)
        {
            var playersForSummary = new List<Player>();
            var playersForBans = new List<Player>();
            var now = DateTimeOffset.Now.ToUnixTimeSeconds();

            foreach (var player in players)
            {
                if (forceSummary || (now - player.LastUpdated) > 1200) // 20 mins
                    playersForSummary.Add(player);

                if (forceBans || player.LastVacCheck == 0 ||
                    (_settingsService.CurrentSettings.EnablePeriodicVacCheck && (now - player.LastVacCheck) > 86400))
                    playersForBans.Add(player);
            }

            return (playersForSummary, playersForBans);
        }

        private async Task<(EnrichmentResult, List<PlayerBanData>?)> FetchEnrichmentData(List<Player> playersForSummary, List<Player> playersForBans, CancellationToken token)
        {
            var summaryTask = _steamApi.EnrichPlayersAsync(playersForSummary, token);
            var bansTask = _steamApi.GetPlayerBansAsync(playersForBans, token);
            await Task.WhenAll(summaryTask, bansTask);
            return (await summaryTask, await bansTask);
        }

        private async Task<List<Player>> ProcessEnrichmentResults(List<Player> playerList, EnrichmentResult summaryResult, List<PlayerBanData>? bansResult, CancellationToken token)
        {
            var failedPlayers = new List<Player>();
            foreach (var player in playerList)
            {
                if (summaryResult?.Data.TryGetValue(player.SteamId64, out var summary) == true)
                {
                    ApplySummaryDataToPlayer(player, summary);
                    try
                    {
                        await UpdatePlayerSummaryInDb(player, token);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[Repository] Failed to save summary data for player {player.SteamId64}: {ex.Message}");
                        if (!failedPlayers.Contains(player)) failedPlayers.Add(player);
                    }
                }

                if (bansResult?.FirstOrDefault(b => b.SteamId == player.SteamId64) is { } banData)
                {
                    ApplyBanDataToPlayer(player, banData);
                    try
                    {
                        await UpdatePlayerBansInDb(player);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[Repository] Failed to save ban data for player {player.SteamId64}: {ex.Message}");
                        if (!failedPlayers.Contains(player)) failedPlayers.Add(player);
                    }
                }
            }
            return failedPlayers;
        }

        private void ShowBatchErrorDialog(List<Player> failedPlayers)
        {
            var playerNames = string.Join(", ", failedPlayers.Take(3).Select(p => p.DisplayNameForUI));
            var message = $"Failed to save updated data for {failedPlayers.Count} player(s) (e.g., {playerNames}). The application may re-check them later.";
            _dialogService.ShowMessageDialog("Database Write Error", message);
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

            foreach (var player in playersToEnrich)
            {
                if (!long.TryParse(player.SteamId64, out var steamId64)) continue;

                if (totalSteamData.TryGetValue(player.SteamId64, out var data))
                {
                    ApplySummaryDataToPlayer(player, data);
                }
                else
                {
                    player.ProfileStatus = ProfileStatus.NotFound;
                }

                try
                {
                    await UpdatePlayerSummaryInDb(player, token);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[DoEnrichment] Failed to save summary for {steamId64}: {ex.Message}");
                }
            }
        }

        private async Task UpdatePlayerSummaryInDb(Player player, CancellationToken token)
        {
            if (!long.TryParse(player.SteamId64, out var steamId64)) return;

            try { await _database.SetPersonaNameAsync(steamId64, player.PersonaName); }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[Repository] Failed to save PersonaName for {steamId64}: {ex.Message}"); }

            try { await _database.SetTimeCreatedAsync(steamId64, player.TimeCreated); }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[Repository] Failed to save TimeCreated for {steamId64}: {ex.Message}"); }

            try { await _database.SetAvatarHashAsync(steamId64, player.AvatarHash); }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[Repository] Failed to save AvatarHash for {steamId64}: {ex.Message}"); }

            try { await _database.SetProfileStatusAsync(steamId64, player.ProfileStatus); }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[Repository] Failed to save ProfileStatus for {steamId64}: {ex.Message}"); }

            try { await _database.SetLastUpdatedAsync(steamId64, DateTimeOffset.Now.ToUnixTimeSeconds()); }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[Repository] Failed to save LastUpdated for {steamId64}: {ex.Message}"); }

            if (!string.IsNullOrEmpty(player.AvatarHash) && !player.IsAvatarCached)
            {
                player.IsUpdating = true; // Set IsUpdating here
                try
                {
                    await DownloadAvatarAsync(player, player.AvatarHash, token);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[Repository] Failed to download avatar for {steamId64}: {ex.Message}");
                }
                finally
                {
                    player.IsUpdating = false; // Reset IsUpdating here
                }
            }
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

            bool semaphoreAcquired = false;
            try
            {
                await _avatarDownloadSemaphore.WaitAsync(token);
                semaphoreAcquired = true;

                token.ThrowIfCancellationRequested();

                var avatarUrl = $"https://avatars.steamstatic.com/{avatarHash}_medium.jpg";
                var avatarPath = Path.Combine(_pathProvider.GetCachePath(), $"{avatarHash}.jpg");

                if (File.Exists(avatarPath))
                {
                    _avatarPathCache[avatarHash] = avatarPath;
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        player.AvatarPath = avatarPath;
                        player.IsAvatarCached = true;
                        player.AvatarDownloadFailed = false;
                    });
                    return;
                }

                var httpClient = _httpClientService.GetClient();
                var response = await httpClient.GetAsync(avatarUrl, token);

                if (response.IsSuccessStatusCode)
                {
                    var imageBytes = await response.Content.ReadAsByteArrayAsync(token);
                    await File.WriteAllBytesAsync(avatarPath, imageBytes, token);
                    _avatarPathCache[avatarHash] = avatarPath;
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        player.AvatarPath = avatarPath;
                        player.IsAvatarCached = true;
                        player.AvatarDownloadFailed = false;
                    });
                }
                else
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        player.AvatarDownloadFailed = true;
                    });
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
                if (semaphoreAcquired)
                {
                    _avatarDownloadSemaphore.Release();
                }
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
                player.IconName = oldIconName;
                player.IconPath = oldIconPath;
                _dialogService.ShowMessageDialog("Database Error", "Failed to update player icon.");
                System.Diagnostics.Debug.WriteLine($"[Repository] Failed to set icon: {ex.Message}");
            }
        }

            private void ApplySummaryDataToPlayer(Player player, PlayerData summary)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    player.ProfileStatus = summary.IsPrivate ? ProfileStatus.Private : ProfileStatus.Public;
                    player.PersonaName = summary.PersonaName;
                    // Only update TimeCreated if it's not already set.
                    if (player.TimeCreated == 0 && summary.TimeCreated > 0)
                    {
                        player.TimeCreated = summary.TimeCreated;
                    }
                    player.AvatarHash = summary.AvatarHash;
                    player.AvatarPath = GetAvatarPath(player.AvatarHash);
                    player.IsAvatarCached = !string.IsNullOrEmpty(player.AvatarPath);
                });
            }
        private void ApplyBanDataToPlayer(Player player, PlayerBanData banData)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                player.IsCommunityBanned = banData.CommunityBanned;
                player.EconomyBan = banData.EconomyBan;
                player.NumberOfVacBans = banData.NumberOfVACBans;
                long banDate = (banData.DaysSinceLastBan > 0)
                    ? DateTimeOffset.Now.ToUnixTimeSeconds() - (banData.DaysSinceLastBan * 86400L)
                    : 0;
                player.BanDate = banDate;
                player.LastVacCheck = DateTimeOffset.Now.ToUnixTimeSeconds();
            });
        }

        public void Dispose()
        {
            _cts.Cancel();
            _cts.Dispose();
            _clipboardCts.Cancel();
            _clipboardCts.Dispose();
            PlayersUpdated = null;
            lock (_lock) _currentPlayers.Clear();
        }

        public string GetAvatarPath(string avatarHash)
        {
            if (string.IsNullOrEmpty(avatarHash))
                return string.Empty;

            return _avatarPathCache.GetOrAdd(avatarHash, hash =>
            {
                var path = Path.Combine(_pathProvider.GetCachePath(), $"{hash}.jpg");
                return File.Exists(path) ? path : string.Empty;
            });
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
                    Alias = result.Data.Alias,
                    PlayerColor = result.Data.PlayerColor,
                    PersonaNameColor = result.Data.PersonaNameColor,
                    AliasColor = result.Data.AliasColor,
                    AvatarHash = result.Data.AvatarHash,
                    TimeCreated = result.Data.TimeCreated,
                    PersonaName = result.Data.PersonaName,
                    IconName = result.Data.IconName,
                    IconPath = _iconService.ResolveIconPath(result.Data.IconName) ?? string.Empty,
                    IsCommunityBanned = result.Data.IsCommunityBanned,
                    NumberOfVacBans = result.Data.NumberOfVacBans,
                    LastVacCheck = result.Data.LastVacCheck,
                    EconomyBan = result.Data.EconomyBan,
                    BanDate = result.Data.BanDate,
                    ProfileStatus = result.Data.ProfileStatus,
                    LastUpdated = result.Data.LastUpdated
                };
                player.AvatarPath = GetAvatarPath(player.AvatarHash);
                player.IsAvatarCached = !string.IsNullOrEmpty(player.AvatarPath);
                player.Name = result.Data.PersonaName;
                players.Add(player);
            }

            return players;
        }
    }
}
