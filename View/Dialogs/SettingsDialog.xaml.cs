using GSRP.Models;
using GSRP.Services;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace GSRP.Views.Dialogs
{
    public partial class SettingsDialog : Window
    {
        private readonly ISettingsService _settingsService;
        private readonly IApiKeyService _apiKeyService;
        private readonly AppSettings _settings; // A copy of settings to edit, to support cancellation.

        public SettingsDialog(ISettingsService settingsService, IApiKeyService apiKeyService)
        {
            InitializeComponent();
            _settingsService = settingsService;
            _apiKeyService = apiKeyService ?? throw new ArgumentNullException(nameof(apiKeyService));

            // Work on a copy of the settings
            _settings = new AppSettings(_settingsService.CurrentSettings);
            
            LoadSettingsIntoUI();

            IconOffsetTextBox.PreviewTextInput += (s, e) =>
            {
                e.Handled = !char.IsDigit(e.Text, 0);
            };
            
            IconOffsetTextBox.LostFocus += (s, e) =>
            {
                if (int.TryParse(IconOffsetTextBox.Text, out int value))
                {
                    if (value > 8) IconOffsetTextBox.Text = "8";
                    if (value < 0) IconOffsetTextBox.Text = "0";
                }
                else
                {
                    IconOffsetTextBox.Text = "2";
                }
            };
        }

        private void LoadSettingsIntoUI()
        {
            // Load data from the settings copy into the UI controls
            ServersListBox.ItemsSource = new ObservableCollection<string?>(_settings.Servers ?? new List<string?>());
            ReportTemplateTextBox.Text = _settings.ReportTemplate;
            ScreenshotsFolderTextBox.Text = _settings.ScreenshotsPath;
            VideosFolderTextBox.Text = _settings.VideosPath;
            IconCornerComboBox.SelectedItem = _settings.IconPlacement;
            UdpListenPortTextBox.Text = _settings.UdpListenPort.ToString();
            UdpSendPortTextBox.Text = _settings.UdpSendPort.ToString();
            UdpSendAddressTextBox.Text = _settings.UdpSendAddress;
            IconOffsetTextBox.Text = _settings.IconOffset.ToString();
            EnablePeriodicVacCheckCheckBox.IsChecked = _settings.EnablePeriodicVacCheck;

            UpdateApiKeyStatus();
        }

        private void SaveSettingsFromUI()
        {
            // Save settings from UI controls back to the settings copy object
            _settings.ReportTemplate = ReportTemplateTextBox.Text;
            _settings.Servers = new List<string?>(ServersListBox.Items.Cast<string>());
            _settings.ScreenshotsPath = ScreenshotsFolderTextBox.Text;
            _settings.VideosPath = VideosFolderTextBox.Text;
            _settings.IconPlacement = (IconCorner)IconCornerComboBox.SelectedItem;

            if (int.TryParse(UdpListenPortTextBox.Text, out int listenPort))
            {
                _settings.UdpListenPort = listenPort;
            }
            if (int.TryParse(UdpSendPortTextBox.Text, out int sendPort))
            {
                _settings.UdpSendPort = sendPort;
            }
            _settings.UdpSendAddress = UdpSendAddressTextBox.Text;

            if (int.TryParse(IconOffsetTextBox.Text, out int iconOffset))
            {
                _settings.IconOffset = iconOffset;
            }
            else
            {
                _settings.IconOffset = 2; // Default value if parsing fails
            }

            _settings.EnablePeriodicVacCheck = EnablePeriodicVacCheckCheckBox.IsChecked ?? false;

            // Now, apply the changes from the copy to the actual settings and save.
            _settingsService.CurrentSettings.CopyFrom(_settings);
            _settingsService.SaveSettings();
        }

        private void UpdateApiKeyStatus()
        {
            if (_apiKeyService.HasApiKey())
            {
                ApiKeyStatusText.Text = "API Key Saved";
                ApiKeyStatusText.Foreground = new SolidColorBrush(Color.FromRgb(34, 197, 94)); // Green
                DeleteApiKeyButton.IsEnabled = true;
            }
            else
            {
                ApiKeyStatusText.Text = "No API Key Saved";
                ApiKeyStatusText.Foreground = Brushes.Gray;
                DeleteApiKeyButton.IsEnabled = false;
            }
        }

        // --- Event Handlers ---

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            SaveSettingsFromUI();
            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void ChangeApiKeyButton_Click(object sender, RoutedEventArgs e)
        {
            // The current key is intentionally not passed to the dialog for security.
            var inputDialog = new InputDialog("Enter Steam Web API Key", "API Key:", "");
            inputDialog.Owner = this;

            if (inputDialog.ShowDialog() == true)
            {
                var newApiKey = inputDialog.InputText;
                if (string.IsNullOrWhiteSpace(newApiKey))
                {
                    _apiKeyService.ClearApiKey();
                }
                else if (newApiKey.Length != 32)
                {
                    MessageBox.Show("Steam API key should be 32 characters long.", "Invalid API Key",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                else
                {
                    _apiKeyService.SetApiKey(newApiKey);
                }
                UpdateApiKeyStatus();
            }
        }

        private void DeleteApiKeyButton_Click(object sender, RoutedEventArgs e)
        {
            _apiKeyService.ClearApiKey();
            UpdateApiKeyStatus();
        }

        private void AddServerButton_Click(object sender, RoutedEventArgs e)
        {
            var serverName = ServerNameTextBox.Text.Trim();
            if (!string.IsNullOrEmpty(serverName) && !ServersListBox.Items.Contains(serverName))
            {
                ((ObservableCollection<string?>)ServersListBox.ItemsSource).Add(serverName);
                ServerNameTextBox.Clear();
            }
        }

        private void RemoveServerButton_Click(object sender, RoutedEventArgs e)
        {
            if (ServersListBox.SelectedItem is string selectedServer)
            {
                ((ObservableCollection<string?>)ServersListBox.ItemsSource).Remove(selectedServer);
            }
        }

        private void BrowseScreenshotsFolder_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFolderDialog { Title = "Select Screenshots Folder" };
            if (dialog.ShowDialog() == true)
            {
                ScreenshotsFolderTextBox.Text = dialog.FolderName;
            }
        }

        private void BrowseVideosFolder_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFolderDialog { Title = "Select Videos Folder" };
            if (dialog.ShowDialog() == true)
            {
                VideosFolderTextBox.Text = dialog.FolderName;
            }
        }

        private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            try { DragMove(); } catch (InvalidOperationException) { }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
