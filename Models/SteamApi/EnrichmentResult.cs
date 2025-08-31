using System.Collections.Generic;

namespace GSRP.Models.SteamApi
{
    public class EnrichmentResult
    {
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public Dictionary<string, PlayerData> Data { get; set; } = new Dictionary<string, PlayerData>();
    }
}
