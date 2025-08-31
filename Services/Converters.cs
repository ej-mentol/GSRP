using GSRP.Models;
using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace GSRP.Converters
{
    public class StringToImageSourceConverter : IValueConverter
    {
        public object? Convert(object value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is string path && !string.IsNullOrEmpty(path) && File.Exists(path))
            {
                try
                {
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.UriSource = new Uri(path, UriKind.Absolute);
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.EndInit();
                    return bitmap;
                }
                catch
                {
                    return null;
                }
            }
            return null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class PathToImageConverter : IValueConverter
    {
        private static readonly StringToImageSourceConverter _imageSourceConverter = new StringToImageSourceConverter();

        public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string path && !string.IsNullOrEmpty(path))
            {
                // var imageSource = _imageSourceConverter.Convert(path, typeof(ImageSource), null, culture);
                var imageSource = _imageSourceConverter.Convert(path, typeof(ImageSource), (object?)null, culture);
                if (imageSource != null)
                {
                    return new Image
                    {
                        Source = (ImageSource)imageSource,
                        Width = 16,
                        Height = 16,
                        Stretch = Stretch.Uniform
                    };
                }
            }
            return null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class EqualityToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var valueStr = value?.ToString();
            var parameterStr = parameter?.ToString();
            return string.Equals(valueStr, parameterStr, StringComparison.InvariantCultureIgnoreCase)
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class TupleConverter : IMultiValueConverter
    {
        public object? Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            // The values array contains the player and the icon name string
            if (values != null && values.Length == 2)
            {
                return new Tuple<object, object>(values[0], values[1]);
            }
            return null;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }    

    public class IconCornerToHorizontalAlignmentConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is IconCorner corner)
            {
                switch (corner)
                {
                    case IconCorner.TopRight:
                    case IconCorner.BottomRight:
                        return HorizontalAlignment.Right;
                    case IconCorner.TopLeft:
                    case IconCorner.BottomLeft:
                    default:
                        return HorizontalAlignment.Left;
                }
            }
            return HorizontalAlignment.Right;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class IconCornerToVerticalAlignmentConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is IconCorner corner)
            {
                switch (corner)
                {
                    case IconCorner.BottomLeft:
                    case IconCorner.BottomRight:
                        return VerticalAlignment.Bottom;
                    case IconCorner.TopLeft:
                    case IconCorner.TopRight:
                    default:
                        return VerticalAlignment.Top;
                }
            }
            return VerticalAlignment.Bottom;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}