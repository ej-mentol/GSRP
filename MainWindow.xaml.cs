using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Shell;
using System.Windows.Input;

namespace GSRP
{
    public partial class MainWindow : Window
    {
        public MainWindow(ViewModels.MainViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
            StateChanged += MainWindow_StateChanged;
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