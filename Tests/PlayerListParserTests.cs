using Xunit;
using GSRP.Services;
using GSRP.Models;
using System.Linq;

namespace GSRP.Tests
{
    public class PlayerListParserTests
    {
        private readonly PlayerListParser _parser;

        public PlayerListParserTests()
        {
            _parser = new PlayerListParser();
        }

        [Fact]
        public void ParsePlayers_ValidStatusOutput_ReturnsPlayers()
        {
            var input = @"
hostname: My Server
version : 1.0.0.0/12345 1234 secure
udp/ip  : 127.0.0.1:27015
players : 2 humans, 0 bots (32 max)
# userid name uniqueid connected ping loss state rate
#  2 ""PlayerOne"" 1337 STEAM_0:0:123456 00:10 50 0 active 30000
#  3 ""PlayerTwo"" 1337 STEAM_0:1:654321 00:20 60 0 active 30000
";
            var players = _parser.ParsePlayers(input);

            Assert.Equal(2, players.Count);
            Assert.Contains(players, p => p.Name == "PlayerOne" && p.SteamId2 == "STEAM_0:0:123456");
            Assert.Contains(players, p => p.Name == "PlayerTwo" && p.SteamId2 == "STEAM_0:1:654321");
        }

        [Fact]
        public void ParsePlayers_InvalidInput_ReturnsEmptyList()
        {
            var input = "Just some random text";
            var players = _parser.ParsePlayers(input);
            Assert.Empty(players);
        }

        [Fact]
        public void SteamId2To64_ValidSteamId_ReturnsCorrectSteamId64()
        {
            var steamId2 = "STEAM_0:0:123456";
            var steamId64 = _parser.SteamId2To64(steamId2);
            // 76561197960265728 + 123456 * 2 + 0 = 76561197960512640
            Assert.Equal("76561197960512640", steamId64);
        }
    }
}
