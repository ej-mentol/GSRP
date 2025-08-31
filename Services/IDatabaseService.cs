using GSRP.Models;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Media;

namespace GSRP.Services
{
    public record PlayerDbData(
        string Alias,
        Color? PlayerColor,
        Color? PersonaNameColor,
        string AvatarHash,
        uint TimeCreated,
        string PersonaName,
        long LastUpdated,
        string IconName,
        ProfileStatus ProfileStatus
    );

    public record PlayerSearchResult(string SteamId64, PlayerDbData Data);

    public interface IDatabaseService
    {
        Task<List<PlayerSearchResult>> SearchPlayersAsync(string searchTerm, string? steamId64Term, bool exactMatch);
        Task<Dictionary<string, PlayerDbData>> GetPlayersDataAsync(IEnumerable<string> steamIds);
        Task<long> GetLastUpdatedAsync(long steamId64); // Keep this for the enrichment check

        Task SetIconNameAsync(long steamId64, string iconName);
        Task SetAliasAsync(long steamId64, string alias);
        Task SetTextColorAsync(long steamId64, Color color);
        Task SetPersonaNameColorAsync(long steamId64, Color color);
        Task RemoveTextColorAsync(long steamId64);
        Task RemovePersonaNameColorAsync(long steamId64);
        Task SetAvatarHashAsync(long steamId64, string avatarHash);
        Task SetTimeCreatedAsync(long steamId64, uint timeCreated);
        Task SetPersonaNameAsync(long steamId64, string personaName);
        Task SetProfileStatusAsync(long steamId64, ProfileStatus profileStatus);
        Task SetLastUpdatedAsync(long steamId64, long timestamp);
    }
}
