using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Shell;
using System.Windows.Input;
using GSRP.Services;

namespace GSRP
{
    public partial class MainWindow : Window
    {
        private readonly ISettingsService _settingsService;

        public MainWindow(ViewModels.MainViewModel viewModel, ISettingsService settingsService)
        {
            InitializeComponent();
            DataContext = viewModel;
            _settingsService = settingsService;

            this.StateChanged += MainWindow_StateChanged;
            this.Closing += MainWindow_Closing;

            this.Height = _settingsService.CurrentSettings.WindowHeight;
            this.Width = _settingsService.CurrentSettings.WindowWidth;

            // Ensure the window is not positioned off-screen.
            var virtualScreenLeft = SystemParameters.VirtualScreenLeft;
            var virtualScreenTop = SystemParameters.VirtualScreenTop;
            var virtualScreenWidth = SystemParameters.VirtualScreenWidth;
            var virtualScreenHeight = SystemParameters.VirtualScreenHeight;

            var windowX = _settingsService.CurrentSettings.WindowX;
            var windowY = _settingsService.CurrentSettings.WindowY;

            if (windowX < virtualScreenLeft || windowX > virtualScreenLeft + virtualScreenWidth - this.Width)
            {
                windowX = (int)(virtualScreenLeft + (virtualScreenWidth - this.Width) / 2);
            }

            if (windowY < virtualScreenTop || windowY > virtualScreenTop + virtualScreenHeight - this.Height)
            {
                windowY = (int)(virtualScreenTop + (virtualScreenHeight - this.Height) / 2);
            }

            this.Left = windowX;
            this.Top = windowY;

            this.WindowState = _settingsService.CurrentSettings.WindowMaximized ? WindowState.Maximized : WindowState.Normal;
        }

        private void MainWindow_Closing(object? sender, CancelEventArgs e)
        {
            var settings = _settingsService.CurrentSettings;
            if (this.WindowState == WindowState.Normal)
            {
                settings.WindowHeight = (int)this.Height;
                settings.WindowWidth = (int)this.Width;
                settings.WindowX = (int)this.Left;
                settings.WindowY = (int)this.Top;
                settings.WindowMaximized = false;
            }
            else
            {
                settings.WindowHeight = (int)this.RestoreBounds.Height;
                settings.WindowWidth = (int)this.RestoreBounds.Width;
                settings.WindowX = (int)this.RestoreBounds.Left;
                settings.WindowY = (int)this.RestoreBounds.Top;
                settings.WindowMaximized = true;
            }
            _settingsService.SaveSettings();
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);

            var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
            if (hwnd == IntPtr.Zero) return;

            // Dark mode title bar
            int trueVal = 1;
            DwmSetWindowAttribute(hwnd, 20, ref trueVal, sizeof(int));

            // Rounded corners
            uint roundPref = 2;
            DwmSetWindowAttribute(hwnd, 33, ref roundPref, sizeof(uint));
        }

        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
            => SystemCommands.MinimizeWindow(this);

        private void MaximizeButton_Click(object sender, RoutedEventArgs e)
        {
            if (WindowState == WindowState.Maximized)
                SystemCommands.RestoreWindow(this);
            else
                SystemCommands.MaximizeWindow(this);
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
            => SystemCommands.CloseWindow(this);

        private void SearchBoxContextMenu_Opened(object sender, RoutedEventArgs e)
        {
            bool hasSelection = MainSearchBox.SelectionLength > 0;
            CutMenuItem.IsEnabled = hasSelection;
            CopyMenuItem.IsEnabled = hasSelection;
            DeleteMenuItem.IsEnabled = hasSelection;
            PasteMenuItem.IsEnabled = Clipboard.ContainsText();
        }

        private void CutMenuItem_Click(object sender, RoutedEventArgs e)
        {
            MainSearchBox.Cut();
        }

        private void CopyMenuItem_Click(object sender, RoutedEventArgs e)
        {
            MainSearchBox.Copy();
        }

        private void PasteMenuItem_Click(object sender, RoutedEventArgs e)
        {
            MainSearchBox.Paste();
        }

        private void DeleteMenuItem_Click(object sender, RoutedEventArgs e)
        {
            MainSearchBox.SelectedText = "";
        }

        private void SelectAllMenuItem_Click(object sender, RoutedEventArgs e)
        {
            MainSearchBox.SelectAll();
        }

        private void MainWindow_StateChanged(object? sender, EventArgs e)
        {
            if (WindowState == WindowState.Maximized)
            {
                MaximizeIcon.Text = "🗗";
            }
            else
            {
                MaximizeIcon.Text = "🗖";
            }
        }

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref uint attrValue, int attrSize);
    }
}