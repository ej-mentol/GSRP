using System.Windows.Media;

namespace GSRP.Services
{
    public enum ColorTarget { GameName, SteamName };

    public interface IDialogService
    {
        string? ShowInputDialog(string title, string prompt, string defaultValue = "");
        bool ShowConfirmDialog(string title, string message);
        void ShowMessageDialog(string title, string message);
        (bool Confirmed, Color? Color) ShowColorPicker(string title, Color? currentColor = null);
        void ShowSettingsDialog();
    }
}