using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace GSRP.Models.SteamApi
{
    public record PlayerBanData
    {
        [JsonPropertyName("SteamId")]
        public string SteamId { get; init; } = string.Empty;

        [JsonPropertyName("CommunityBanned")]
        public bool CommunityBanned { get; init; }

        [JsonPropertyName("VACBanned")]
        public bool VACBanned { get; init; }

        [JsonPropertyName("NumberOfVACBans")]
        public int NumberOfVACBans { get; init; }

        [JsonPropertyName("NumberOfGameBans")]
        public int NumberOfGameBans { get; init; }

        [JsonPropertyName("DaysSinceLastBan")]
        public int DaysSinceLastBan { get; init; }

        [JsonPropertyName("EconomyBan")]
        public string EconomyBan { get; init; } = string.Empty;
    }

    public record PlayerBansRoot
    {
        [JsonPropertyName("players")]
        public List<PlayerBanData> Players { get; init; } = new();
    }
}