using GSRP.Models;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace GSRP.Services
{
    public class PlayerListParser : IPlayerListParser
    {
        private const long STEAM_BASE = 76561197960265728L;
                
        private readonly Regex PlayerLineRegex = new Regex(
            @"^#\s*\d+\s+""([^""]+)""\s+\d+\s+(STEAM_[0-9]:[0-9]:\d+)\s",
            RegexOptions.Multiline | RegexOptions.Compiled
        );
                
        private readonly Regex PlayerListIndicatorRegex = new Regex(
            @"(?:players\s*:\s*\d+|#\s*\d+\s*""[^""]*""|\bSTEAM_[0-9]:[0-9]:\d+)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled
        );

        public string SteamId2To64(string steamId2)
        {
            if (string.IsNullOrEmpty(steamId2))
                return string.Empty;

            var match = new Regex(@"STEAM_[0-9]\:([0-9])\:(\d+)").Match(steamId2);
            if (!match.Success)
                return string.Empty;

            try
            {
                int authServer = int.Parse(match.Groups[1].Value);
                long accountId = long.Parse(match.Groups[2].Value);
                return (accountId * 2 + STEAM_BASE + authServer).ToString();
            }
            catch
            {
                return string.Empty;
            }
        }

        public string SteamId64To2(string steamId64)
        {
            if (string.IsNullOrEmpty(steamId64) || !long.TryParse(steamId64, out long id64))
                return string.Empty;

            try
            {
                long accountId = (id64 - STEAM_BASE) / 2;
                int authServer = (int)((id64 - STEAM_BASE) % 2);
                return $"STEAM_0:{authServer}:{accountId}";
            }
            catch
            {
                return string.Empty;
            }
        }

        public List<Player> ParsePlayers(string text)
        {
            var players = new List<Player>();

            if (string.IsNullOrEmpty(text))
                return players;

            var matches = PlayerLineRegex.Matches(text);

            foreach (Match match in matches)
            {
                if (match.Groups.Count < 3)
                    continue;

                try
                {
                    var steamId2 = match.Groups[2].Value.Trim();
                    var steamId64 = SteamId2To64(steamId2);

                    if (!string.IsNullOrEmpty(steamId64))
                    {
                        var player = new Player
                        {
                            Name = match.Groups[1].Value.Trim(),
                            SteamId2 = steamId2,
                            SteamId64 = steamId64
                        };
                        players.Add(player);
                    }
                }
                catch
                {
                    continue;
                }
            }

            return players;
        }

        public bool IsValidPlayerListFormat(string text)
        {
            if (string.IsNullOrEmpty(text))
                return false;
                        
            if (!PlayerListIndicatorRegex.IsMatch(text))
                return false;
                        
            return PlayerLineRegex.IsMatch(text);
        }

        public List<string> ExtractSteamIds64(string text)
        {
            return ParsePlayers(text).Select(p => p.SteamId64).Where(id => !string.IsNullOrEmpty(id)).ToList();
        }

        public Dictionary<string, string> GetNameToSteamIdMapping(string text)
        {
            var mapping = new Dictionary<string, string>();
            var players = ParsePlayers(text);

            foreach (var player in players)
            {
                if (!string.IsNullOrEmpty(player.Name) && !string.IsNullOrEmpty(player.SteamId64))
                {
                    mapping[player.Name] = player.SteamId64;
                }
            }

            return mapping;
        }
    }
}