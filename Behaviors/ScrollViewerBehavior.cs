using System.Windows;
using System.Windows.Controls;

namespace GSRP.Behaviors
{
    public static class ScrollViewerBehavior
    {
        public static readonly DependencyProperty VerticalOffsetProperty =
            DependencyProperty.RegisterAttached("VerticalOffset", typeof(double), typeof(ScrollViewerBehavior),
                new PropertyMetadata(0.0, OnVerticalOffsetChanged));

        private static void OnVerticalOffsetChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ScrollViewer scrollViewer)
                scrollViewer.ScrollToVerticalOffset((double)e.NewValue);
        }

        public static void SetVerticalOffset(DependencyObject obj, double value) => obj.SetValue(VerticalOffsetProperty, value);
        public static double GetVerticalOffset(DependencyObject obj) => (double)obj.GetValue(VerticalOffsetProperty);
    }
}
