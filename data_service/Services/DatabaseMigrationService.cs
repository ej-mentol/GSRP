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

        public MigrationRule(string name, string checkSql, string fixSql, bool enabled = true) {
            Name = name;
            CheckSql = checkSql;
            FixSql = fixSql;
            Enabled = enabled;
        }
    }

    public class DatabaseMigrationService
    {
        private readonly string _connectionString;
        private readonly string _dbPath;

        // --- REGISTRY OF MIGRATION RULES ---
        private readonly List<MigrationRule> _rules = new()
        {
            new MigrationRule(
                "Colors: Cleanup 0 and Empty strings",
                @"SELECT COUNT(*) FROM players 
                  WHERE (txt_color IN ('0', '')) 
                     OR (stm_color IN ('0', '')) 
                     OR (alias_color IN ('0', ''))",
                @"UPDATE players SET txt_color = NULL WHERE txt_color IN ('0', '');
                  UPDATE players SET stm_color = NULL WHERE stm_color IN ('0', '');
                  UPDATE players SET alias_color = NULL WHERE alias_color IN ('0', '');"
            ),
            new MigrationRule(
                "Economy: Cleanup 'none' and '0' markers",
                "SELECT COUNT(*) FROM players WHERE economy_ban IN ('none', '0', '')",
                "UPDATE players SET economy_ban = NULL WHERE economy_ban IN ('none', '0', '');"
            )
        };

        public DatabaseMigrationService()
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            _dbPath = Path.Combine(appData, "GSRP", "gsrp.db");
            _connectionString = $"Data Source={_dbPath}";
        }

        public async Task<int> GetTotalAffectedCountAsync()
        {
            return await Task.Run(() => {
                int total = 0;
                try {
                    using var conn = new SqliteConnection(_connectionString);
                    conn.Open();
                    foreach (var rule in _rules) {
                        if (!rule.Enabled) continue;
                        using var cmd = conn.CreateCommand();
                        cmd.CommandText = rule.CheckSql;
                        total += Convert.ToInt32(cmd.ExecuteScalar());
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
                    using var trans = conn.BeginTransaction();
                    foreach (var rule in _rules) {
                        if (!rule.Enabled) continue;
                        using var cmd = conn.CreateCommand();
                        cmd.Transaction = trans;
                        cmd.CommandText = rule.FixSql;
                        cmd.ExecuteNonQuery();
                    }
                    trans.Commit();
                } catch { }
            });
        }
    }
}
