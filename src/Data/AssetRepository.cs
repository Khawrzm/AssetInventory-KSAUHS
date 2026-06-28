using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using AssetInventory.Core;
using AssetInventory.Models;

namespace AssetInventory.Data;

public class AssetRepository
{
    private string ConnStr
    {
        get
        {
            var dbPath = ConfigService.Load().DatabasePath;
            var password = EncryptionService.GetDatabasePassword();
            if (string.IsNullOrEmpty(password))
            {
                return $"Data Source={dbPath}";
            }
            return $"Data Source={dbPath};Password={password};";
        }
    }

    public AssetRepository()
    {
        using var conn = new SqliteConnection(ConnStr);
        conn.Open();
        using var cmd = conn.CreateCommand();
        
        // Enable WAL mode for better concurrency and corruption protection
        cmd.CommandText = "PRAGMA journal_mode=WAL; PRAGMA synchronous=NORMAL;";
        cmd.ExecuteNonQuery();

        cmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS Assets (
                Tag       TEXT PRIMARY KEY,
                Desc      TEXT NOT NULL DEFAULT '',
                Loc       TEXT NOT NULL DEFAULT '',
                Minor     TEXT NOT NULL DEFAULT '',
                Status    TEXT NOT NULL DEFAULT 'PENDING',
                Hash      TEXT,
                Note      TEXT NOT NULL DEFAULT '',
                CreatedAt TEXT NOT NULL DEFAULT (datetime('now')),
                UpdatedAt TEXT NOT NULL DEFAULT (datetime('now'))
            );
            CREATE TABLE IF NOT EXISTS AuditLogs (
                Id           INTEGER PRIMARY KEY AUTOINCREMENT,
                AssetTag     TEXT NOT NULL,
                FieldChanged TEXT NOT NULL,
                OldValue     TEXT,
                NewValue     TEXT,
                ChangedBy    TEXT NOT NULL,
                DeviceName   TEXT NOT NULL,
                Timestamp    TEXT NOT NULL DEFAULT (datetime('now'))
            );";
        cmd.ExecuteNonQuery();

        // Migrate older DBs that are missing newer columns
        foreach (var col in new[] {
            "ALTER TABLE Assets ADD COLUMN Note TEXT NOT NULL DEFAULT ''",
            "ALTER TABLE Assets ADD COLUMN CreatedAt TEXT NOT NULL DEFAULT (datetime('now'))",
            "ALTER TABLE Assets ADD COLUMN UpdatedAt TEXT NOT NULL DEFAULT (datetime('now'))",
        })
        {
            try { cmd.CommandText = col; cmd.ExecuteNonQuery(); } catch { /* already exists */ }
        }
    }

    public int GetFilteredCount(string? statusFilter, string? searchQuery)
    {
        using var conn = new SqliteConnection(ConnStr);
        conn.Open();
        using var cmd = conn.CreateCommand();
        var sql = "SELECT COUNT(*) FROM Assets WHERE 1=1";
        if (!string.IsNullOrEmpty(statusFilter))
        {
            sql += " AND Status=@status";
            cmd.Parameters.AddWithValue("@status", statusFilter);
        }
        if (!string.IsNullOrEmpty(searchQuery))
        {
            sql += " AND (Tag LIKE @q OR Desc LIKE @q OR Loc LIKE @q OR Note LIKE @q)";
            cmd.Parameters.AddWithValue("@q", $"%{searchQuery}%");
        }
        cmd.CommandText = sql;
        return Convert.ToInt32(cmd.ExecuteScalar());
    }

    public List<Asset> GetFilteredPage(string? statusFilter, string? searchQuery, int offset, int limit, string sortCol = "Tag", bool sortAsc = true)
    {
        var list = new List<Asset>();
        using var conn = new SqliteConnection(ConnStr);
        conn.Open();
        using var cmd = conn.CreateCommand();
        var sql = "SELECT Tag,Desc,Loc,Minor,Status,Hash,Note FROM Assets WHERE 1=1";
        if (!string.IsNullOrEmpty(statusFilter))
        {
            sql += " AND Status=@status";
            cmd.Parameters.AddWithValue("@status", statusFilter);
        }
        if (!string.IsNullOrEmpty(searchQuery))
        {
            sql += " AND (Tag LIKE @q OR Desc LIKE @q OR Loc LIKE @q OR Note LIKE @q)";
            cmd.Parameters.AddWithValue("@q", $"%{searchQuery}%");
        }
        
        string orderCol = sortCol switch
        {
            "TagNumber" => "Tag",
            "AssetDescription" => "Desc",
            "MajorLoc" => "Loc",
            "MinorLoc" => "Minor",
            "Status" => "Status",
            "Note" => "Note",
            _ => "Tag"
        };
        
        sql += $" ORDER BY [{orderCol}] {(sortAsc ? "ASC" : "DESC")} LIMIT @limit OFFSET @offset";
        cmd.Parameters.AddWithValue("@limit", limit);
        cmd.Parameters.AddWithValue("@offset", offset);
        
        cmd.CommandText = sql;
        using var r = cmd.ExecuteReader();
        while (r.Read()) list.Add(Map(r));
        return list;
    }

    // ── Synchronous Methods ──────────────────────────────────────────────────

    public List<Asset> GetAll()
    {
        var list = new List<Asset>();
        using var conn = new SqliteConnection(ConnStr);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Tag,Desc,Loc,Minor,Status,Hash,Note FROM Assets ORDER BY Tag";
        using var r = cmd.ExecuteReader();
        while (r.Read()) list.Add(Map(r));
        return list;
    }

    public List<string> GetAllStatuses()
    {
        var list = new List<string>();
        using var conn = new SqliteConnection(ConnStr);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Status FROM Assets";
        using var r = cmd.ExecuteReader();
        while (r.Read()) list.Add(r.IsDBNull(0) ? "PENDING" : r.GetString(0));
        return list;
    }

    public List<Asset> GetByStatus(string status)
    {
        var list = new List<Asset>();
        using var conn = new SqliteConnection(ConnStr);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Tag,Desc,Loc,Minor,Status,Hash,Note FROM Assets WHERE Status=@s ORDER BY Tag";
        cmd.Parameters.AddWithValue("@s", status);
        using var r = cmd.ExecuteReader();
        while (r.Read()) list.Add(Map(r));
        return list;
    }

    public AssetStats GetStats()
    {
        using var conn = new SqliteConnection(ConnStr);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT
                COUNT(*)                                              AS Total,
                SUM(CASE WHEN Status='VERIFIED'    THEN 1 ELSE 0 END) AS Verified,
                SUM(CASE WHEN Status='PENDING'     THEN 1 ELSE 0 END) AS Pending,
                SUM(CASE WHEN Status='DISPOSED'    THEN 1 ELSE 0 END) AS Disposed,
                SUM(CASE WHEN Status='TRANSFERRED' THEN 1 ELSE 0 END) AS Transferred
            FROM Assets";
        using var r = cmd.ExecuteReader();
        return r.Read()
            ? new AssetStats(r.GetInt32(0), r.GetInt32(1), r.GetInt32(2), r.GetInt32(3), r.GetInt32(4))
            : new AssetStats(0, 0, 0, 0, 0);
    }

    public Dictionary<string, int> GetLocationStats()
    {
        var dict = new Dictionary<string, int>();
        using var conn = new SqliteConnection(ConnStr);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT Loc, COUNT(*) AS Cnt
            FROM Assets
            WHERE Loc <> ''
            GROUP BY Loc
            ORDER BY Cnt DESC";
        using var r = cmd.ExecuteReader();
        while (r.Read())
            dict[r.GetString(0)] = r.GetInt32(1);
        return dict;
    }

    public Asset? GetByTag(string tag)
    {
        using var conn = new SqliteConnection(ConnStr);
        conn.Open();
        return GetByTag(tag, conn, null);
    }

    private Asset? GetByTag(string tag, SqliteConnection conn, SqliteTransaction trans)
    {
        using var cmd = conn.CreateCommand();
        cmd.Transaction = trans;
        cmd.CommandText = "SELECT Tag,Desc,Loc,Minor,Status,Hash,Note FROM Assets WHERE Tag=@tag";
        cmd.Parameters.AddWithValue("@tag", tag);
        using var r = cmd.ExecuteReader();
        if (r.Read()) return Map(r);
        return null;
    }

    private void WriteAuditLog(SqliteConnection conn, SqliteTransaction trans, string assetTag, string fieldChanged, string? oldValue, string? newValue, string? changedBy = null)
    {
        using var cmd = conn.CreateCommand();
        cmd.Transaction = trans;
        cmd.CommandText = @"
            INSERT INTO AuditLogs (AssetTag, FieldChanged, OldValue, NewValue, ChangedBy, DeviceName)
            VALUES (@tag, @field, @old, @new, @user, @device)";
        cmd.Parameters.AddWithValue("@tag", assetTag);
        cmd.Parameters.AddWithValue("@field", fieldChanged);
        cmd.Parameters.AddWithValue("@old", (object?)oldValue ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@new", (object?)newValue ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@user", changedBy ?? Environment.UserName);
        cmd.Parameters.AddWithValue("@device", Environment.MachineName);
        cmd.ExecuteNonQuery();
    }

    public void Save(Asset asset, string? changedBy = null)
    {
        using var conn  = new SqliteConnection(ConnStr);
        conn.Open();
        using var trans = conn.BeginTransaction();

        var old = GetByTag(asset.TagNumber, conn, trans);

        using var cmd   = conn.CreateCommand();
        cmd.Transaction = trans;
        cmd.CommandText = @"
            INSERT INTO Assets(Tag,Desc,Loc,Minor,Status,Hash,Note,CreatedAt,UpdatedAt)
            VALUES(@tag,@desc,@loc,@minor,@stat,@hash,@note,datetime('now'),datetime('now'))
            ON CONFLICT(Tag) DO UPDATE SET
                Desc=excluded.Desc,
                Loc=excluded.Loc,
                Minor=excluded.Minor,
                Status=excluded.Status,
                Hash=excluded.Hash,
                Note=excluded.Note,
                UpdatedAt=datetime('now')";
        cmd.Parameters.AddWithValue("@tag",   asset.TagNumber);
        cmd.Parameters.AddWithValue("@desc",  asset.AssetDescription);
        cmd.Parameters.AddWithValue("@loc",   asset.MajorLoc);
        cmd.Parameters.AddWithValue("@minor", asset.MinorLoc);
        cmd.Parameters.AddWithValue("@stat",  asset.Status);
        cmd.Parameters.AddWithValue("@hash",  asset.DataHash);
        cmd.Parameters.AddWithValue("@note",  asset.Note);
        cmd.ExecuteNonQuery();

        if (old == null)
        {
            WriteAuditLog(conn, trans, asset.TagNumber, "Create", null, "Asset Created", changedBy);
        }
        else
        {
            if (old.AssetDescription != asset.AssetDescription)
                WriteAuditLog(conn, trans, asset.TagNumber, "AssetDescription", old.AssetDescription, asset.AssetDescription, changedBy);
            if (old.MajorLoc != asset.MajorLoc)
                WriteAuditLog(conn, trans, asset.TagNumber, "MajorLoc", old.MajorLoc, asset.MajorLoc, changedBy);
            if (old.MinorLoc != asset.MinorLoc)
                WriteAuditLog(conn, trans, asset.TagNumber, "MinorLoc", old.MinorLoc, asset.MinorLoc, changedBy);
            if (old.Status != asset.Status)
                WriteAuditLog(conn, trans, asset.TagNumber, "Status", old.Status, asset.Status, changedBy);
            if (old.Note != asset.Note)
                WriteAuditLog(conn, trans, asset.TagNumber, "Note", old.Note, asset.Note, changedBy);
        }

        trans.Commit();
    }

    public void BulkSetStatus(IEnumerable<string> tags, string newStatus)
    {
        using var conn  = new SqliteConnection(ConnStr);
        conn.Open();
        using var trans = conn.BeginTransaction();

        foreach (var tag in tags)
        {
            var old = GetByTag(tag, conn, trans);
            string? oldStatus = old?.Status;

            using var cmd = conn.CreateCommand();
            cmd.Transaction = trans;
            cmd.CommandText = "UPDATE Assets SET Status=@s, UpdatedAt=datetime('now') WHERE Tag=@t";
            cmd.Parameters.AddWithValue("@s", newStatus);
            cmd.Parameters.AddWithValue("@t", tag);
            cmd.ExecuteNonQuery();

            if (oldStatus != newStatus)
            {
                WriteAuditLog(conn, trans, tag, "Status", oldStatus, newStatus);
            }
        }
        trans.Commit();
    }

    public void Delete(string tag)
    {
        using var conn  = new SqliteConnection(ConnStr);
        conn.Open();
        using var trans = conn.BeginTransaction();

        var old = GetByTag(tag, conn, trans);

        using var cmd   = conn.CreateCommand();
        cmd.Transaction = trans;
        cmd.CommandText = "DELETE FROM Assets WHERE Tag=@tag";
        cmd.Parameters.AddWithValue("@tag", tag);
        cmd.ExecuteNonQuery();

        if (old != null)
        {
            WriteAuditLog(conn, trans, tag, "Delete", old.Status, "Asset Deleted");
        }

        trans.Commit();
    }

    // ── Asynchronous Methods (Non-blocking DB operations) ──────────────────────

    public async Task<List<Asset>> GetAllAsync()
    {
        var list = new List<Asset>();
        using var conn = new SqliteConnection(ConnStr);
        await conn.OpenAsync();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Tag,Desc,Loc,Minor,Status,Hash,Note FROM Assets ORDER BY Tag";
        using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync()) list.Add(Map(r));
        return list;
    }

    public async Task<List<Asset>> GetByStatusAsync(string status)
    {
        var list = new List<Asset>();
        using var conn = new SqliteConnection(ConnStr);
        await conn.OpenAsync();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Tag,Desc,Loc,Minor,Status,Hash,Note FROM Assets WHERE Status=@s ORDER BY Tag";
        cmd.Parameters.AddWithValue("@s", status);
        using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync()) list.Add(Map(r));
        return list;
    }

    public async Task<AssetStats> GetStatsAsync()
    {
        using var conn = new SqliteConnection(ConnStr);
        await conn.OpenAsync();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT
                COUNT(*)                                              AS Total,
                SUM(CASE WHEN Status='VERIFIED'    THEN 1 ELSE 0 END) AS Verified,
                SUM(CASE WHEN Status='PENDING'     THEN 1 ELSE 0 END) AS Pending,
                SUM(CASE WHEN Status='DISPOSED'    THEN 1 ELSE 0 END) AS Disposed,
                SUM(CASE WHEN Status='TRANSFERRED' THEN 1 ELSE 0 END) AS Transferred
            FROM Assets";
        using var r = await cmd.ExecuteReaderAsync();
        return await r.ReadAsync()
            ? new AssetStats(r.GetInt32(0), r.GetInt32(1), r.GetInt32(2), r.GetInt32(3), r.GetInt32(4))
            : new AssetStats(0, 0, 0, 0, 0);
    }

    public async Task<Dictionary<string, int>> GetLocationStatsAsync()
    {
        var dict = new Dictionary<string, int>();
        using var conn = new SqliteConnection(ConnStr);
        await conn.OpenAsync();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT Loc, COUNT(*) AS Cnt
            FROM Assets
            WHERE Loc <> ''
            GROUP BY Loc
            ORDER BY Cnt DESC";
        using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
            dict[r.GetString(0)] = r.GetInt32(1);
        return dict;
    }

    public async Task<Asset?> GetByTagAsync(string tag)
    {
        using var conn = new SqliteConnection(ConnStr);
        await conn.OpenAsync();
        return await GetByTagAsync(tag, conn, null);
    }

    private async Task<Asset?> GetByTagAsync(string tag, SqliteConnection conn, SqliteTransaction trans)
    {
        using var cmd = conn.CreateCommand();
        cmd.Transaction = trans;
        cmd.CommandText = "SELECT Tag,Desc,Loc,Minor,Status,Hash,Note FROM Assets WHERE Tag=@tag";
        cmd.Parameters.AddWithValue("@tag", tag);
        using var r = await cmd.ExecuteReaderAsync();
        if (await r.ReadAsync()) return Map(r);
        return null;
    }

    private async Task WriteAuditLogAsync(SqliteConnection conn, SqliteTransaction trans, string assetTag, string fieldChanged, string? oldValue, string? newValue, string? changedBy = null)
    {
        using var cmd = conn.CreateCommand();
        cmd.Transaction = trans;
        cmd.CommandText = @"
            INSERT INTO AuditLogs (AssetTag, FieldChanged, OldValue, NewValue, ChangedBy, DeviceName)
            VALUES (@tag, @field, @old, @new, @user, @device)";
        cmd.Parameters.AddWithValue("@tag", assetTag);
        cmd.Parameters.AddWithValue("@field", fieldChanged);
        cmd.Parameters.AddWithValue("@old", (object?)oldValue ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@new", (object?)newValue ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@user", changedBy ?? Environment.UserName);
        cmd.Parameters.AddWithValue("@device", Environment.MachineName);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task SaveAsync(Asset asset, string? changedBy = null)
    {
        using var conn  = new SqliteConnection(ConnStr);
        await conn.OpenAsync();
        using var trans = conn.BeginTransaction();

        var old = await GetByTagAsync(asset.TagNumber, conn, trans);

        using var cmd   = conn.CreateCommand();
        cmd.Transaction = trans;
        cmd.CommandText = @"
            INSERT INTO Assets(Tag,Desc,Loc,Minor,Status,Hash,Note,CreatedAt,UpdatedAt)
            VALUES(@tag,@desc,@loc,@minor,@stat,@hash,@note,datetime('now'),datetime('now'))
            ON CONFLICT(Tag) DO UPDATE SET
                Desc=excluded.Desc,
                Loc=excluded.Loc,
                Minor=excluded.Minor,
                Status=excluded.Status,
                Hash=excluded.Hash,
                Note=excluded.Note,
                UpdatedAt=datetime('now')";
        cmd.Parameters.AddWithValue("@tag",   asset.TagNumber);
        cmd.Parameters.AddWithValue("@desc",  asset.AssetDescription);
        cmd.Parameters.AddWithValue("@loc",   asset.MajorLoc);
        cmd.Parameters.AddWithValue("@minor", asset.MinorLoc);
        cmd.Parameters.AddWithValue("@stat",  asset.Status);
        cmd.Parameters.AddWithValue("@hash",  asset.DataHash);
        cmd.Parameters.AddWithValue("@note",  asset.Note);
        await cmd.ExecuteNonQueryAsync();

        if (old == null)
        {
            await WriteAuditLogAsync(conn, trans, asset.TagNumber, "Create", null, "Asset Created", changedBy);
        }
        else
        {
            if (old.AssetDescription != asset.AssetDescription)
                await WriteAuditLogAsync(conn, trans, asset.TagNumber, "AssetDescription", old.AssetDescription, asset.AssetDescription, changedBy);
            if (old.MajorLoc != asset.MajorLoc)
                await WriteAuditLogAsync(conn, trans, asset.TagNumber, "MajorLoc", old.MajorLoc, asset.MajorLoc, changedBy);
            if (old.MinorLoc != asset.MinorLoc)
                await WriteAuditLogAsync(conn, trans, asset.TagNumber, "MinorLoc", old.MinorLoc, asset.MinorLoc, changedBy);
            if (old.Status != asset.Status)
                await WriteAuditLogAsync(conn, trans, asset.TagNumber, "Status", old.Status, asset.Status, changedBy);
            if (old.Note != asset.Note)
                await WriteAuditLogAsync(conn, trans, asset.TagNumber, "Note", old.Note, asset.Note, changedBy);
        }

        await trans.CommitAsync();
    }

    public async Task BulkSetStatusAsync(IEnumerable<string> tags, string newStatus)
    {
        using var conn  = new SqliteConnection(ConnStr);
        await conn.OpenAsync();
        using var trans = conn.BeginTransaction();

        foreach (var tag in tags)
        {
            var old = await GetByTagAsync(tag, conn, trans);
            string? oldStatus = old?.Status;

            using var cmd = conn.CreateCommand();
            cmd.Transaction = trans;
            cmd.CommandText = "UPDATE Assets SET Status=@s, UpdatedAt=datetime('now') WHERE Tag=@t";
            cmd.Parameters.AddWithValue("@s", newStatus);
            cmd.Parameters.AddWithValue("@t", tag);
            await cmd.ExecuteNonQueryAsync();

            if (oldStatus != newStatus)
            {
                await WriteAuditLogAsync(conn, trans, tag, "Status", oldStatus, newStatus);
            }
        }
        await trans.CommitAsync();
    }

    public async Task DeleteAsync(string tag)
    {
        using var conn  = new SqliteConnection(ConnStr);
        await conn.OpenAsync();
        using var trans = conn.BeginTransaction();

        var old = await GetByTagAsync(tag, conn, trans);

        using var cmd   = conn.CreateCommand();
        cmd.Transaction = trans;
        cmd.CommandText = "DELETE FROM Assets WHERE Tag=@tag";
        cmd.Parameters.AddWithValue("@tag", tag);
        await cmd.ExecuteNonQueryAsync();

        if (old != null)
        {
            await WriteAuditLogAsync(conn, trans, tag, "Delete", old.Status, "Asset Deleted");
        }

        await trans.CommitAsync();
    }

    private static Asset Map(SqliteDataReader r) => new()
    {
        TagNumber        = r.GetString(0),
        AssetDescription = r.IsDBNull(1) ? "" : r.GetString(1),
        MajorLoc         = r.IsDBNull(2) ? "" : r.GetString(2),
        MinorLoc         = r.IsDBNull(3) ? "" : r.GetString(3),
        Status           = r.IsDBNull(4) ? "PENDING" : r.GetString(4),
        DataHash         = r.IsDBNull(5) ? "" : r.GetString(5),
        Note             = r.IsDBNull(6) ? "" : r.GetString(6),
    };
}
