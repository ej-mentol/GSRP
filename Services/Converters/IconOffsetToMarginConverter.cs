using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace GSRP.Converters
{
    public class IconOffsetToMarginConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int offset)
            {                
                var clampedOffset = Math.Max(0, Math.Min(8, offset));
                return new Thickness(-clampedOffset);
            }
            return new Thickness(-2); 
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
