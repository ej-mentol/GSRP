using Microsoft.Xaml.Behaviors;
using System;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;

namespace GSRP.Behaviors
{
    public class SmoothScrollBehavior : Behavior<ScrollViewer>
    {
        public double Speed { get; set; } = 0.5;
        public int Duration { get; set; } = 150;

        protected override void OnAttached()
        {
            base.OnAttached();
            AssociatedObject.PreviewMouseWheel += OnPreviewMouseWheel;
        }

        protected override void OnDetaching()
        {
            base.OnDetaching();
            AssociatedObject.PreviewMouseWheel -= OnPreviewMouseWheel;
        }

        private void OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (AssociatedObject == null) return;

            double targetOffset = AssociatedObject.VerticalOffset - (e.Delta * Speed);

            var animation = new DoubleAnimation
            {
                From = AssociatedObject.VerticalOffset,
                To = targetOffset,
                Duration = TimeSpan.FromMilliseconds(Duration),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };

            var storyboard = new Storyboard();
            storyboard.Children.Add(animation);
            Storyboard.SetTarget(animation, AssociatedObject);
            Storyboard.SetTargetProperty(animation, new System.Windows.PropertyPath(ScrollViewerBehavior.VerticalOffsetProperty));

            storyboard.Begin();

            e.Handled = true;
        }
    }
}
