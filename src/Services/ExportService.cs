using System.IO;
using System.Collections.Generic;
using System.Text;
using AssetInventory.Models;

namespace AssetInventory.Services
{
    public static class ExportService
    {
        // إصلاح #4: تغليف أي قيمة تحتوي فاصلة أو علامة اقتباس أو سطر جديد بعلامات اقتباس
        // لمنع CSV Injection وضمان صحة الملف عند فتحه في Excel أو أي برنامج
        private static string EscapeCsvField(string value)
        {
            if (value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r'))
            {
                return $"\"{value.Replace("\"", "\"\"")}\"";
            }
            return value;
        }

        public static void ExportToCsv(List<Asset> assets, string filePath)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Tag,Description,Location,Status");
            foreach (var a in assets)
            {
                sb.AppendLine(string.Join(",",
                    EscapeCsvField(a.TagNumber),
                    EscapeCsvField(a.AssetDescription),
                    EscapeCsvField(a.MajorLoc),
                    EscapeCsvField(a.Status)));
            }
            File.WriteAllText(filePath, sb.ToString(), System.Text.Encoding.UTF8);
        }
    }
}
