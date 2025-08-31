using GSRP.Models;
using System.Collections.Generic;

namespace GSRP.Services
{
    public interface IPlayerListParser
    {
        string SteamId2To64(string steamId2);
        string SteamId64To2(string steamId64);
        List<Player> ParsePlayers(string text);
        bool IsValidPlayerListFormat(string text);
        List<string> ExtractSteamIds64(string text);
        Dictionary<string, string> GetNameToSteamIdMapping(string text);
    }
}
