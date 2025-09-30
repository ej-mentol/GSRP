using System.Windows.Controls;
using System.Windows.Input;
using GSRP.ViewModels;
using System.Windows;
using System.Windows.Media;
using System.Collections.Specialized;

namespace GSRP.View.Views
{
    public partial class ConsoleView : UserControl
    {
        public ConsoleView()
        {
            InitializeComponent();
            Loaded += ConsoleView_Loaded;
        }

        private void ConsoleView_Loaded(object sender, RoutedEventArgs e)
        {
            if (DataContext is MainViewModel vm && vm.FilteredConsoleOutput.SourceCollection is INotifyCollectionChanged output)
            {
                output.CollectionChanged += (s, args) =>
                {
                    if (args.Action == NotifyCollectionChangedAction.Add)
                    {
                        Dispatcher.InvokeAsync(() =>
                        {
                            var scrollViewer = FindVisualChild<ScrollViewer>(this);
                            scrollViewer?.ScrollToEnd();
                        });
                    }
                };
            }
        }
        
        private static T? FindVisualChild<T>(DependencyObject? parent) where T : DependencyObject
        {
            if (parent == null) return null;

            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T t)
                {
                    return t;
                }
                
                var childOfChild = FindVisualChild<T>(child);
                if (childOfChild != null)
                {
                    return childOfChild;
                }
            }
            return null;
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