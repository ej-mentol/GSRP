namespace GSRP.Backend.Models
{
    public class Player
    {
        public string SteamId64 { get; set; } = string.Empty;
        public string SteamId2 { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string PersonaName { get; set; } = string.Empty;
        public string? Alias { get; set; }
        
        public string? PlayerColor { get; set; }
        public string? PersonaNameColor { get; set; }
        public string? AliasColor { get; set; }
        public string? IconName { get; set; }
        public string? AvatarHash { get; set; }

        public string ProfileStatus { get; set; } = "Unknown";
        public bool IsCommunityBanned { get; set; }
        public int NumberOfVacBans { get; set; }
        public int NumberOfGameBans { get; set; }
        public string? EconomyBan { get; set; }
        
        public long? LastVacCheck { get; set; } // null = never checked
        public long BanDate { get; set; }       // Unix Timestamp
        public uint TimeCreated { get; set; }   // Unix Timestamp
        public long LastUpdated { get; set; }   // Unix Timestamp
    }
}