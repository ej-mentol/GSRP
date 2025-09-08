using GSRP.Models;
using GSRP.ViewModels;
using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace GSRP.Views.Controls
{
    public partial class PlayerCard : UserControl
    {
        public PlayerCard()
        {
            InitializeComponent();
        }

        private void ContextMenu_ContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            var viewModel = FindParentViewModel<MainViewModel>(this);
            if (viewModel == null || CopyAsReportMenuItem == null) return;

            CopyAsReportMenuItem.Items.Clear();

            if (viewModel.ServerList.Any())
            {
                foreach (var server in viewModel.ServerList)
                {
                    var subMenuItem = new MenuItem
                    {
                        Header = server,
                        Style = (Style)FindResource("ThemedMenuItem")
                    };
                    subMenuItem.Click += SubMenuItem_Click;
                    CopyAsReportMenuItem.Items.Add(subMenuItem);
                }
                CopyAsReportMenuItem.IsEnabled = true;
            }
            else
            {
                CopyAsReportMenuItem.Items.Add(new MenuItem
                {
                    Header = "No servers configured",
                    IsEnabled = false,
                    Style = (Style)FindResource("ThemedMenuItem")
                });
                CopyAsReportMenuItem.IsEnabled = false;
            }
        }

        private void SubMenuItem_Click(object sender, RoutedEventArgs e)
        {
            var viewModel = FindParentViewModel<MainViewModel>(this);
            if (sender is not MenuItem { Header: string serverName } clickedMenuItem) return;
            if (DataContext is not Player player) return;
            if (viewModel == null) return;

            var parameter = new Tuple<object, object>(player, serverName);
            if (viewModel.CopyReportForServerCommand.CanExecute(parameter))
            {
                viewModel.CopyReportForServerCommand.Execute(parameter);
            }
        }

        private void MenuButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.ContextMenu != null)
            {
                button.ContextMenu.PlacementTarget = button;
                button.ContextMenu.IsOpen = true;
            }
        }

        private void UpdateVacStatus_Click(object sender, RoutedEventArgs e)
        {
            var viewModel = FindParentViewModel<MainViewModel>(this);
            if (DataContext is Player player && viewModel != null)
            {
                if (viewModel.UpdateSinglePlayerVacStatusCommand.CanExecute(player))
                {
                    viewModel.UpdateSinglePlayerVacStatusCommand.Execute(player);
                }
            }
        }

        private static T? FindParentViewModel<T>(DependencyObject child) where T : class
        {
            DependencyObject parentObject = VisualTreeHelper.GetParent(child);
            if (parentObject == null) return null;

            if (parentObject is FrameworkElement parent && parent.DataContext is T viewModel)
            {
                return viewModel;
            }
            
            return FindParentViewModel<T>(parentObject);
        }
    }
}