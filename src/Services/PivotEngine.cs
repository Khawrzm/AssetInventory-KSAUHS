using System;
using System.Collections.Generic;
using System.Data;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using AssetInventory.Core;

namespace AssetInventory.Services;

public class PivotEngine
{
    private readonly string _connStr;

    public PivotEngine()
    {
        var config = ConfigService.Load();
        var dbPath = config.DatabasePath;
        var password = EncryptionService.GetDatabasePassword();
        
        if (string.IsNullOrEmpty(password))
        {
            _connStr = $"Data Source={dbPath}";
        }
        else
        {
            _connStr = $"Data Source={dbPath};Password={password};";
        }
    }

    /// <summary>
    /// Fetches all assets and aggregates values in-memory to prevent database locking and external dependency issues.
    /// </summary>
    public string GetExecutivePivotJson()
    {
        var rawRows = new List<PivotRawRow>();

        using (var conn = new SqliteConnection(_connStr))
        {
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT Loc, Status, Note FROM Assets";

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                string loc = reader.IsDBNull(0) ? "N/A" : reader.GetString(0);
                string status = reader.IsDBNull(1) ? "PENDING" : reader.GetString(1);
                string note = reader.IsDBNull(2) ? "" : reader.GetString(2);

                if (string.IsNullOrEmpty(loc)) loc = "N/A";

                // Parse numeric value from note (which stores the depreciated or initial value)
                double val = 500.0;
                if (double.TryParse(note, out double parsedVal))
                {
                    val = parsedVal;
                }

                rawRows.Add(new PivotRawRow
                {
                    Location = loc,
                    Status = status.ToUpperInvariant(),
                    Value = val
                });
            }
        }

        // Pivot the records grouped by Location
        var pivotDict = new Dictionary<string, Dictionary<string, double>>();
        var targetStatuses = new HashSet<string> { "PENDING", "VERIFIED", "DISPOSED", "TRANSFERRED" };

        foreach (var row in rawRows)
        {
            if (!pivotDict.ContainsKey(row.Location))
            {
                pivotDict[row.Location] = new Dictionary<string, double>();
                foreach (var status in targetStatuses)
                {
                    pivotDict[row.Location][status] = 0.0;
                }
            }

            if (targetStatuses.Contains(row.Status))
            {
                pivotDict[row.Location][row.Status] += row.Value;
            }
            else
            {
                // Group any unexpected status under PENDING
                pivotDict[row.Location]["PENDING"] += row.Value;
            }
        }

        // Format for JSON serialization
        var resultList = new List<Dictionary<string, object>>();
        foreach (var kvp in pivotDict)
        {
            var dict = new Dictionary<string, object>
            {
                { "Location", kvp.Key }
            };
            foreach (var statusKvp in kvp.Value)
            {
                dict[statusKvp.Key] = statusKvp.Value;
            }
            resultList.Add(dict);
        }

        return JsonSerializer.Serialize(resultList);
    }

    private class PivotRawRow
    {
        public string Location { get; set; } = "N/A";
        public string Status { get; set; } = "PENDING";
        public double Value { get; set; }
    }
}
