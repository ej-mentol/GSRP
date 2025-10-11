using Microsoft.Data.Sqlite;
using System.Collections.Generic;
using System.Linq;

namespace GSRP.Services
{
    public class DatabaseMigrationService : IDatabaseMigrationService
    {
        public void ApplyMigrations(SqliteConnection connection)
        {
            AddAliasColorColumn(connection);
            AddGameBansColumn(connection);
            // Future migrations will be called here
        }

        private void AddGameBansColumn(SqliteConnection connection)
        {
            if (!ColumnExists(connection, "players", "number_of_game_bans"))
            {
                var command = connection.CreateCommand();
                command.CommandText = "ALTER TABLE players ADD COLUMN number_of_game_bans INTEGER NOT NULL DEFAULT 0;";
                command.ExecuteNonQuery();
            }
        }

        private void AddAliasColorColumn(SqliteConnection connection)
        {
            if (!ColumnExists(connection, "players", "alias_color"))
            {
                var command = connection.CreateCommand();
                command.CommandText = "ALTER TABLE players ADD COLUMN alias_color INTEGER NOT NULL DEFAULT 0;";
                command.ExecuteNonQuery();
            }
        }

        private bool ColumnExists(SqliteConnection connection, string tableName, string columnName)
        {
            var command = connection.CreateCommand();
            command.CommandText = $"PRAGMA table_info({tableName});";

            using (var reader = command.ExecuteReader())
            {
                while (reader.Read())
                {
                    // The column name is in the second position (index 1)
                    if (reader.GetString(1).Equals(columnName, System.StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }
            return false;
        }
    }
}
