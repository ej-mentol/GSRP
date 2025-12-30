using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;

namespace GSRP.Daemon.Services
{
    public class MigrationRule {
        public string Name { get; set; }
        public string CheckSql { get; set; }
        public string FixSql { get; set; }
        public bool Enabled { get; set; }
        public bool IsSchemaChange { get; set; }

        public MigrationRule(string name, string checkSql, string fixSql, bool enabled = true, bool isSchemaChange = false) {
            Name = name;
            CheckSql = checkSql;
            FixSql = fixSql;
            Enabled = enabled;
            IsSchemaChange = isSchemaChange;
        }
    }

    public class DatabaseMigrationService
    {
        private readonly string _connectionString;
        private readonly string _dbPath;

        private readonly List<MigrationRule> _rules = new()
        {
            new MigrationRule(
                "Schema: Add card_color column",
                "SELECT COUNT(*) FROM pragma_table_info('players') WHERE name='card_color'",
                "ALTER TABLE players ADD COLUMN card_color TEXT;",
                true, true
            ),
            new MigrationRule(
                "Schema: Add alias_color column",
                "SELECT COUNT(*) FROM pragma_table_info('players') WHERE name='alias_color'",
                "ALTER TABLE players ADD COLUMN alias_color TEXT;",
                true, true
            ),
            new MigrationRule(
                "Colors: Cleanup markers",
                @"SELECT COUNT(*) FROM players 
                  WHERE (txt_color IN ('0', '')) 
                     OR (stm_color IN ('0', '')) 
                     OR (alias_color IN ('0', ''))",
                @"UPDATE players SET txt_color = NULL WHERE txt_color IN ('0', '');
                  UPDATE players SET stm_color = NULL WHERE stm_color IN ('0', '');
                  UPDATE players SET alias_color = NULL WHERE alias_color IN ('0', '');"
            ),
            new MigrationRule(
                "Index: Cleanup & Optimization",
                "SELECT COUNT(*) FROM sqlite_master WHERE type='index' AND name='idx_vac_status'",
                @"CREATE INDEX IF NOT EXISTS idx_comm_status ON players(is_community_banned, profile_status);
                  CREATE INDEX IF NOT EXISTS idx_stm_color ON players(stm_color);
                  CREATE INDEX IF NOT EXISTS idx_game_status ON players(number_of_game_bans, ban_date);
                  CREATE INDEX IF NOT EXISTS idx_vac_status ON players(number_of_vac_bans, last_vac_check);",
                true, true
            ),
            new MigrationRule(
                "Cleanup: Remove legacy logs table",
                "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='logs'",
                "DROP TABLE IF EXISTS logs;",
                true, false
            )
        };

        public DatabaseMigrationService()
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            _dbPath = Path.Combine(appData, "GSRP", "gsrp.db");
            _connectionString = $"Data Source={_dbPath};Mode=ReadWriteCreate;Cache=Shared";
        }

        public async Task<int> GetTotalAffectedCountAsync()
        {
            return await Task.Run(() => {
                int total = 0;
                try {
                    using var conn = new SqliteConnection(_connectionString);
                    conn.Open();
                    foreach (var rule in _rules) {
                        try {
                            using var cmd = conn.CreateCommand();
                            cmd.CommandText = rule.CheckSql;
                            var result = Convert.ToInt32(cmd.ExecuteScalar());
                            
                            if (rule.IsSchemaChange) {
                                if (result == 0) total += 1;
                            } else {
                                total += result;
                            }
                        } catch { 
                            if (!rule.IsSchemaChange) total += 1; 
                        }
                    }
                } catch { }
                return total;
            });
        }

        public async Task RunMigrationsAsync(bool backup)
        {
            await Task.Run(() => {
                try {
                    if (backup && File.Exists(_dbPath)) {
                        var bPath = _dbPath + ".bak_" + DateTime.Now.ToString("yyyyMMdd_HHmmss");
                        File.Copy(_dbPath, bPath, true);
                    }

                    using var conn = new SqliteConnection(_connectionString);
                    conn.Open();
                    foreach (var rule in _rules) {
                        try {
                            bool needsFix = false;
                            using (var checkCmd = conn.CreateCommand()) {
                                checkCmd.CommandText = rule.CheckSql;
                                var res = Convert.ToInt32(checkCmd.ExecuteScalar());
                                needsFix = rule.IsSchemaChange ? (res == 0) : (res > 0);
                            }

                            if (needsFix) {
                                using var fixCmd = conn.CreateCommand();
                                fixCmd.CommandText = rule.FixSql;
                                fixCmd.ExecuteNonQuery();
                            }
                        } catch { 
                            try {
                                using var fixCmd = conn.CreateCommand();
                                fixCmd.CommandText = rule.FixSql;
                                fixCmd.ExecuteNonQuery();
                            } catch { }
                        }
                    }
                } catch { }
            });
        }
    }
}
