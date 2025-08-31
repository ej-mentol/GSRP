using GSRP.Models;
using GSRP.ViewModels;
using System;
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

        private void SubMenuItem_Click(object sender, RoutedEventArgs e)
        {
            // Find the root ContextMenu by walking up the logical and visual trees.
            DependencyObject? current = sender as DependencyObject;
            while (current != null)
            {
                // Try logical parent first, then visual parent.
                current = LogicalTreeHelper.GetParent(current) ?? VisualTreeHelper.GetParent(current);
                if (current is ContextMenu contextMenu)
                {
                    contextMenu.IsOpen = false;
                    break;
                }
            }
        }
    }
}
