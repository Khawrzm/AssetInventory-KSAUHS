using System;
using System.Collections.Generic;
using System.Data;
using System.Text.Json;
using DuckDB.NET.Data;
using AssetInventory.Core;

namespace AssetInventory.Services;

public class PivotEngine : IDisposable
{
    private DuckDBConnection _connection;

    public PivotEngine()
    {
        _connection = new DuckDBConnection("DataSource=:memory:");
        _connection.Open();
        InitializeSchema();
    }

    private void InitializeSchema()
    {
        var config = ConfigService.Load();
        var dbPath = config.DatabasePath;

        using var cmd = _connection.CreateCommand();
        cmd.CommandText = $"INSTALL sqlite; LOAD sqlite; ATTACH '{dbPath}' AS sqlite_db (TYPE SQLITE);";
        cmd.ExecuteNonQuery();
    }

    /// <summary>
    /// Computes high-performance OLAP pivot aggregations using DuckDB.
    /// </summary>
    public string GetExecutivePivotJson()
    {
        using var cmd = _connection.CreateCommand();
        
        cmd.CommandText = @"
            PIVOT (
                SELECT 
                    Loc AS Location, 
                    Status, 
                    COALESCE(TRY_CAST(Note AS DOUBLE), 500.0) AS AssetValue 
                FROM sqlite_db.Assets
            )
            ON Status 
            USING SUM(AssetValue) 
            GROUP BY Location
            ORDER BY Location ASC";

        using var reader = cmd.ExecuteReader();
        var dt = new DataTable();
        dt.Load(reader);

        var rows = new List<Dictionary<string, object>>();
        foreach (DataRow row in dt.Rows)
        {
            var dict = new Dictionary<string, object>();
            foreach (DataColumn col in dt.Columns)
            {
                var val = row[col];
                dict[col.ColumnName] = val == DBNull.Value ? 0.0 : val;
            }
            rows.Add(dict);
        }

        return JsonSerializer.Serialize(rows, new JsonSerializerOptions { PropertyNamingPolicy = null });
    }

    public void Dispose()
    {
        if (_connection != null)
        {
            _connection.Dispose();
            _connection = null;
        }
    }
}
