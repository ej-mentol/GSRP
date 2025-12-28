using System.Text.Json.Serialization;
using System.Collections.Generic;
using GSRP.Backend.Models;
using GSRP.Daemon.Models;

namespace GSRP.Daemon.Core
{
    public record MigrationData(int Count);
    public record DetectedData(string? Hostname, List<Player> Players);
    public record SearchResultsData(List<Player> Players);
    public record StatusData(string Status);
    public record ProgressData(int Percent, string Status);
    public record LogData(string Tag, string Text);
    public record IpcMessage(string Type, object? Data);

    // Steam API Records
    public record SteamPlayer(string Steamid, string Personaname, string Avatarhash, uint Timecreated, int Communityvisibilitystate);
    public record SteamBan(string SteamId, bool CommunityBanned, bool VACBanned, int NumberOfVACBans, int DaysSinceLastBan, int NumberOfGameBans, string EconomyBan);
    
    public record SteamSummaryResponse(List<SteamPlayer> Players);
    public record SteamBansResponse(List<SteamBan> Players);

    [JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
    [JsonSerializable(typeof(IpcMessage))]
    [JsonSerializable(typeof(Player))]
    [JsonSerializable(typeof(List<Player>))]
    [JsonSerializable(typeof(MigrationData))]
    [JsonSerializable(typeof(DetectedData))]
    [JsonSerializable(typeof(SearchResultsData))]
    [JsonSerializable(typeof(StatusData))]
    [JsonSerializable(typeof(ProgressData))]
    [JsonSerializable(typeof(LogData))]
    [JsonSerializable(typeof(SteamSummaryResponse))]
    [JsonSerializable(typeof(SteamBansResponse))]
    [JsonSerializable(typeof(AppSettings))]
    internal partial class JsonContext : JsonSerializerContext
    {
    }
}