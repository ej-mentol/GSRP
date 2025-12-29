using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using GSRP.Backend.Models;

namespace GSRP.Daemon.Services
{
    public class PlayerParser
    {
        private const long STEAM_BASE = 76561197960265728L;
        
        // Robust regex for various 'status' formats: # 1 "name" 123 STEAM_0:0:123
        private readonly Regex PlayerLineRegex = new Regex(@"^\s*#\s*\d+\s+""(.*?)""\s+\d+\s+(STEAM_[0-9]:[0-9]:\d+)", RegexOptions.Multiline | RegexOptions.Compiled);

        public struct ParseResult { public string? Hostname; public List<Player> Players; }

        public ParseResult Parse(string text)
        {
            var res = new ParseResult { Players = new List<Player>() };
            if (string.IsNullOrEmpty(text)) return res;

            var matches = PlayerLineRegex.Matches(text);
            foreach (Match m in matches)
            {
                var name = m.Groups[1].Value.Trim();
                var sid2 = m.Groups[2].Value.Trim();
                var s64 = SteamId2To64(sid2);

                if (!string.IsNullOrEmpty(s64))
                {
                    res.Players.Add(new Player { 
                        SteamId64 = s64, 
                        SteamId2 = sid2, 
                        DisplayName = name, 
                        PersonaName = name 
                    });
                }
            }
            return res;
        }

        private string SteamId2To64(string s2) {
            var m = Regex.Match(s2, @"STEAM_[0-9]\:([0-9])\:(\d+)");
            if (!m.Success) return "";
            try {
                long authServer = long.Parse(m.Groups[1].Value);
                long accountId = long.Parse(m.Groups[2].Value);
                return (accountId * 2 + STEAM_BASE + authServer).ToString();
            } catch { return ""; }
        }
    }
}
