using System.Text.Json.Serialization;

namespace GSRP.Daemon.Models
{
    public class AppSettings
    {
        [JsonPropertyName("udpListenPort")]
        public int UdpListenPort { get; set; } = 26000;

        [JsonPropertyName("enable_periodic_vac_check")]
        public bool EnablePeriodicVacCheck { get; set; } = false;

        [JsonPropertyName("report")]
        public string? ReportTemplate { get; set; }

        [JsonPropertyName("favoriteColors")]
        public string[] FavoriteColors { get; set; } = new string[] { "#ff4444", "#66ccff", "#ffd700", "#10b981", "#a855f7" };

        [JsonPropertyName("defaultGameColor")]
        public string DefaultGameColor { get; set; } = "#f4f4f5"; // Zinc 100

        [JsonPropertyName("defaultSteamColor")]
        public string DefaultSteamColor { get; set; } = "#60a5fa"; // Blue 400

        [JsonPropertyName("defaultAliasColor")]
        public string DefaultAliasColor { get; set; } = "#3b82f6"; // Blue 600
    }
}
