using GSRP.Models;
using GSRP.ViewModels;
using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace GSRP.Views.Controls
{
    public partial class PlayerCard : UserControl
    {
        public PlayerCard()
        {
            InitializeComponent();
            Loaded += PlayerCard_Loaded;
        }

        private void PlayerCard_Loaded(object sender, RoutedEventArgs e)
        {
            // Create the dynamic submenu for "Copy as Report"
            if (Tag is MainViewModel viewModel && CopyAsReportMenuItem != null)
            {
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
                }
                else
                {
                    // Add a disabled item to inform the user that the list is empty
                    CopyAsReportMenuItem.Items.Add(new MenuItem
                    {
                        Header = "No servers configured",
                        IsEnabled = false,
                        Style = (Style)FindResource("ThemedMenuItem")
                    });
                }
            }
        }

        private void SubMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not MenuItem { Header: string serverName } clickedMenuItem) return;
            if (DataContext is not Player player) return;
            if (Tag is not MainViewModel viewModel) return;

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
            if (DataContext is Player player && Tag is MainViewModel viewModel)
            {
                if (viewModel.UpdateSinglePlayerVacStatusCommand.CanExecute(player))
                {
                    viewModel.UpdateSinglePlayerVacStatusCommand.Execute(player);
                }
            }
        }
    }
}
