using System.Windows;
using System.Windows.Media;
using GSRP.Views.Dialogs;

namespace GSRP.Services
{
    public class DialogService : IDialogService
    {
        private readonly ISettingsService _settingsService;
        private readonly IApiKeyService _apiKeyService;

        public DialogService(ISettingsService settingsService, IApiKeyService apiKeyService)
        {
            _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
            _apiKeyService = apiKeyService ?? throw new ArgumentNullException(nameof(apiKeyService));
        }

        public string? ShowInputDialog(string title, string prompt, string defaultValue = "")
        {
            var dialog = new InputDialog(title, prompt, defaultValue);
            dialog.Owner = Application.Current.MainWindow;
            var result = dialog.ShowDialog();
            return result == true ? dialog.InputText : null;
        }

        public bool ShowConfirmDialog(string title, string message)
        {
            var result = MessageBox.Show(message, title, MessageBoxButton.YesNo, MessageBoxImage.Question);
            return result == MessageBoxResult.Yes;
        }

        public void ShowMessageDialog(string title, string message)
        {
            MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information);
        }

        public (bool Confirmed, Color? Color) ShowColorPicker(string title, Color? currentColor = null)
        {
            var dialog = new ColorPickerDialog(title, currentColor);
            dialog.Owner = Application.Current.MainWindow;
            var result = dialog.ShowDialog();
            
            if (result == true)
            {
                return (true, dialog.SelectedColor);
            }
            
            return (false, null);
        }

        public void ShowSettingsDialog()
        {
            var dialog = new SettingsDialog(_settingsService, _apiKeyService)
            {
                Owner = Application.Current.MainWindow
            };
            dialog.ShowDialog();
        }
    }
}