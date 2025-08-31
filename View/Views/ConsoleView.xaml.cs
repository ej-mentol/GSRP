using System.Windows.Controls;
using System.Windows.Input;

namespace GSRP.View.Views
{
    public partial class ConsoleView : UserControl
    {
        public ConsoleView()
        {
            InitializeComponent();
        }

        private void ConsoleInput_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                if (DataContext is ViewModels.MainViewModel vm && vm.SendConsoleCommand.CanExecute(null))
                {
                    vm.SendConsoleCommand.Execute(null);
                }
            }
        }
    }
}
