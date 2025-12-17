using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GSRP.Models;
using GSRP.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using System.Media;
using System.Windows.Media;

namespace GSRP.ViewModels
{
    public record IconInfo(string Name, string? ImagePath);

    public partial class MainViewModel : ObservableObject, IDisposable
    {
        private readonly IPlayerRepository _playerRepository;
        private readonly IClipboardService _clipboardService;
        private readonly IPlayerListParser _playerListParser;
        private readonly IUdpConsoleService _udpConsoleService;
        private readonly ISettingsService _settingsService;
        private readonly IDialogService _dialogService;
        private readonly IIconService _iconService;
        private readonly IApiKeyService _apiKeyService;
        private readonly IScreenshotService _screenshotService;
        private readonly CancellationTokenSource _cts = new();
        private int _isProcessingClipboard = 0;
        private readonly ObservableCollection<Player> _allPlayers = new();
        private CancellationTokenSource? _filterCts;

        public ObservableCollection<Player> FilteredPlayers { get; }
        public ICollectionView FilteredConsoleOutput { get; }
        public ObservableCollection<string> ServerList { get; }
        public IReadOnlyList<IconInfo> AvailableIconInfos { get; private set; }
        public ObservableCollection<Player> DbSearchResults { get; }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(PlayerCount))]
        [NotifyPropertyChangedFor(nameof(PlayersTabHeader))]
        private string _searchText = string.Empty;

        [ObservableProperty]
        private string _selectedTab = TabNames.Players;

        [ObservableProperty]
        private bool _isMonitoring;

        [ObservableProperty]
        private string _consoleInputText = string.Empty;

        [ObservableProperty]
        private bool _isConsoleConnected;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(FilteredConsoleOutput))]
        private bool _showUserCommandsOnly;

        [ObservableProperty]
        private string? _selectedServer;

        [ObservableProperty]
        private string _reportNickname = string.Empty;

        [ObservableProperty]
        private string _reportSteamId = string.Empty;

        [ObservableProperty]
        private string _reportDetails = string.Empty;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(SearchDatabaseCommand))]
        private string _dbSearchTerm = string.Empty;

        [ObservableProperty]
        private bool _dbSearchExactMatch = true;

        [ObservableProperty]
        private string _statusMessage = "Player Database & Reporting";

        public int PlayerCount => FilteredPlayers.Count;
        public string PlayersTabHeader => $"Players ({PlayerCount})";

        public ICommand UpdatePlayerAliasAsyncCommand { get; }
        public ICommand SetPlayerGameColorAsyncCommand { get; }
        public ICommand SetPlayerSteamColorAsyncCommand { get; }
        public ICommand SetPlayerAliasColorAsyncCommand { get; }
        public ICommand SetPlayerIconAsyncCommand { get; }
        public ICommand OpenInBrowserCommand { get; }
        public ICommand CopyPlayerIdCommand { get; }
        public ICommand CopyPlayerSteamId2Command { get; }
        public ICommand CopyPlayerNameCommand { get; }
        public ICommand CopyPlayerAliasCommand { get; }
        public ICommand CopyPlayerPersonaNameCommand { get; }
        public IAsyncRelayCommand UpdateSinglePlayerVacStatusCommand { get; }
        public ICommand CopyPlayerToReportCommand { get; }
        public ICommand CopyReportForServerCommand { get; }
        public IAsyncRelayCommand CreatePlayerCardImageCommand { get; }
        public IAsyncRelayCommand SearchDatabaseCommand { get; }
        public ICommand ClearSearchTextCommand { get; }
        public ICommand ClearDbSearchTextCommand { get; }
        public ICommand TestCommand { get; } // For diagnostics

        public ISettingsService SettingsService => _settingsService;
        public AppSettings CurrentSettings => _settingsService.CurrentSettings;

        public MainViewModel(IPlayerRepository playerRepository, IClipboardService clipboardService, IPlayerListParser playerListParser, 
                             IUdpConsoleService udpConsoleService, ISettingsService settingsService, IDialogService dialogService, IIconService iconService, IApiKeyService apiKeyService, IScreenshotService screenshotService)
        {
            _playerRepository = playerRepository;
            _clipboardService = clipboardService;
            _playerListParser = playerListParser;
            _udpConsoleService = udpConsoleService;
            _settingsService = settingsService;
            _dialogService = dialogService;
            _iconService = iconService;
            _apiKeyService = apiKeyService;
            _screenshotService = screenshotService;
            
            _iconService.ScanForIcons();
            AvailableIconInfos = _iconService.AvailableIconNames
                .Select(name => new IconInfo(name, _iconService.ResolveIconPath(name)))
                .ToList();

            FilteredPlayers = new ObservableCollection<Player>();
            DbSearchResults = new ObservableCollection<Player>();
            ServerList = new ObservableCollection<string>();
            FilteredConsoleOutput = CollectionViewSource.GetDefaultView(_udpConsoleService.ConsoleOutput);
            FilteredConsoleOutput.Filter = FilterConsoleMessages;

            _playerRepository.PlayersUpdated += OnPlayersUpdated;
            _clipboardService.ClipboardChanged += OnClipboardChanged;
            _settingsService.SettingsChanged += OnSettingsChanged;

            LoadServers();
            StartMonitoring();
            RefreshPlayers();

            // Commands
            UpdateSinglePlayerVacStatusCommand = new AsyncRelayCommand<Player?>(UpdateSinglePlayerVacStatusAsync, CanUpdateSinglePlayerVacStatus);
            UpdatePlayerAliasAsyncCommand = new AsyncRelayCommand<Player?>(UpdatePlayerAliasAsync, CanExecuteOnPlayer);
            SetPlayerGameColorAsyncCommand = new AsyncRelayCommand<Player?>(p => SetPlayerColorAsync(p, ColorTarget.GameName), CanExecuteOnPlayer);
            SetPlayerSteamColorAsyncCommand = new AsyncRelayCommand<Player?>(p => SetPlayerColorAsync(p, ColorTarget.SteamName), CanExecuteOnPlayer);
            SetPlayerAliasColorAsyncCommand = new AsyncRelayCommand<Player?>(SetPlayerAliasColorAsync, CanExecuteOnPlayer);
            SetPlayerIconAsyncCommand = new AsyncRelayCommand<Tuple<object, object>?>(SetPlayerIconAsync, CanExecuteOnPlayerTuple);
            CreatePlayerCardImageCommand = new AsyncRelayCommand<Player?>(CreatePlayerCardImageAsync, CanExecuteOnPlayer);

            // These commands work with static data and don't need to be disabled during updates
            OpenInBrowserCommand = new RelayCommand<Player?>(OpenInBrowser);
            CopyPlayerIdCommand = new RelayCommand<Player?>(CopyPlayerId);
            CopyPlayerSteamId2Command = new RelayCommand<Player?>(CopyPlayerSteamId2);
            CopyPlayerNameCommand = new RelayCommand<Player?>(CopyPlayerName);
            CopyPlayerAliasCommand = new RelayCommand<Player?>(CopyPlayerAlias);
            CopyPlayerPersonaNameCommand = new RelayCommand<Player?>(CopyPlayerPersonaName);
            CopyPlayerToReportCommand = new RelayCommand<Player?>(CopyPlayerToReport);
            CopyReportForServerCommand = new RelayCommand<object>(CopyReportForServer);
            
            SearchDatabaseCommand = new AsyncRelayCommand(SearchDatabaseAsync, () => !string.IsNullOrWhiteSpace(DbSearchTerm));
            ClearSearchTextCommand = new RelayCommand(() => SearchText = string.Empty);
            ClearDbSearchTextCommand = new RelayCommand(() => DbSearchTerm = string.Empty);
            TestCommand = new RelayCommand(() => _dialogService.ShowMessageDialog("Test", "Command was executed!"));
        }

        private bool CanExecuteOnPlayer(Player? player) => player != null;
        private bool CanExecuteOnPlayerTuple(Tuple<object, object>? parameters) => parameters?.Item1 is Player;
        private bool CanExecuteOnIdlePlayer(Player? player) => player != null && !player.IsBusy;

        private async Task CreatePlayerCardImageAsync(Player? player)
        {
            if (player == null) return;

            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            while (player.IsBusy && stopwatch.Elapsed < TimeSpan.FromSeconds(5))
            {
                await Task.Delay(100);
            }

            if (player.IsBusy)
            {
                _dialogService.ShowMessageDialog("Error", "Player is still updating. Please try again in a moment.");
                return;
            }

            await _screenshotService.CreateAndCopyToClipboardAsync(player);
            StatusMessage = "Card image copied to clipboard.";
            await Task.Delay(2000);
            StatusMessage = "Player Database & Reporting";
        }

        partial void OnShowUserCommandsOnlyChanged(bool value)
        {
            FilteredConsoleOutput.Refresh();
        }

        private bool FilterConsoleMessages(object item)
        {
            if (!ShowUserCommandsOnly) return true;
            if (item is ConsoleMessage message)
            {
                return message.Type == ConsoleMessageType.UserInput;
            }
            return true;
        }

        async partial void OnSearchTextChanged(string value)
        {
            _filterCts?.Cancel();
            _filterCts = new CancellationTokenSource();
            var token = _filterCts.Token;

            try
            {
                await Task.Delay(300, token); // Debounce
                FilterPlayers(token);
            }
            catch (OperationCanceledException)
            { 
                // ignored
            }
        }

        [RelayCommand]
        private void SelectTab(string? tabName)
        {
            SelectedTab = tabName ?? TabNames.Players;
        }

        private void OpenInBrowser(Player? player)
        {
            if (player == null || string.IsNullOrEmpty(player.SteamId64)) return;
            try
            {
                var url = $"https://steamcommunity.com/profiles/{player.SteamId64}";
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                _dialogService.ShowMessageDialog("Error", $"Could not open browser: {ex.Message}");
            }
        }

        private async Task UpdatePlayerAliasAsync(Player? player)
        {
            if (player == null) return;
            var newAlias = _dialogService.ShowInputDialog($"Set alias for {player.Name}", "Alias", player.Alias);
            if (newAlias != null)
            {
                await _playerRepository.SetPlayerAliasAsync(player, newAlias);
            }
        }

        private async Task SetPlayerColorAsync(Player? player, ColorTarget target)
        {
            if (player == null) return;
            var (title, currentColor) = target == ColorTarget.GameName
                ? ($"Choose color for {player.Name}", player.PlayerColor)
                : ($"Choose color for {player.PersonaName}", player.PersonaNameColor);

            var (confirmed, newColor) = _dialogService.ShowColorPicker(title, currentColor);
            if (!confirmed) return;

            if (target == ColorTarget.GameName)
            {
                if (newColor.HasValue) await _playerRepository.SetPlayerColorAsync(player, newColor.Value);
                else await _playerRepository.RemovePlayerColorAsync(player);
            }
            else
            {
                if (newColor.HasValue) await _playerRepository.SetPlayerPersonaNameColorAsync(player, newColor.Value);
                else await _playerRepository.RemovePlayerPersonaNameColorAsync(player);
            }
        }

        private async Task SetPlayerAliasColorAsync(Player? player)
        {
            if (player == null || !player.HasAlias) return;

            var (confirmed, newColor) = _dialogService.ShowColorPicker($"Choose color for alias '{player.Alias}'", player.AliasColor);
            if (!confirmed) return;

            if (newColor.HasValue)
            {
                await _playerRepository.SetPlayerAliasColorAsync(player, newColor.Value);
            }
            else
            {
                await _playerRepository.RemovePlayerAliasColorAsync(player);
            }
        }

        private async Task SetPlayerIconAsync(Tuple<object, object>? parameters)
        {
            if (parameters?.Item1 is not Player player || parameters?.Item2 is not IconInfo iconInfo) return;
            var iconToSet = iconInfo.Name.Equals("None", StringComparison.OrdinalIgnoreCase) ? string.Empty : iconInfo.Name;
            await _playerRepository.SetPlayerIconAsync(player, iconToSet);
        }

        private void CopyPlayerToReport(Player? player)
        {
            if (player == null) return;
            ReportNickname = player.Name;
            ReportSteamId = player.SteamId2;
            
            // Auto-copy logic
            var template = _settingsService.CurrentSettings.ReportTemplate ?? string.Empty;
            var reportText = template
                .Replace("${ServerName}", SelectedServer ?? "N/A")
                .Replace("${PlayerName}", ReportNickname ?? "")
                .Replace("${SteamId}", ReportSteamId ?? "")
                .Replace("${Details}", ReportDetails ?? "");
            
            CopyToClipboard(reportText, "Report");
            StatusMessage = "Report copied to clipboard.";
            
            // We do NOT switch the tab anymore
            // SelectedTab = TabNames.Report; 
            
            // Reset status message after a delay
            Task.Delay(2000).ContinueWith(_ => StatusMessage = "Player Database & Reporting", TaskScheduler.FromCurrentSynchronizationContext());
        }

        [RelayCommand]
        private void CopyReport()
        {
            var template = _settingsService.CurrentSettings.ReportTemplate ?? string.Empty;
            var reportText = template
                .Replace("${ServerName}", SelectedServer ?? "N/A")
                .Replace("${PlayerName}", ReportNickname ?? "")
                .Replace("${SteamId}", ReportSteamId ?? "")
                .Replace("${Details}", ReportDetails ?? "");
            CopyToClipboard(reportText, "Report");
        }

        private void CopyReportForServer(object? parameter)
        {
            if (parameter is not Tuple<object, object> tuple) return;
            if (tuple.Item1 is not Player player || tuple.Item2 is not string serverName) return;
            var template = _settingsService.CurrentSettings.ReportTemplate ?? string.Empty;
            var reportText = template
                .Replace("${ServerName}", serverName ?? "N/A")
                .Replace("${PlayerName}", player.Name ?? "")
                .Replace("${SteamId}", player.SteamId2 ?? "")
                .Replace("${Details}", "");
            CopyToClipboard(reportText, "Report");
        }

        private async Task SearchDatabaseAsync()
        {
            string? steamId64Term = null;
            if (DbSearchTerm.StartsWith("STEAM_"))
            {
                steamId64Term = _playerListParser.SteamId2To64(DbSearchTerm);
            }
            var results = await _playerRepository.SearchPlayersAsync(DbSearchTerm, steamId64Term, DbSearchExactMatch);
            DbSearchResults.Clear();
            foreach (var player in results) DbSearchResults.Add(player);
            if (results.Count == 0) SystemSounds.Beep.Play();
        }

        [RelayCommand]
        private void ShowSettingsDialog() => _dialogService.ShowSettingsDialog();

        [RelayCommand]
        private void SendConsole() => SendConsoleMessage();

        [RelayCommand]
        private void ToggleConsoleConnection() => ToggleConsoleConnectionInternal();

        private void OnPlayersUpdated(object? sender, List<Player> players)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                foreach (var player in _allPlayers)
                {
                    player.IsBusyChanged -= OnPlayerIsBusyChanged;
                }

                _allPlayers.Clear();
                foreach (var player in players)
                {
                    player.IsBusyChanged += OnPlayerIsBusyChanged;
                    _allPlayers.Add(player);
                }
                FilterPlayers(_cts.Token); // Initial filter
            });
        }

        private void OnPlayerIsBusyChanged()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                (CreatePlayerCardImageCommand as IRelayCommand)?.NotifyCanExecuteChanged();
                UpdateSinglePlayerVacStatusCommand.NotifyCanExecuteChanged();
            });
        }

        private async void OnClipboardChanged(object? sender, string clipboardText)
        {
            if (Interlocked.Exchange(ref _isProcessingClipboard, 1) == 1) return;
            try
            {
                if (!IsMonitoring || !_playerListParser.IsValidPlayerListFormat(clipboardText)) return;
                StatusMessage = "Processing...";
                await _playerRepository.ProcessClipboardDataAsync(clipboardText, new Progress<string>(msg => StatusMessage = msg));
            }
            finally
            {
                StatusMessage = "Player Database & Reporting";
                Interlocked.Exchange(ref _isProcessingClipboard, 0);
            }
        }

        private async void FilterPlayers(CancellationToken token)
        {
            try
            {
                var filtered = await Task.Run(() =>
                {
                    if (string.IsNullOrWhiteSpace(SearchText))
                        return _allPlayers.ToList();

                    return _allPlayers.Where(p =>
                        (p.Name?.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ?? false) ||
                        (p.PersonaName?.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ?? false) ||
                        (p.Alias?.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ?? false) ||
                        (p.SteamId2?.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ?? false) ||
                        (p.SteamId64?.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ?? false))
                        .ToList();
                }, token);

                if (token.IsCancellationRequested) return;

                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    FilteredPlayers.Clear();
                    foreach (var player in filtered) FilteredPlayers.Add(player);

                    OnPropertyChanged(nameof(PlayerCount));
                    OnPropertyChanged(nameof(PlayersTabHeader));
                });
            }
            catch (OperationCanceledException)
            {
                // Task was cancelled, which is normal.
            }
        }

        private void LoadServers()
        {
            ServerList.Clear();
            var servers = _settingsService.CurrentSettings.Servers;
            if (servers != null)
            {
                foreach (var server in servers) if (!string.IsNullOrEmpty(server)) ServerList.Add(server);
            }
            SelectedServer = ServerList.FirstOrDefault();
        }

        private void OnSettingsChanged(object? sender, AppSettings newSettings)
        {
            LoadServers();
            if (IsConsoleConnected)
            {
                _udpConsoleService.StopListening();
                _udpConsoleService.StartListening(newSettings.UdpListenPort);
            }
            OnPropertyChanged(nameof(CurrentSettings));
            UpdateSinglePlayerVacStatusCommand.NotifyCanExecuteChanged();
        }

        private void ToggleConsoleConnectionInternal()
        {
            if (IsConsoleConnected)
            {
                _udpConsoleService.StopListening();
                IsConsoleConnected = false;
            }
            else
            {
                _udpConsoleService.StartListening(_settingsService.CurrentSettings.UdpListenPort);
                IsConsoleConnected = true;
            }
        }

        private void SendConsoleMessage()
        {
            var sendAddress = _settingsService.CurrentSettings.UdpSendAddress;
            if (string.IsNullOrEmpty(sendAddress)) return;
            _ = _udpConsoleService.SendMessage(ConsoleInputText, sendAddress, _settingsService.CurrentSettings.UdpSendPort);
            ConsoleInputText = string.Empty;
        }

        private void RefreshPlayers()
        {
            var currentPlayers = _playerRepository.GetCurrentPlayers();
            _allPlayers.Clear();
            foreach (var player in currentPlayers) _allPlayers.Add(player);
            FilterPlayers(_cts.Token);
        }

        private void StartMonitoring()
        {
            try { _clipboardService.StartListening(); IsMonitoring = true; }
            catch (Exception) { IsMonitoring = false; }
        }

        private void StopMonitoring()
        {
            try { _clipboardService.StopListening(); IsMonitoring = false; }
            catch (Exception) { }
        }

        private void CopyToClipboard(string? text, string type)
        {
            if (string.IsNullOrEmpty(text)) return;
            try { Clipboard.SetText(text); }
            catch (Exception ex) { _dialogService.ShowMessageDialog("Error", $"Failed to copy {type}: {ex.Message}"); }
        }

        private void CopyPlayerId(Player? player)
        {
            if (player == null) return;
            if (string.IsNullOrEmpty(player.SteamId64))
            {
                _dialogService.ShowMessageDialog("Info", "This player does not have a SteamID64 to copy.");
                return;
            }
            CopyToClipboard(player.SteamId64, "SteamID64");
        }

        private void CopyPlayerSteamId2(Player? player)
        {
            if (player == null) return;
            // Fix: Use ParsedSteamId2 if available, otherwise fallback to SteamId2 property
            var steamId2 = !string.IsNullOrEmpty(player.ParsedSteamId2) ? player.ParsedSteamId2 : player.SteamId2;
            
            if (string.IsNullOrEmpty(steamId2))
            {
                _dialogService.ShowMessageDialog("Info", "This player does not have a SteamID to copy.");
                return;
            }
            CopyToClipboard(steamId2, "SteamID");
        }
        private void CopyPlayerName(Player? player)
        {
            if (player == null) return;
            if (string.IsNullOrEmpty(player.Name))
            {
                _dialogService.ShowMessageDialog("Info", "This player does not have a name to copy.");
                return;
            }
            CopyToClipboard(player.Name, "Name");
        }
        private void CopyPlayerAlias(Player? player)
        {
            if (player == null) return;
            if (string.IsNullOrEmpty(player.Alias))
            {
                _dialogService.ShowMessageDialog("Info", "This player does not have an alias to copy.");
                return;
            }
            CopyToClipboard(player.Alias, "Alias");
        }
        private void CopyPlayerPersonaName(Player? player)
        {
            if (player == null) return;
            if (string.IsNullOrEmpty(player.PersonaName))
            {
                _dialogService.ShowMessageDialog("Info", "This player does not have a PersonaName to copy.");
                return;
            }
            CopyToClipboard(player.PersonaName, "PersonaName");
        }
        
        private async Task UpdateSinglePlayerVacStatusAsync(Player? player)
        {
            if (player == null) return;

            long currentTime = DateTimeOffset.Now.ToUnixTimeSeconds();
            long fiveMinutesInSeconds = 5 * 60;
            if (player.LastVacCheck > 0 && (currentTime - player.LastVacCheck) < fiveMinutesInSeconds)
            {
                _dialogService.ShowMessageDialog("Info", "Update not required.");
                return;
            }

            player.IsCheckingBans = true;
            StatusMessage = "Updating VAC Status...";
            try
            {
                await _playerRepository.EnrichSinglePlayerAsync(player, _cts.Token);
                StatusMessage = "VAC status updated.";
                await Task.Delay(2000); // Shorter delay
            }
            catch (OperationCanceledException)
            {
                StatusMessage = "VAC status update cancelled.";
                await Task.Delay(2000);
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error updating VAC status: {ex.Message}";
                _dialogService.ShowMessageDialog("Error", StatusMessage);
            }
            finally
            {
                player.IsCheckingBans = false;
                StatusMessage = "Player Database & Reporting";
                UpdateSinglePlayerVacStatusCommand.NotifyCanExecuteChanged();
    
                ((RelayCommand<Player?>)CopyPlayerIdCommand).NotifyCanExecuteChanged();
                ((RelayCommand<Player?>)CopyPlayerSteamId2Command).NotifyCanExecuteChanged();
                ((RelayCommand<Player?>)CopyPlayerNameCommand).NotifyCanExecuteChanged();
                ((RelayCommand<Player?>)CopyPlayerAliasCommand).NotifyCanExecuteChanged();
            }
        }

        private bool CanUpdateSinglePlayerVacStatus(Player? player)
        {
            return player != null && !player.IsBusy && !string.IsNullOrEmpty(_apiKeyService.GetApiKey());
        }
        
        public void Dispose()
        {
            _cts.Cancel();
            _cts.Dispose();
            _filterCts?.Cancel();
            _filterCts?.Dispose();
            _settingsService.SettingsChanged -= OnSettingsChanged;
            _playerRepository.PlayersUpdated -= OnPlayersUpdated;
            _clipboardService.ClipboardChanged -= OnClipboardChanged;

            foreach (var player in _allPlayers)
            {
                player.IsBusyChanged -= OnPlayerIsBusyChanged;
            }

            _clipboardService?.Dispose();
            _playerRepository?.Dispose();
            _udpConsoleService?.Dispose();
        }
    }
}