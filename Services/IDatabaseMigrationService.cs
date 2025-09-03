using Microsoft.Data.Sqlite;

namespace GSRP.Services
{
    public interface IDatabaseMigrationService
    {
        void ApplyMigrations(SqliteConnection connection);
    }
}
