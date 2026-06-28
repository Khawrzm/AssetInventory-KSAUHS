using Microsoft.Data.Sqlite;
using AssetInventory.Core;

namespace AssetInventory.Data
{
    public class DatabaseService
    {
        private string GetConnectionString() => $"Data Source={ConfigService.Load().DatabasePath}";

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
