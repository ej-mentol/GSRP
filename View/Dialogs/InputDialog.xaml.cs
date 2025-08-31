using System.Windows;
using System.Windows.Input;

namespace GSRP.Views.Dialogs
{
    public partial class InputDialog : Window
    {
        public string InputText { get; private set; } = string.Empty;

        public InputDialog(string title, string prompt, string defaultValue = "")
        {
            InitializeComponent();
            TitleText.Text = title;
            PromptText.Text = prompt;
            InputTextBox.Text = defaultValue;
            InputText = defaultValue;

            // Set focus and select all text
            Loaded += (s, e) =>
            {
                InputTextBox.Focus();
                InputTextBox.SelectAll();
            };

            // Handle Enter key in TextBox
            InputTextBox.KeyDown += InputTextBox_KeyDown;
        }

        private void InputTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                OkButton_Click(sender, new RoutedEventArgs());
                e.Handled = true;
            }
        }

        private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            try
            {
                DragMove();
            }
            catch (System.InvalidOperationException)
            {
                // Handle case where mouse is not down when this is called
            }
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            InputText = InputTextBox.Text ?? string.Empty;
            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}