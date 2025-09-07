using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace GSRP.Converters
{
    public class IsNullOrEmptyToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string? valueStr = value as string;
            string? parameterStr = parameter as string;

            bool isHidden;

            if (string.IsNullOrEmpty(valueStr))
            {
                isHidden = true;
            }
            else if (parameterStr != null && valueStr.Equals(parameterStr, StringComparison.OrdinalIgnoreCase))
            {
                isHidden = true;
            }
            else
            {
                isHidden = false;
            }

            // Invert the logic if "inverse" or "invert" is passed as a parameter.
            // This is a bit of a hack since we also use parameter for the string to compare against.
            // A better implementation would use a separate property on the converter.
            // For now, we check for a specific keyword.
            if (parameterStr != null && (parameterStr.Equals("inverse", StringComparison.OrdinalIgnoreCase) || parameterStr.Equals("invert", StringComparison.OrdinalIgnoreCase)))
            {
                 // This part of the logic is tricky. If the parameter is "inverse", we can't also use it for comparison.
                 // Let's simplify. The main use case is to hide if null/empty, or show if null/empty.
                 // The 'none' check is a special case.

                 // Let's stick to the original logic and create a new converter for the inverse case.
                 // Reverting to original logic for clarity.

                isHidden = string.IsNullOrEmpty(valueStr);
                if (parameterStr != null && valueStr != null && valueStr.Equals(parameterStr, StringComparison.OrdinalIgnoreCase))
                {
                    isHidden = true;
                }
            }

            return isHidden ? Visibility.Collapsed : Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
