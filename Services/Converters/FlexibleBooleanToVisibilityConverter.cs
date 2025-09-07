using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace GSRP.Converters
{
    [ValueConversion(typeof(bool), typeof(Visibility))]
    public class FlexibleBooleanToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool boolValue = false;
            if (value is bool b)
            {
                boolValue = b;
            }

            string? parameterString = parameter as string;
            if (parameterString != null && (parameterString.Equals("inverse", StringComparison.OrdinalIgnoreCase) || parameterString.Equals("invert", StringComparison.OrdinalIgnoreCase)))
            {
                boolValue = !boolValue;
            }

            return boolValue ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
