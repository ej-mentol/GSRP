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
                // Ограничиваем смещение от 0 до 8px для безопасности
                var clampedOffset = Math.Max(0, Math.Min(8, offset));
                return new Thickness(-clampedOffset);
            }
            return new Thickness(-2); // значение по умолчанию
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
