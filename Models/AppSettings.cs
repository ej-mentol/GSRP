using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace GSRP.Models
{
    public partial class AppSettings
    {
        public AppSettings() { }

        // Copy constructor
        public AppSettings(AppSettings other)
        {
            WindowWidth = other.WindowWidth;
            WindowHeight = other.WindowHeight;
            WindowX = other.WindowX;
            WindowY = other.WindowY;
            WindowMaximized = other.WindowMaximized;
            UdpListenPort = other.UdpListenPort;
            UdpSendPort = other.UdpSendPort;
            UdpSendAddress = other.UdpSendAddress;
            ReportTemplate = other.ReportTemplate;
            Servers = new List<string?>(other.Servers ?? new List<string?>());
            ScreenshotsPath = other.ScreenshotsPath;
            VideosPath = other.VideosPath;
            IconPlacement = other.IconPlacement;
            IconOffset = other.IconOffset;
        }

        public void CopyFrom(AppSettings other)
        {
            WindowWidth = other.WindowWidth;
            WindowHeight = other.WindowHeight;
            WindowX = other.WindowX;
            WindowY = other.WindowY;
            WindowMaximized = other.WindowMaximized;
            UdpListenPort = other.UdpListenPort;
            UdpSendPort = other.UdpSendPort;
            UdpSendAddress = other.UdpSendAddress;
            ReportTemplate = other.ReportTemplate;
            Servers = new List<string?>(other.Servers ?? new List<string?>());
            ScreenshotsPath = other.ScreenshotsPath;
            VideosPath = other.VideosPath;
            IconPlacement = other.IconPlacement;
            IconOffset = other.IconOffset;
        }

        // Window settings
        [JsonPropertyName("width")]
        public int WindowWidth { get; set; } = 1000;
        [JsonPropertyName("height")]
        public int WindowHeight { get; set; } = 800;
        public int WindowX { get; set; } = 100;
        public int WindowY { get; set; } = 100;
        public bool WindowMaximized { get; set; } = false;

        // Console settings
        public int UdpListenPort { get; set; } = 26000;
        public int UdpSendPort { get; set; } = 26001;
        public string? UdpSendAddress { get; set; } = "127.0.0.1";

        // User-specific settings
        [JsonPropertyName("report")]
        public string? ReportTemplate { get; set; } = "Server name: ${ServerName}\r\nWho are you reporting?: ${PlayerName}\r\nHis SteamId: ${SteamId}\r\nWhat happened?: ${Details}\r\nEvidence:\r\n";
        
        [JsonPropertyName("servers")]
        public List<string?> Servers { get; set; } = new List<string?> { "[EU] Die-Hard (+Anti-Rush)", "[EU] Hardcore Survival (+Anti-Rush)", "[US] Hardcore Survival (+Anti-Rush)", "[EU] Survival (+Anti-Rush)", "[US] Survival (+Anti-Rush)", "G-Man Invasion (+Anti-Rush)" };
        
        [JsonPropertyName("screenshots")]
        public string? ScreenshotsPath { get; set; } = "";
        
        [JsonPropertyName("videos")]
        public string? VideosPath { get; set; } = "";

        // API and automation settings
        [JsonPropertyName("api_key")]
        public string? ApiKey { get; set; }

        [JsonPropertyName("enable_periodic_vac_check")]
        public bool EnablePeriodicVacCheck { get; set; } = true;

        // Appearance settings
        [JsonPropertyName("icon_corner")]
        public IconCorner IconPlacement { get; set; } = IconCorner.BottomRight;
        public int IconOffset { get; set; } = 2;
    }
}
