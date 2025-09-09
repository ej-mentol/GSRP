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
            if (sender is ContextMenu menu)
            {
                var copyAsReportMenuItem = menu.Items.OfType<MenuItem>().FirstOrDefault(item => item.Name == "CopyAsReportMenuItem");

                if (copyAsReportMenuItem != null && DataContext is Player player && FindParentViewModel<MainViewModel>(this) is MainViewModel viewModel)
                {
                    copyAsReportMenuItem.Items.Clear();
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
                            copyAsReportMenuItem.Items.Add(subMenuItem);
                        }
                    }
                    else
                    {
                        copyAsReportMenuItem.Items.Add(new MenuItem
                        {
                            Header = "No servers configured",
                            IsEnabled = false,
                            Style = (Style)FindResource("ThemedMenuItem")
                        });
                    }
                }
            }
        }

        private void SubMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not MenuItem { Header: string serverName } clickedMenuItem) return;
            if (DataContext is not Player player) return;
            if (FindParentViewModel<MainViewModel>(this) is not MainViewModel viewModel) return;

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