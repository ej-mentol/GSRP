using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Media;
using GSRP.Models;

namespace GSRP.Services
{
    public interface IPlayerRepository : IDisposable
    {
        event EventHandler<List<Player>>? PlayersUpdated;
        Task ProcessClipboardDataAsync(string clipboardText, IProgress<string> progress);
        List<Player> GetCurrentPlayers();
        Task SetPlayerAliasAsync(Player player, string alias);
        Task SetPlayerColorAsync(Player player, Color color);
        Task SetPlayerPersonaNameColorAsync(Player player, Color color);
        Task SetPlayerIconAsync(Player player, string iconName);
        Task RemovePlayerColorAsync(Player player);
        Task RemovePlayerPersonaNameColorAsync(Player player);
        
        string GetAvatarPath(string avatarHash);
        Task ForceEnrichCurrentPlayersAsync(CancellationToken token);
        Task<List<Player>> SearchPlayersAsync(string searchTerm, string? steamId64Term, bool exactMatch);
    }
}