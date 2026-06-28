using System;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using AssetInventory.Core;

namespace AssetInventory.Data
{
    public class DatabaseService
    {
        private string GetConnectionString()
        {
            var dbPath = ConfigService.Load().DatabasePath;
            var password = EncryptionService.GetDatabasePassword();
            if (string.IsNullOrEmpty(password))
            {
                return $"Data Source={dbPath}";
            }
            return $"Data Source={dbPath};Password={password};";
        }

        // Synchronous transaction execution
        public void ExecuteTransaction(Action<SqliteConnection, SqliteTransaction> action)
        {
            using var conn = new SqliteConnection(GetConnectionString());
            conn.Open();
            using var trans = conn.BeginTransaction();
            try
            {
                action(conn, trans);
                trans.Commit();
            }
            catch
            {
                trans.Rollback();
                throw;
            }
        }

        // Asynchronous transaction execution to eradicate UI blocking
        public async Task ExecuteTransactionAsync(Func<SqliteConnection, SqliteTransaction, Task> action)
        {
            using var conn = new SqliteConnection(GetConnectionString());
            await conn.OpenAsync();
            using var trans = conn.BeginTransaction();
            try
            {
                await action(conn, trans);
                await trans.CommitAsync();
            }
            catch
            {
                await trans.RollbackAsync();
                throw;
            }
        }
    }
}
