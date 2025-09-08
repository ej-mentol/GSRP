using GSRP.Models;
using Microsoft.Data.Sqlite;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;

namespace GSRP.Services
{
    public class DatabaseService : IDatabaseService, IDisposable
    {
        private readonly string _connectionString;
        private readonly IDatabaseMigrationService _migrationService;
        private readonly ConcurrentQueue<Func<Task>> _operationQueue;
        private readonly SemaphoreSlim _queueSemaphore;
        private readonly CancellationTokenSource _cts;
        private readonly Task _processingTask;

        public DatabaseService(IPathProvider pathProvider, IDatabaseMigrationService migrationService)
        {
            _migrationService = migrationService;
            var dbPath = Path.Combine(pathProvider.GetAppDataPath(), "players.db");
            _connectionString = $"Data Source={dbPath};";
            
            _operationQueue = new ConcurrentQueue<Func<Task>>();
            _queueSemaphore = new SemaphoreSlim(0);
            _cts = new CancellationTokenSource();
            
            InitializeDatabase();
            
            _processingTask = Task.Run(ProcessQueueAsync);
        }

        private async Task ProcessQueueAsync()
        {
            while (!_cts.Token.IsCancellationRequested)
            {
                try
                {
                    await _queueSemaphore.WaitAsync(_cts.Token);
                    
                    if (_operationQueue.TryDequeue(out var operation))
                    {
                        await operation();
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[DatabaseService] Queue processing error: {ex.Message}");
                }
            }
        }

        private Task EnqueueOperationAsync(Func<Task> operation)
        {
            var tcs = new TaskCompletionSource<bool>();
            
            _operationQueue.Enqueue(async () =>
            {
                try
                {
                    await operation();
                    tcs.SetResult(true);
                }
                catch (Exception ex)
                {
                    tcs.SetException(ex);
                }
            });
            
            _queueSemaphore.Release();
            return tcs.Task;
        }

        private void InitializeDatabase()
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            _migrationService.ApplyMigrations(connection);

            var createTableSql = @"
                CREATE TABLE IF NOT EXISTS players (
                    steam_id64 TEXT PRIMARY KEY,
                    alias TEXT DEFAULT '',
                    txt_color INTEGER DEFAULT 0,
                    avatarhash TEXT DEFAULT '',
                    timecreated INTEGER DEFAULT 0,
                    personaname TEXT DEFAULT '',
                    last_updated INTEGER DEFAULT 0,
                    iconname TEXT DEFAULT '',
                    stm_color INTEGER DEFAULT 0,
                    profile_status INTEGER DEFAULT 0,
                    is_community_banned INTEGER NOT NULL DEFAULT 0,
                    number_of_vac_bans INTEGER NOT NULL DEFAULT 0,
                    last_vac_check INTEGER NOT NULL DEFAULT 0,
                    economy_ban TEXT NOT NULL DEFAULT 'none',
                    ban_date INTEGER NOT NULL DEFAULT 0,
                    alias_color INTEGER DEFAULT 0
                );";
            using (var command = new SqliteCommand(createTableSql, connection))
            {
                command.ExecuteNonQuery();
            }

            using (var indexCommand = new SqliteCommand("CREATE INDEX IF NOT EXISTS idx_steam_id64 ON players(steam_id64);", connection))
            {
                indexCommand.ExecuteNonQuery();
            }
        }

        public async Task<Dictionary<string, PlayerDbData>> GetPlayersDataAsync(IEnumerable<string> steamIds)
        {
            var result = new Dictionary<string, PlayerDbData>();
            var idList = steamIds.ToList();
            if (!idList.Any())
            {
                return result;
            }

            var sql = new StringBuilder("SELECT steam_id64, alias, txt_color, stm_color, avatarhash, timecreated, personaname, last_updated, iconname, profile_status, is_community_banned, number_of_vac_bans, last_vac_check, economy_ban, ban_date, alias_color FROM players WHERE steam_id64 IN (");
            var parameters = new List<SqliteParameter>();
            for (int i = 0; i < idList.Count; i++)
            {
                var paramName = $"@id{i}";
                sql.Append(paramName);
                if (i < idList.Count - 1) sql.Append(", ");
                parameters.Add(new SqliteParameter(paramName, idList[i]));
            }
            sql.Append(");");

            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();
            using var command = new SqliteCommand(sql.ToString(), connection);
            command.Parameters.AddRange(parameters);

            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var steamId = reader.GetString(0);
                result[steamId] = new PlayerDbData(
                    Alias: reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                    PlayerColor: ConvertToColor(reader.IsDBNull(2) ? 0 : reader.GetInt64(2)),
                    PersonaNameColor: ConvertToColor(reader.IsDBNull(3) ? 0 : reader.GetInt64(3)),
                    AvatarHash: reader.IsDBNull(4) ? string.Empty : reader.GetString(4),
                    TimeCreated: reader.IsDBNull(5) ? 0 : (uint)reader.GetInt64(5),
                    PersonaName: reader.IsDBNull(6) ? string.Empty : reader.GetString(6),
                    LastUpdated: reader.IsDBNull(7) ? 0 : reader.GetInt64(7),
                    IconName: reader.IsDBNull(8) ? string.Empty : reader.GetString(8),
                    ProfileStatus: reader.IsDBNull(9) ? ProfileStatus.Unknown : (ProfileStatus)reader.GetInt32(9),
                    IsCommunityBanned: reader.IsDBNull(10) ? false : reader.GetBoolean(10),
                    NumberOfVacBans: reader.IsDBNull(11) ? 0 : reader.GetInt32(11),
                    LastVacCheck: reader.IsDBNull(12) ? 0 : reader.GetInt64(12),
                    EconomyBan: reader.IsDBNull(13) ? string.Empty : reader.GetString(13),
                    BanDate: reader.IsDBNull(14) ? 0 : reader.GetInt64(14),
                    AliasColor: ConvertToColor(reader.IsDBNull(15) ? 0 : reader.GetInt64(15))
                );
            }

            return result;
        }

        public async Task<List<PlayerSearchResult>> SearchPlayersAsync(string searchTerm, string? steamId64Term, bool exactMatch)
        {
            var results = new List<PlayerSearchResult>();
            if (string.IsNullOrWhiteSpace(searchTerm))
            {
                return results;
            }

            var conditions = new List<string>();
            var parameters = new List<SqliteParameter>();

            if (exactMatch)
            {
                conditions.Add("alias = @term");
                conditions.Add("personaname = @term");
                parameters.Add(new SqliteParameter("@term", searchTerm));
            }
            else
            {
                conditions.Add("alias LIKE @term");
                conditions.Add("personaname LIKE @term");
                parameters.Add(new SqliteParameter("@term", $"%{searchTerm}%"));
            }

            if (!string.IsNullOrEmpty(steamId64Term))
            {
                conditions.Add("steam_id64 = @steamId64");
                parameters.Add(new SqliteParameter("@steamId64", steamId64Term));
            }
            else
            {
                conditions.Add("steam_id64 LIKE @term");
            }

            var whereClause = string.Join(" OR ", conditions);
            var sql = @$"
                SELECT steam_id64, alias, txt_color, stm_color, avatarhash, timecreated, personaname, last_updated, iconname, profile_status, is_community_banned, number_of_vac_bans, last_vac_check, economy_ban, ban_date, alias_color
                FROM players
                WHERE {whereClause};";

            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();
            using var command = new SqliteCommand(sql, connection);
            command.Parameters.AddRange(parameters);

            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var steamId = reader.GetString(0);
                var dbData = new PlayerDbData(
                    Alias: reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                    PlayerColor: ConvertToColor(reader.IsDBNull(2) ? 0 : reader.GetInt64(2)),
                    PersonaNameColor: ConvertToColor(reader.IsDBNull(3) ? 0 : reader.GetInt64(3)),
                    AvatarHash: reader.IsDBNull(4) ? string.Empty : reader.GetString(4),
                    TimeCreated: reader.IsDBNull(5) ? 0 : (uint)reader.GetInt64(5),
                    PersonaName: reader.IsDBNull(6) ? string.Empty : reader.GetString(6),
                    LastUpdated: reader.IsDBNull(7) ? 0 : reader.GetInt64(7),
                    IconName: reader.IsDBNull(8) ? string.Empty : reader.GetString(8),
                    ProfileStatus: reader.IsDBNull(9) ? ProfileStatus.Unknown : (ProfileStatus)reader.GetInt32(9),
                    IsCommunityBanned: reader.IsDBNull(10) ? false : reader.GetBoolean(10),
                    NumberOfVacBans: reader.IsDBNull(11) ? 0 : reader.GetInt32(11),
                    LastVacCheck: reader.IsDBNull(12) ? 0 : reader.GetInt64(12),
                    EconomyBan: reader.IsDBNull(13) ? string.Empty : reader.GetString(13),
                    BanDate: reader.IsDBNull(14) ? 0 : reader.GetInt64(14),
                    AliasColor: ConvertToColor(reader.IsDBNull(15) ? 0 : reader.GetInt64(15))
                );
                results.Add(new PlayerSearchResult(steamId, dbData));
            }

            return results;
        }

        private Color? ConvertToColor(long colorValue)
        {
            if (colorValue == 0) return null;
            return Color.FromArgb((byte)((colorValue >> 24) & 0xFF), (byte)((colorValue >> 16) & 0xFF), (byte)((colorValue >> 8) & 0xFF), (byte)(colorValue & 0xFF));
        }

        public async Task<long> GetLastUpdatedAsync(long steamId64)
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();
            using var command = new SqliteCommand("SELECT last_updated FROM players WHERE steam_id64 = @steamId", connection);
            command.Parameters.AddWithValue("@steamId", steamId64.ToString());
            var result = await command.ExecuteScalarAsync();
            if (result == null || result == DBNull.Value) return 0;
            return Convert.ToInt64(result);
        }

        private async Task ExecuteNonQueryAsync(string sql, params SqliteParameter[] parameters)
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();
            using var command = new SqliteCommand(sql, connection);
            command.Parameters.AddRange(parameters);
            await command.ExecuteNonQueryAsync();
        }

        private Task UpsertPlayerFieldAsync(long steamId64, string fieldName, object value)
        {
            var sql = $@"
                INSERT INTO players (steam_id64, {fieldName}) 
                VALUES (@steamId, @value)
                ON CONFLICT(steam_id64) DO UPDATE SET 
                    {fieldName} = excluded.{fieldName},
                    last_updated = @lastUpdated;";
            
            return EnqueueOperationAsync(() => ExecuteNonQueryAsync(sql, 
                new SqliteParameter("@steamId", steamId64.ToString()),
                new SqliteParameter("@value", value),
                new SqliteParameter("@lastUpdated", DateTimeOffset.Now.ToUnixTimeSeconds())));
        }

        public Task SetIconNameAsync(long steamId64, string iconName) => UpsertPlayerFieldAsync(steamId64, "iconname", iconName);
        public Task SetAliasAsync(long steamId64, string alias) => UpsertPlayerFieldAsync(steamId64, "alias", alias);
        
        public Task SetTextColorAsync(long steamId64, Color color)
        {
            long colorValue = (uint)(color.A << 24) | (uint)(color.R << 16) | (uint)(color.G << 8) | color.B;
            return UpsertPlayerFieldAsync(steamId64, "txt_color", colorValue);
        }

        public Task SetPersonaNameColorAsync(long steamId64, Color color)
        {
            long colorValue = (uint)(color.A << 24) | (uint)(color.R << 16) | (uint)(color.G << 8) | color.B;
            return UpsertPlayerFieldAsync(steamId64, "stm_color", colorValue);
        }

        public Task SetAliasColorAsync(long steamId64, Color color)
        {
            long colorValue = (uint)(color.A << 24) | (uint)(color.R << 16) | (uint)(color.G << 8) | color.B;
            return UpsertPlayerFieldAsync(steamId64, "alias_color", colorValue);
        }

        public Task RemoveTextColorAsync(long steamId64) => UpsertPlayerFieldAsync(steamId64, "txt_color", 0);
        public Task RemovePersonaNameColorAsync(long steamId64) => UpsertPlayerFieldAsync(steamId64, "stm_color", 0);
        public Task RemoveAliasColorAsync(long steamId64) => UpsertPlayerFieldAsync(steamId64, "alias_color", 0);

        public Task SetAvatarHashAsync(long steamId64, string avatarHash) => UpsertPlayerFieldAsync(steamId64, "avatarhash", avatarHash);
        public Task SetTimeCreatedAsync(long steamId64, uint timeCreated) => UpsertPlayerFieldAsync(steamId64, "timecreated", timeCreated);
        public Task SetPersonaNameAsync(long steamId64, string personaName) => UpsertPlayerFieldAsync(steamId64, "personaname", personaName);
        public Task SetProfileStatusAsync(long steamId64, ProfileStatus profileStatus) => UpsertPlayerFieldAsync(steamId64, "profile_status", (int)profileStatus);
        public Task SetLastUpdatedAsync(long steamId64, long timestamp) => UpsertPlayerFieldAsync(steamId64, "last_updated", timestamp);

        public Task UpdatePlayerBanStatusAsync(long steamId64, bool isCommunityBanned, string economyBan, int numberOfVacBans, long banDate, long lastVacCheck)
        {
            var sql = $@"
                INSERT INTO players (steam_id64, is_community_banned, economy_ban, number_of_vac_bans, ban_date, last_vac_check) 
                VALUES (@steamId, @isCommunityBanned, @economyBan, @numberOfVacBans, @banDate, @lastVacCheck)
                ON CONFLICT(steam_id64) DO UPDATE SET 
                    is_community_banned = excluded.is_community_banned,
                    economy_ban = excluded.economy_ban,
                    number_of_vac_bans = excluded.number_of_vac_bans,
                    ban_date = excluded.ban_date,
                    last_vac_check = excluded.last_vac_check;";

            return EnqueueOperationAsync(() => ExecuteNonQueryAsync(sql,
                new SqliteParameter("@steamId", steamId64.ToString()),
                new SqliteParameter("@isCommunityBanned", isCommunityBanned ? 1 : 0),
                new SqliteParameter("@economyBan", economyBan),
                new SqliteParameter("@numberOfVacBans", numberOfVacBans),
                new SqliteParameter("@banDate", banDate),
                new SqliteParameter("@lastVacCheck", lastVacCheck)));
        }

        public void Dispose()
        {
            _cts.Cancel();
            try
            {
                _processingTask.Wait(1000);
            }
            catch { }
            
            _cts.Dispose();
            _queueSemaphore.Dispose();
        }
    }
}
