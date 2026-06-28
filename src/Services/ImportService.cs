using System.IO;
using System.Linq;
using System.Collections.Generic;
using Microsoft.Data.Sqlite;
using AssetInventory.Data;
using AssetInventory.Core;

namespace AssetInventory.Services
{
    public static class ImportService
    {
        // إصلاح #8: دعم قيم CSV المُحاطة بعلامات اقتباس (RFC 4180)
        // سابقاً: Split(',') يكسر أي قيمة تحتوي فاصلة داخل علامات اقتباس
        private static string[] ParseCsvLine(string line)
        {
            var fields = new List<string>();
            bool inQuotes = false;
            var current = new System.Text.StringBuilder();

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (c == '"')
                {
                    if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                    {
                        current.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = !inQuotes;
                    }
                }
                else if (c == ',' && !inQuotes)
                {
                    fields.Add(current.ToString());
                    current.Clear();
                }
                else
                {
                    current.Append(c);
                }
            }
            fields.Add(current.ToString());
            return fields.ToArray();
        }

        public static void ImportFromCsv(string filePath, DatabaseService db)
        {
            var lines = File.ReadAllLines(filePath);
            db.ExecuteTransaction((conn, trans) =>
            {
                foreach (var line in lines.Skip(1))
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;

                    var parts = ParseCsvLine(line);
                    if (parts.Length < 4) continue;

                    // تطهير رقم TAG باستخدام ScannerService
                    var tag = ScannerService.Sanitize(parts[0].Trim());
                    if (string.IsNullOrEmpty(tag)) continue;

                    var cmd = conn.CreateCommand();
                    cmd.Transaction = trans; // ربط الـ command بالـ transaction الصحيحة
                    cmd.CommandText = "INSERT OR REPLACE INTO Assets (Tag, Desc, Loc, Status) VALUES (@tag, @desc, @loc, @stat)";
                    cmd.Parameters.AddWithValue("@tag", tag);
                    cmd.Parameters.AddWithValue("@desc", parts[1].Trim());
                    cmd.Parameters.AddWithValue("@loc", parts[2].Trim());
                    cmd.Parameters.AddWithValue("@stat", parts[3].Trim());
                    cmd.ExecuteNonQuery();

                    var auditCmd = conn.CreateCommand();
                    auditCmd.Transaction = trans;
                    auditCmd.CommandText = @"
                        INSERT INTO AuditLogs (AssetTag, FieldChanged, OldValue, NewValue, ChangedBy, DeviceName)
                        VALUES (@tag, 'Import', 'None', 'CSV Import', @user, @device)";
                    auditCmd.Parameters.AddWithValue("@tag", tag);
                    auditCmd.Parameters.AddWithValue("@user", System.Environment.UserName);
                    auditCmd.Parameters.AddWithValue("@device", System.Environment.MachineName);
                    auditCmd.ExecuteNonQuery();
                }
            });
        }
    }
}
