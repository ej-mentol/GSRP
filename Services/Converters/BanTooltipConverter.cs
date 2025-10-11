using System;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Data;

namespace GSRP.Converters
{
    public class BanTooltipConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values == null || values.Length < 5)
                return string.Empty;

            if (values.Any(v => v == DependencyProperty.UnsetValue))
            {
                return string.Empty;
            }

            var banDate = System.Convert.ToInt64(values[0]);
            var numberOfVACBans = System.Convert.ToInt32(values[1]);
            var numberOfGameBans = System.Convert.ToInt32(values[2]);
            var isCommunityBanned = System.Convert.ToBoolean(values[3]);
            var economyBan = values[4] as string;

            bool hasAnyBan = numberOfVACBans > 0 || numberOfGameBans > 0 || isCommunityBanned || (!string.IsNullOrEmpty(economyBan) && economyBan != "none");

            if (!hasAnyBan)
            {
                return string.Empty;
            }

            var sb = new StringBuilder();

            if (banDate > 0)
            {
                var banDateTime = DateTimeOffset.FromUnixTimeSeconds(banDate);
                var daysSince = (DateTimeOffset.UtcNow - banDateTime).Days;
                sb.AppendLine($"Days since last ban: {daysSince}");
            }
            else if ((numberOfVACBans > 0 || numberOfGameBans > 0) && banDate == 0)
            {
                sb.AppendLine("Please update ban status");
            }

            if (numberOfVACBans > 0)
            {
                sb.AppendLine($"Number of VAC bans: {numberOfVACBans}");
            }

            if (numberOfGameBans > 0)
            {
                sb.AppendLine($"Game bans on record: {numberOfGameBans}");
            }

            if (isCommunityBanned)
            {
                sb.AppendLine("Community banned: Yes");
            }

            if (!string.IsNullOrEmpty(economyBan) && economyBan != "none")
            {
                sb.AppendLine($"Economy ban: {economyBan}");
            }

            return sb.ToString().Trim();
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}