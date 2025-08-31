using GSRP.ViewModels;
using System.Windows.Controls;
using System.Windows.Input;

namespace GSRP.View.Views
{
    public partial class DatabaseSearchView : UserControl
    {
        public DatabaseSearchView()
        {
            InitializeComponent();
        }

        private void SearchBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                if (DataContext is MainViewModel vm && vm.SearchDatabaseCommand.CanExecute(null))
                {
                    vm.SearchDatabaseCommand.Execute(null);
                }
            }
        }
    }
}
