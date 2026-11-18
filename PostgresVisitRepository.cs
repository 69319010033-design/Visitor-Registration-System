using Npgsql;
using NpgsqlTypes;

namespace VisitorLog;

/// <summary>เก็บข้อมูลใน Supabase (Postgres) ผ่าน Npgsql ตาราง public.visits (ดู supabase/schema.sql)</summary>
public sealed class PostgresVisitRepository : IVisitRepository
{
    private const string Columns =
        "id, visitor_name, organization, host_name, purpose, check_in, check_out, " +
        "is_cancelled, cancel_reason, cancelled_at";

    private readonly NpgsqlDataSource _db;

    public PostgresVisitRepository(string connectionString)
    {
        _db = NpgsqlDataSource.Create(Normalize(connectionString));
    }

    /// <summary>ใช้ตรวจว่าเชื่อมต่อได้ และตารางถูกสร้างแล้ว</summary>
    public async Task<int> CountAsync(CancellationToken ct = default)
    {
        await using var cmd = _db.CreateCommand("select count(*) from public.visits");
        return Convert.ToInt32(await cmd.ExecuteScalarAsync(ct));
    }

    public async Task<Visit> AddAsync(Visit visit, CancellationToken ct = default)
    {
        await using var cmd = _db.CreateCommand(
            "insert into public.visits (visitor_name, organization, host_name, purpose, check_in) " +
            "values (@name, @org, @host, @purpose, @checkIn) returning " + Columns);
        cmd.Parameters.AddWithValue("name", NpgsqlDbType.Text, visit.VisitorName);
        cmd.Parameters.AddWithValue("org", NpgsqlDbType.Text, visit.Organization);
        cmd.Parameters.AddWithValue("host", NpgsqlDbType.Text, visit.HostName);
        cmd.Parameters.AddWithValue("purpose", NpgsqlDbType.Text, visit.Purpose);
        cmd.Parameters.AddWithValue("checkIn", NpgsqlDbType.Timestamp, Wall(visit.CheckIn));

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        await reader.ReadAsync(ct);
        return Read(reader);
    }

    public async Task<Visit?> FindAsync(int id, CancellationToken ct = default)
    {
        await using var cmd = _db.CreateCommand(
            "select " + Columns + " from public.visits where id = @id");
        cmd.Parameters.AddWithValue("id", NpgsqlDbType.Integer, id);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct) ? Read(reader) : null;
    }

    public async Task<IReadOnlyList<Visit>> ListAsync(CancellationToken ct = default)
    {
        await using var cmd = _db.CreateCommand(
            "select " + Columns + " from public.visits order by check_in, id");

        var list = new List<Visit>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            list.Add(Read(reader));
        return list;
    }

    public async Task SaveAsync(Visit visit, CancellationToken ct = default)
    {
        await using var cmd = _db.CreateCommand(
            "update public.visits set check_out = @checkOut, is_cancelled = @cancelled, " +
            "cancel_reason = @reason, cancelled_at = @cancelledAt where id = @id");

        cmd.Parameters.AddWithValue("checkOut", NpgsqlDbType.Timestamp,
            visit.CheckOut.HasValue ? (object)Wall(visit.CheckOut.Value) : DBNull.Value);
        cmd.Parameters.AddWithValue("cancelled", NpgsqlDbType.Boolean, visit.IsCancelled);
        cmd.Parameters.AddWithValue("reason", NpgsqlDbType.Text,
            visit.CancelReason is not null ? (object)visit.CancelReason : DBNull.Value);
        cmd.Parameters.AddWithValue("cancelledAt", NpgsqlDbType.Timestamp,
            visit.CancelledAt.HasValue ? (object)Wall(visit.CancelledAt.Value) : DBNull.Value);
        cmd.Parameters.AddWithValue("id", NpgsqlDbType.Integer, visit.Id);

        var rows = await cmd.ExecuteNonQueryAsync(ct);
        if (rows == 0) throw new KeyNotFoundException($"ไม่พบรายการ #{visit.Id}");
    }

    // ---------- ตัวช่วย ----------

    // เก็บเวลาแบบ "เวลาบนนาฬิกา" (timestamp ไม่มีโซนเวลา) ตามที่เจ้าหน้าที่กรอก
    private static DateTime Wall(DateTime value) =>
        DateTime.SpecifyKind(value, DateTimeKind.Unspecified);

    private static Visit Read(NpgsqlDataReader r) => new()
    {
        Id = r.GetInt32(0),
        VisitorName = r.GetString(1),
        Organization = r.GetString(2),
        HostName = r.GetString(3),
        Purpose = r.GetString(4),
        CheckIn = r.GetDateTime(5),
        CheckOut = r.IsDBNull(6) ? null : r.GetDateTime(6),
        IsCancelled = r.GetBoolean(7),
        CancelReason = r.IsDBNull(8) ? null : r.GetString(8),
        CancelledAt = r.IsDBNull(9) ? null : r.GetDateTime(9)
    };

    // รับได้ทั้งรูปแบบ Npgsql (Host=...;Username=...) และ URI ที่ Supabase แสดง (postgresql://...)
    private static string Normalize(string connectionString)
    {
        var text = connectionString.Trim();
        var isUri = text.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase)
                 || text.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase);
        if (!isUri) return text;

        var uri = new Uri(text);
        var userInfo = uri.UserInfo.Split(':', 2);

        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            Port = uri.Port > 0 ? uri.Port : 5432,
            Username = Uri.UnescapeDataString(userInfo[0]),
            Password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : null,
            Database = uri.AbsolutePath.Trim('/'),
            SslMode = SslMode.Require,
            TrustServerCertificate = true
        };
        return builder.ConnectionString;
    }
}
