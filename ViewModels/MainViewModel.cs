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
        private readonly CancellationTokenSource _cts = new();

        public ObservableCollection<Player> Players { get; }
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
        private string _selectedTab = "Players";

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
        public ICommand SetPlayerIconAsyncCommand { get; }
        public ICommand OpenInBrowserCommand { get; }
        public ICommand CopyPlayerIdCommand { get; }
        public ICommand CopyPlayerNameCommand { get; }
        public ICommand CopyPlayerAliasCommand { get; }
        
        public ICommand CopyPlayerToReportCommand { get; }
        public ICommand CopyReportForServerCommand { get; }
        public IAsyncRelayCommand SearchDatabaseCommand { get; }

        public ICommand ClearSearchTextCommand { get; }
        public ICommand ClearDbSearchTextCommand { get; }

        public ISettingsService SettingsService => _settingsService;
        public AppSettings CurrentSettings => _settingsService.CurrentSettings;

        public MainViewModel(IPlayerRepository playerRepository, IClipboardService clipboardService, IPlayerListParser playerListParser, 
                             IUdpConsoleService udpConsoleService, ISettingsService settingsService, IDialogService dialogService, IIconService iconService)
        {
            _playerRepository = playerRepository;
            _clipboardService = clipboardService;
            _playerListParser = playerListParser;
            _udpConsoleService = udpConsoleService;
            _settingsService = settingsService;
            _dialogService = dialogService;
            _iconService = iconService;
            
            _iconService.ScanForIcons();
            AvailableIconInfos = _iconService.AvailableIconNames
                .Select(name => new IconInfo(name, _iconService.ResolveIconPath(name)))
                .ToList();

            Players = new ObservableCollection<Player>();
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

            UpdatePlayerAliasAsyncCommand = new AsyncRelayCommand<Player?>(UpdatePlayerAliasAsync);
            SetPlayerGameColorAsyncCommand = new AsyncRelayCommand<Player?>(p => SetPlayerColorAsync(p, ColorTarget.GameName));
            SetPlayerSteamColorAsyncCommand = new AsyncRelayCommand<Player?>(p => SetPlayerColorAsync(p, ColorTarget.SteamName));
            SetPlayerIconAsyncCommand = new AsyncRelayCommand<Tuple<object, object>?>(SetPlayerIconAsync);
            OpenInBrowserCommand = new RelayCommand<Player?>(OpenInBrowser);

            CopyPlayerIdCommand = new RelayCommand<Player?>(CopyPlayerId);
            CopyPlayerNameCommand = new RelayCommand<Player?>(CopyPlayerName);
            CopyPlayerAliasCommand = new RelayCommand<Player?>(CopyPlayerAlias);
            
            CopyPlayerToReportCommand = new RelayCommand<Player?>(CopyPlayerToReport);
            CopyReportForServerCommand = new RelayCommand<object>(CopyReportForServer);
            SearchDatabaseCommand = new AsyncRelayCommand(SearchDatabaseAsync, () => !string.IsNullOrWhiteSpace(DbSearchTerm));
            ClearSearchTextCommand = new RelayCommand(() => SearchText = string.Empty);
            ClearDbSearchTextCommand = new RelayCommand(() => DbSearchTerm = string.Empty);
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

        partial void OnSearchTextChanged(string value)
        {
            FilterPlayers();
        }

        [RelayCommand]
        private void SelectTab(string? tabName)
        {
            SelectedTab = tabName ?? "Players";
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
                if (newColor.HasValue)
                    await _playerRepository.SetPlayerColorAsync(player, newColor.Value);
                else
                    await _playerRepository.RemovePlayerColorAsync(player);
            }
            else // SteamName
            {
                if (newColor.HasValue)
                    await _playerRepository.SetPlayerPersonaNameColorAsync(player, newColor.Value);
                else
                    await _playerRepository.RemovePlayerPersonaNameColorAsync(player);
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
            SelectedTab = "Report";
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
                .Replace("${Details}", ""); // No details from context menu

            CopyToClipboard(reportText, "Report");
        }

        private async Task SearchDatabaseAsync()
        {
            string? steamId64Term = null;
            if (DbSearchTerm.StartsWith("STEAM_"))
            {
                steamId64Term = _playerListParser.SteamId2To64(DbSearchTerm);
            }
            // If the input is already a SteamID64, the LIKE search in the DB will catch it.

            var results = await _playerRepository.SearchPlayersAsync(DbSearchTerm, steamId64Term, DbSearchExactMatch);

            DbSearchResults.Clear();
            foreach (var player in results)
            {
                DbSearchResults.Add(player);
            }

            if (results.Count == 0)
            {
                SystemSounds.Beep.Play();
            }
        }

        [RelayCommand(IncludeCancelCommand = true)]
        private async Task RefreshDataAsync(CancellationToken token)
        {
            await _playerRepository.ForceEnrichCurrentPlayersAsync(token);
        }

        [RelayCommand]
        private void ShowSettingsDialog()
        {
            _dialogService.ShowSettingsDialog();
        }

        [RelayCommand]
        private void SendConsole()
        {
            SendConsoleMessage();
        }

        [RelayCommand]
        private void ToggleConsoleConnection()
        {
            ToggleConsoleConnectionInternal();
        }

        private void OnPlayersUpdated(object? sender, List<Player> players)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                Players.Clear();
                foreach (var player in players) Players.Add(player);
                FilterPlayers();
            });
        }

        private async void OnClipboardChanged(object? sender, string clipboardText)
        {
            if (IsMonitoring && _playerListParser.IsValidPlayerListFormat(clipboardText))
            {
                StatusMessage = "Processing...";
                try
                {
                    await _playerRepository.ProcessClipboardDataAsync(clipboardText,
                        new Progress<string>(msg => StatusMessage = msg));
                }
                finally
                {
                    StatusMessage = "Player Database & Reporting";
                }
            }
        }

        private void FilterPlayers()
        {
            FilteredPlayers.Clear();
            var source = string.IsNullOrWhiteSpace(SearchText) ? Players : Players.Where(p =>
                (p.Name?.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (p.PersonaName?.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (p.Alias?.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (p.SteamId2?.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (p.SteamId64?.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ?? false));

            foreach (var player in source) FilteredPlayers.Add(player);

            OnPropertyChanged(nameof(PlayerCount));
            OnPropertyChanged(nameof(PlayersTabHeader));
        }

        private void LoadServers()
        {
            ServerList.Clear();
            var servers = _settingsService.CurrentSettings.Servers;
            if (servers != null)
            {
                foreach (var server in servers)
                {
                    if (!string.IsNullOrEmpty(server))
                    {
                        ServerList.Add(server);
                    }
                }
            }
            SelectedServer = ServerList.FirstOrDefault();
        }

        private void OnSettingsChanged(object? sender, AppSettings newSettings)
        {
            // Reload settings that affect the MainViewModel
            LoadServers();

            // Re-apply UDP settings if the console is running
            if (IsConsoleConnected)
            {
                _udpConsoleService.StopListening();
                _udpConsoleService.StartListening(newSettings.UdpListenPort);
            }

            // Notify the UI that the entire settings object has changed, so any bindings update
            OnPropertyChanged(nameof(CurrentSettings));
        }

        private void ToggleConsoleConnectionInternal()
        {
            if (IsConsoleConnected)
            {
                _udpConsoleService.StartListening(_settingsService.CurrentSettings.UdpListenPort);
            }
            else
            {
                _udpConsoleService.StopListening();
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
            Players.Clear();
            foreach (var player in currentPlayers) Players.Add(player);
            FilterPlayers();
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
            try
            {
                ClipboardUtils.SetClipboardText(text);
            }
            catch (Exception ex)
            {
                _dialogService.ShowMessageDialog("Error", $"Failed to copy {type}: {ex.Message}");
            }
        }

        private void CopyPlayerId(Player? player) => CopyToClipboard(player?.SteamId64, "SteamID64");
        private void CopyPlayerName(Player? player) => CopyToClipboard(player?.Name, "Name");
        private void CopyPlayerAlias(Player? player) => CopyToClipboard(player?.Alias, "Alias");
        

        public void Dispose()
        {
            _cts.Cancel();
            _cts.Dispose();
            _settingsService.SettingsChanged -= OnSettingsChanged;
            _playerRepository.PlayersUpdated -= OnPlayersUpdated;
            _clipboardService.ClipboardChanged -= OnClipboardChanged;
            _clipboardService?.Dispose();
            _playerRepository?.Dispose();
            _udpConsoleService?.Dispose();
        }
    }
}
