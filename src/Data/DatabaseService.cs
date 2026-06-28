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

        // إصلاح #5: تمرير الـ transaction للـ action حتى تستطيع كل command أن تنتمي للـ transaction
        // سابقاً: commands داخل action لا تعرف عن الـ transaction فكانت تُنفَّذ خارجها
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
    }
}
