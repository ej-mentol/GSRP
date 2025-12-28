using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using GSRP.Backend.Models;

namespace GSRP.Daemon.Services
{
    public class StorageService
    {
        private readonly string _dbPath;
        private readonly string _connectionString;
        private const long STEAM_BASE = 76561197960265728;

        public StorageService()
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            _dbPath = Path.Combine(appData, "GSRP", "gsrp.db");
            _connectionString = $"Data Source={_dbPath}";
            
            var dir = Path.GetDirectoryName(_dbPath);
            if (dir != null && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
        }

        public async Task InitializeAsync()
        {
            int retries = 0;
            while (retries < 5) {
                try {
                    using var conn = new SqliteConnection(_connectionString);
                    conn.Open();
                    
                    var walCmd = conn.CreateCommand();
                    walCmd.CommandText = "PRAGMA journal_mode=WAL; PRAGMA synchronous=NORMAL;";
                    walCmd.ExecuteNonQuery();

                    var cmd = conn.CreateCommand();
                    // ... (schema creation)
                    cmd.CommandText = @"
                        CREATE TABLE IF NOT EXISTS players (
                            steam_id64 TEXT PRIMARY KEY,
                            alias TEXT,
                            txt_color TEXT,
                            avatarhash TEXT,
                            timecreated INTEGER DEFAULT 0,
                            personaname TEXT,
                            last_updated INTEGER DEFAULT 0,
                            iconname TEXT,
                            stm_color TEXT,
                            profile_status INTEGER,
                            is_community_banned INTEGER,
                            number_of_vac_bans INTEGER DEFAULT 0,
                            number_of_game_bans INTEGER DEFAULT 0,
                            last_vac_check INTEGER,
                            economy_ban TEXT,
                            ban_date INTEGER DEFAULT 0,
                            alias_color TEXT,
                            card_color TEXT
                        );
                        CREATE INDEX IF NOT EXISTS idx_steam_id64 ON players(steam_id64);
                        CREATE INDEX IF NOT EXISTS idx_personaname ON players(personaname);
                        CREATE INDEX IF NOT EXISTS idx_alias ON players(alias);
                    ";
                    cmd.ExecuteNonQuery();

                    // Schema Migration: Check for 'card_color' (added in v2.1)
                    try {
                        var checkCmd = conn.CreateCommand();
                        checkCmd.CommandText = "SELECT card_color FROM players LIMIT 1";
                        checkCmd.ExecuteNonQuery();
                    } catch {
                        var alterCmd = conn.CreateCommand();
                        alterCmd.CommandText = "ALTER TABLE players ADD COLUMN card_color TEXT";
                        alterCmd.ExecuteNonQuery();
                    }
                    
                    return; // Success!
                } catch (SqliteException ex) when (ex.SqliteErrorCode == 5) { // 5 = SQLITE_BUSY
                    retries++;
                    await Task.Delay(200);
                } catch { 
                    break; 
                }
            }
        }

        public async Task<List<Player>> SearchPlayersAsync(string term, bool caseSensitive = false, string? colorFilter = null, bool vacBanned = false, bool gameBanned = false, bool communityBanned = false, bool economyBanned = false)
        {
            return await Task.Run(() => {
                var results = new List<Player>();
                try {
                    using var conn = new SqliteConnection(_connectionString);
                    conn.Open();
                    var cmd = conn.CreateCommand();
                    
                    string searchId64 = "";
                    if (!string.IsNullOrEmpty(term) && term.StartsWith("STEAM_", StringComparison.OrdinalIgnoreCase)) {
                        try {
                            var parts = term.Split(':');
                            if (parts.Length == 3) {
                                long authServer = long.Parse(parts[1]);
                                long accountId = long.Parse(parts[2]);
                                searchId64 = (accountId * 2 + STEAM_BASE + authServer).ToString();
                            }
                        } catch { }
                    }

                    List<string> conditions = new List<string>();
                    
                    // 1. Text Search
                    if (!string.IsNullOrEmpty(term)) {
                        if (caseSensitive) conditions.Add("(steam_id64 = @id64 OR steam_id64 LIKE @t OR alias LIKE @t OR personaname LIKE @t)");
                        else conditions.Add("(steam_id64 = @id64 OR LOWER(steam_id64) LIKE LOWER(@t) OR LOWER(alias) LIKE LOWER(@t) OR LOWER(personaname) LIKE LOWER(@t))");
                        cmd.Parameters.AddWithValue("@t", $"%{term}%");
                        cmd.Parameters.AddWithValue("@id64", searchId64);
                    }

                    // 2. Color Filter (Case Insensitive)
                    if (!string.IsNullOrEmpty(colorFilter)) {
                        conditions.Add("(txt_color = @c COLLATE NOCASE OR stm_color = @c COLLATE NOCASE OR alias_color = @c COLLATE NOCASE)");
                        cmd.Parameters.AddWithValue("@c", colorFilter);
                    }

                    // 3. Ban Filters
                    if (vacBanned) conditions.Add("number_of_vac_bans > 0");
                    if (gameBanned) conditions.Add("number_of_game_bans > 0");
                    if (communityBanned) conditions.Add("is_community_banned = 1");
                    if (economyBanned) conditions.Add("(economy_ban IS NOT NULL AND economy_ban != 'none' AND economy_ban != '0')");

                    if (conditions.Count == 0) return results;

                    // Limit logic: If specialized filters are active, allow more results to support client-side browsing
                    bool hasFilters = !string.IsNullOrEmpty(colorFilter) || vacBanned || gameBanned || communityBanned || economyBanned;
                    int limit = hasFilters ? 5000 : 2000;
                    
                    cmd.CommandText = "SELECT * FROM players WHERE " + string.Join(" AND ", conditions) + $" LIMIT {limit}";
                    
                    using var reader = cmd.ExecuteReader();
                    while (reader.Read()) results.Add(MapPlayerFromReader(reader));
                } catch { }
                return results;
            });
        }

        public async Task<Player?> GetPlayerAsync(string steamId64)
        {
            return await Task.Run(() => {
                try {
                    using var conn = new SqliteConnection(_connectionString);
                    conn.Open();
                    var cmd = conn.CreateCommand();
                    cmd.CommandText = "SELECT * FROM players WHERE steam_id64 = @id";
                    cmd.Parameters.AddWithValue("@id", steamId64);
                    using var reader = cmd.ExecuteReader();
                    if (reader.Read()) return MapPlayerFromReader(reader);
                } catch { }
                return null;
            });
        }

        private Player MapPlayerFromReader(SqliteDataReader reader) {
            int Get(string name) => reader.GetOrdinal(name);
            string? GetStr(string name) => reader.IsDBNull(Get(name)) ? null : reader.GetString(Get(name));

            var p = new Player {
                SteamId64 = reader.GetString(Get("steam_id64")),
                Alias = GetStr("alias"),
                PlayerColor = GetStr("txt_color"),
                AvatarHash = GetStr("avatarhash"),
                TimeCreated = (uint)reader.GetInt64(Get("timecreated")),
                PersonaName = GetStr("personaname") ?? "", 
                LastUpdated = reader.GetInt64(Get("last_updated")),
                IconName = GetStr("iconname"),
                PersonaNameColor = GetStr("stm_color"),
                ProfileStatus = reader.IsDBNull(Get("profile_status")) ? "Unknown" : (reader.GetInt32(Get("profile_status")) == 1 ? "Public" : "Private"),
                IsCommunityBanned = !reader.IsDBNull(Get("is_community_banned")) && reader.GetInt32(Get("is_community_banned")) == 1,
                NumberOfVacBans = (int)reader.GetInt64(Get("number_of_vac_bans")),
                NumberOfGameBans = (int)reader.GetInt64(Get("number_of_game_bans")),
                LastVacCheck = reader.IsDBNull(Get("last_vac_check")) ? 0 : reader.GetInt64(Get("last_vac_check")),
                EconomyBan = GetStr("economy_ban") ?? "none",
                BanDate = reader.GetInt64(Get("ban_date")),
                AliasColor = GetStr("alias_color"),
                CardColor = GetStr("card_color")
            };
            p.SteamId2 = SteamId64To2(p.SteamId64);
            p.DisplayName = !string.IsNullOrEmpty(p.Alias) ? p.Alias : p.PersonaName;
            return p;
        }

        public async Task SavePlayerAsync(Player p)
        {
            await Task.Run(() => {
                try {
                    using var conn = new SqliteConnection(_connectionString);
                    conn.Open();
                    var cmd = conn.CreateCommand();
                    
                    object? ColorToDb(string? val) => (string.IsNullOrWhiteSpace(val) || val == "0") ? DBNull.Value : val;
                    object? AliasToDb(string? val) => string.IsNullOrWhiteSpace(val) ? DBNull.Value : val;

                    cmd.CommandText = @"
                        INSERT INTO players (
                            steam_id64, alias, txt_color, avatarhash, timecreated, personaname, 
                            last_updated, iconname, stm_color, profile_status, is_community_banned,
                            number_of_vac_bans, number_of_game_bans, last_vac_check, economy_ban, ban_date, alias_color, card_color
                        ) VALUES (
                            @id, @alias, @txt_c, @hash, @tc, @pname, @lu, @icon, @stm_c, @ps, @icb, @nvb, @ngb, @lvc, @eb, @bd, @ac, @cc
                        ) ON CONFLICT(steam_id64) DO UPDATE SET
                            alias=excluded.alias, txt_color=excluded.txt_color, avatarhash=excluded.avatarhash,
                            timecreated=excluded.timecreated, personaname=excluded.personaname, last_updated=excluded.last_updated,
                            iconname=excluded.iconname, stm_color=excluded.stm_color, profile_status=excluded.profile_status,
                            is_community_banned=excluded.is_community_banned, number_of_vac_bans=excluded.number_of_vac_bans,
                            number_of_game_bans=excluded.number_of_game_bans, last_vac_check=excluded.last_vac_check,
                            economy_ban=excluded.economy_ban, ban_date=excluded.ban_date, alias_color=excluded.alias_color, card_color=excluded.card_color
                    ";
                    
                    cmd.Parameters.AddWithValue("@id", p.SteamId64);
                    cmd.Parameters.AddWithValue("@alias", AliasToDb(p.Alias));
                    cmd.Parameters.AddWithValue("@txt_c", ColorToDb(p.PlayerColor));
                    cmd.Parameters.AddWithValue("@hash", (object?)p.AvatarHash ?? ""); 
                    cmd.Parameters.AddWithValue("@tc", p.TimeCreated);
                    cmd.Parameters.AddWithValue("@pname", p.PersonaName ?? "");
                    cmd.Parameters.AddWithValue("@lu", p.LastUpdated);
                    cmd.Parameters.AddWithValue("@icon", (object?)p.IconName ?? "");
                    cmd.Parameters.AddWithValue("@stm_c", ColorToDb(p.PersonaNameColor));
                    cmd.Parameters.AddWithValue("@ps", p.ProfileStatus == "Public" ? 1 : 0);
                    cmd.Parameters.AddWithValue("@icb", p.IsCommunityBanned ? 1 : 0);
                    cmd.Parameters.AddWithValue("@nvb", p.NumberOfVacBans);
                    cmd.Parameters.AddWithValue("@ngb", p.NumberOfGameBans);
                    cmd.Parameters.AddWithValue("@lvc", (object?)p.LastVacCheck ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@eb", (object?)p.EconomyBan ?? DBNull.Value); 
                    cmd.Parameters.AddWithValue("@bd", p.BanDate);
                    cmd.Parameters.AddWithValue("@ac", ColorToDb(p.AliasColor));
                    cmd.Parameters.AddWithValue("@cc", ColorToDb(p.CardColor));
                    
                    cmd.ExecuteNonQuery();
                } catch { }
            });
        }

        private string SteamId64To2(string sid64) {
            if (string.IsNullOrEmpty(sid64) || !long.TryParse(sid64, out long id64)) return "";
            if (id64 < STEAM_BASE) return "";
            
            long diff = id64 - STEAM_BASE;
            long authServer = diff % 2;
            long accountId = (diff - authServer) / 2;
            return $"STEAM_0:{authServer}:{accountId}";
        }
    }
}
