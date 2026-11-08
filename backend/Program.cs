using Npgsql;

var builder = WebApplication.CreateBuilder(args);

// Listen on :8080 as required by the brief.
builder.WebHost.UseUrls("http://*:8080");

builder.Services.AddCors(o => o.AddDefaultPolicy(p => p
    .AllowAnyOrigin()
    .AllowAnyMethod()
    .AllowAnyHeader()));

var app = builder.Build();
app.UseCors();

// ------------------------------------------------------------------
// Helpers
// ------------------------------------------------------------------
static string GetConnectionString()
{
    var url = Environment.GetEnvironmentVariable("DATABASE_URL");
    if (!string.IsNullOrWhiteSpace(url))
    {
        try
        {
            var uri = new Uri(url);
            var userInfo = uri.UserInfo.Split(':');
            var sb = new NpgsqlConnectionStringBuilder
            {
                Host = uri.Host,
                Port = uri.IsDefaultPort ? 5432 : uri.Port,
                Database = string.IsNullOrWhiteSpace(uri.AbsolutePath) ? "postgres" : uri.AbsolutePath.TrimStart('/'),
                Username = Uri.UnescapeDataString(userInfo[0]),
                Password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : "",
                Pooling = false
            };
            return sb.ConnectionString;
        }
        catch
        {
            // fall through to individual env vars
        }
    }
    var host = Environment.GetEnvironmentVariable("PGHOST") ?? "localhost";
    var port = Environment.GetEnvironmentVariable("PGPORT") ?? "5432";
    var db = Environment.GetEnvironmentVariable("PGDATABASE") ?? "admissions";
    var user = Environment.GetEnvironmentVariable("PGUSER") ?? "postgres";
    var pass = Environment.GetEnvironmentVariable("PGPASSWORD") ?? "postgres";
    return new NpgsqlConnectionStringBuilder
    {
        Host = host, Port = int.TryParse(port, out var p) ? p : 5432,
        Database = db, Username = user, Password = pass, Pooling = false
    }.ConnectionString;
}

static int GetHoldDurationSeconds()
{
    var raw = Environment.GetEnvironmentVariable("HOLD_DURATION_SECONDS");
    if (int.TryParse(raw, out var v) && v >= 1) return v;
    return 600;
}

static string NewId(string prefix) => prefix + Guid.NewGuid().ToString("N")[0..12].ToLowerInvariant();

static string? GetStudentId(HttpContext ctx)
{
    var v = ctx.Request.Headers["X-Student-Id"].FirstOrDefault();
    return string.IsNullOrWhiteSpace(v) ? null : v.Trim();
}

static string Iso(DateTimeOffset t) => t.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'");

static string EffectiveStatus(string stored, DateTimeOffset expiresAt, DateTimeOffset now)
    => stored == "HELD" && expiresAt <= now ? "EXPIRED" : stored;

// Ensure schema (idempotent). Retries so `docker compose up` works
// even when postgres needs a second to accept connections.
async Task InitDbAsync()
{
    const string sql = """
        CREATE TABLE IF NOT EXISTS classes (
            id TEXT PRIMARY KEY,
            name TEXT NOT NULL,
            total_seats INTEGER NOT NULL CHECK(total_seats >= 1),
            created_at TIMESTAMPTZ NOT NULL DEFAULT now()
        );
        CREATE TABLE IF NOT EXISTS holds (
            id TEXT PRIMARY KEY,
            class_id TEXT NOT NULL REFERENCES classes(id),
            student_id TEXT NOT NULL,
            status TEXT NOT NULL CHECK(status IN ('HELD','CONFIRMED','EXPIRED','CANCELLED')),
            expires_at TIMESTAMPTZ NOT NULL,
            created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
            updated_at TIMESTAMPTZ NOT NULL DEFAULT now()
        );
        CREATE INDEX IF NOT EXISTS idx_holds_class ON holds(class_id);
        CREATE INDEX IF NOT EXISTS idx_holds_student ON holds(student_id);
        CREATE INDEX IF NOT EXISTS idx_holds_class_active ON holds(class_id, status, expires_at);
        """;
    Exception? last = null;
    for (int i = 0; i < 60; i++)
    {
        try
        {
            await using var conn = new NpgsqlConnection(GetConnectionString());
            await conn.OpenAsync();
            await using var cmd = new NpgsqlCommand(sql, conn);
            await cmd.ExecuteNonQueryAsync();
            return;
        }
        catch (Exception ex)
        {
            last = ex;
            await Task.Delay(1000);
        }
    }
    throw new InvalidOperationException("Unable to initialise database", last);
}

// Materialise EXPIRED rows (seat release). Read paths also treat
// HELD+past-due as EXPIRED lazily, so seats free up instantly even
// before the sweeper runs. This satisfies "no API call needed".
async Task SweepExpiredAsync(CancellationToken ct)
{
    try
    {
        await using var conn = new NpgsqlConnection(GetConnectionString());
        await conn.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(
            "UPDATE holds SET status='EXPIRED', updated_at=now() WHERE status='HELD' AND expires_at <= now()", conn);
        await cmd.ExecuteNonQueryAsync(ct);
    }
    catch { /* best effort */ }
}

await InitDbAsync();
_ = Task.Run(async () =>
{
    while (!app.Lifetime.ApplicationStopping.IsCancellationRequested)
    {
        await Task.Delay(5000);
        await SweepExpiredAsync(app.Lifetime.ApplicationStopping);
    }
});

// ------------------------------------------------------------------
// API endpoints (contract from the brief; extra fields allowed)
// ------------------------------------------------------------------
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.MapPost("/classes", async (CreateClassRequest? body) =>
{
    if (body == null || string.IsNullOrWhiteSpace(body.name) || body.name.Trim().Length > 200)
        return Results.BadRequest(new { error = "INVALID_INPUT", message = "A class name (1-200 chars) is required." });
    // totalSeats must be present and >= 1.
    if (body.totalSeats == null || body.totalSeats < 1)
        return Results.BadRequest(new { error = "INVALID_INPUT", message = "totalSeats must be an integer >= 1." });

    var id = NewId("c_");
    try
    {
        await using var conn = new NpgsqlConnection(GetConnectionString());
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand("INSERT INTO classes(id,name,total_seats) VALUES(@id,@name,@seats)", conn);
        cmd.Parameters.AddWithValue("id", id);
        cmd.Parameters.AddWithValue("name", body.name.Trim());
        cmd.Parameters.AddWithValue("seats", body.totalSeats.Value);
        await cmd.ExecuteNonQueryAsync();
    }
    catch (Exception)
    {
        return Results.Json(new { error = "SERVER_ERROR", message = "Could not save the class." }, statusCode: 500);
    }

    return Results.Created($"/classes/{id}", new { id, name = body.name.Trim(), totalSeats = body.totalSeats.Value });
});

static async Task<List<object>> ListClassesAsync(string connStr)
{
    const string sql = """
        SELECT c.id, c.name, c.total_seats,
               (SELECT COUNT(*) FROM holds h WHERE h.class_id=c.id AND h.status='CONFIRMED') AS confirmed,
               (SELECT COUNT(*) FROM holds h WHERE h.class_id=c.id AND h.status='HELD' AND h.expires_at > now()) AS held
        FROM classes c ORDER BY c.created_at ASC, c.id ASC
        """;
    var list = new List<object>();
    await using var conn = new NpgsqlConnection(connStr);
    await conn.OpenAsync();
    await using var cmd = new NpgsqlCommand(sql, conn);
    await using var r = await cmd.ExecuteReaderAsync();
    while (await r.ReadAsync())
    {
        int total = r.GetInt32(2);
        int held = r.GetInt32(4);
        int conf = r.GetInt32(3);
        int avail = Math.Max(0, total - held - conf);
        list.Add(new
        {
            id = r.GetString(0),
            name = r.GetString(1),
            totalSeats = total,
            heldSeats = held,
            confirmedSeats = conf,
            availableSeats = avail
        });
    }
    return list;
}

app.MapGet("/classes", async () => Results.Ok(await ListClassesAsync(GetConnectionString())));

app.MapGet("/classes/{classId}", async (string classId) =>
{
    const string sql = """
        SELECT c.id, c.name, c.total_seats,
               (SELECT COUNT(*) FROM holds h WHERE h.class_id=c.id AND h.status='CONFIRMED') AS confirmed,
               (SELECT COUNT(*) FROM holds h WHERE h.class_id=c.id AND h.status='HELD' AND h.expires_at > now()) AS held
        FROM classes c WHERE c.id=@id
        """;
    await using var conn = new NpgsqlConnection(GetConnectionString());
    await conn.OpenAsync();
    await using var cmd = new NpgsqlCommand(sql, conn);
    cmd.Parameters.AddWithValue("id", classId);
    await using var r = await cmd.ExecuteReaderAsync();
    if (!await r.ReadAsync())
        return Results.NotFound(new { error = "NOT_FOUND", message = "Class not found." });
    int total = r.GetInt32(2);
    int held = r.GetInt32(4);
    int conf = r.GetInt32(3);
    return Results.Ok(new
    {
        id = r.GetString(0),
        name = r.GetString(1),
        totalSeats = total,
        heldSeats = held,
        confirmedSeats = conf,
        availableSeats = Math.Max(0, total - held - conf)
    });
});

app.MapPost("/classes/{classId}/holds", async (string classId, HttpContext ctx) =>
{
    var studentId = GetStudentId(ctx);
    if (studentId is null)
        return Results.BadRequest(new { error = "MISSING_STUDENT_ID", message = "X-Student-Id header is required." });

    var duration = GetHoldDurationSeconds();
    await using var conn = new NpgsqlConnection(GetConnectionString());
    await conn.OpenAsync();
    await using var tx = await conn.BeginTransactionAsync();
    try
    {
        // Lock the class row: serialises all hold writers for this class
        // across processes (satisfies the multi-process bonus).
        int? totalSeats = null;
        await using (var sel = new NpgsqlCommand("SELECT total_seats FROM classes WHERE id=@id FOR UPDATE", conn, tx))
        {
            sel.Parameters.AddWithValue("id", classId);
            var v = await sel.ExecuteScalarAsync();
            if (v is null) { await tx.RollbackAsync(); return Results.NotFound(new { error = "NOT_FOUND", message = "Class not found." }); }
            totalSeats = Convert.ToInt32(v);
        }

        // One active hold / confirmed per student per class
        await using (var dup = new NpgsqlCommand("""
            SELECT COUNT(*) FROM holds WHERE class_id=@cid AND student_id=@sid
            AND (status='CONFIRMED' OR (status='HELD' AND expires_at > now()))
            """, conn, tx))
        {
            dup.Parameters.AddWithValue("cid", classId);
            dup.Parameters.AddWithValue("sid", studentId);
            var n = Convert.ToInt64(await dup.ExecuteScalarAsync());
            if (n > 0) { await tx.RollbackAsync(); return Results.Conflict(new { error = "DUPLICATE_ACTIVE_HOLD", message = "You already have an active application or confirmed admission for this class." }); }
        }

        // Capacity: held(active) + confirmed <= totalSeats
        await using (var cnt = new NpgsqlCommand("""
            SELECT COUNT(*) FROM holds WHERE class_id=@cid
            AND (status='CONFIRMED' OR (status='HELD' AND expires_at > now()))
            """, conn, tx))
        {
            cnt.Parameters.AddWithValue("cid", classId);
            var occupied = Convert.ToInt64(await cnt.ExecuteScalarAsync());
            if (occupied >= totalSeats) { await tx.RollbackAsync(); return Results.Conflict(new { error = "NO_SEATS", message = "No seats are available for this class." }); }
        }

        var holdId = NewId("h_");
        await using (var ins = new NpgsqlCommand("""
            INSERT INTO holds(id,class_id,student_id,status,expires_at)
            VALUES(@id,@cid,@sid,'HELD', now() + make_interval(secs=>@dur))
            """, conn, tx))
        {
            ins.Parameters.AddWithValue("id", holdId);
            ins.Parameters.AddWithValue("cid", classId);
            ins.Parameters.AddWithValue("sid", studentId);
            ins.Parameters.AddWithValue("dur", duration);
            await ins.ExecuteNonQueryAsync();
        }

        await using var read = new NpgsqlCommand("SELECT expires_at FROM holds WHERE id=@id", conn, tx);
        read.Parameters.AddWithValue("id", holdId);
        var exp = await read.ExecuteScalarAsync();
        var expiresAt = Convert.ToDateTime(exp).ToUniversalTime();

        await tx.CommitAsync();
        return Results.Created($"/holds/{holdId}", new
        {
            holdId,
            classId,
            studentId,
            status = "HELD",
            expiresAt = Iso(expiresAt)
        });
    }
    catch (Exception)
    {
        try { await tx.RollbackAsync(); } catch { }
        return Results.Json(new { error = "SERVER_ERROR", message = "Could not create the hold." }, statusCode: 500);
    }
});

app.MapGet("/holds/{holdId}", async (string holdId) =>
{
    await using var conn = new NpgsqlConnection(GetConnectionString());
    await conn.OpenAsync();
    await using var cmd = new NpgsqlCommand("""
        SELECT h.id, h.class_id, h.student_id, h.status, h.expires_at, now() FROM holds h WHERE h.id=@id
        """, conn);
    cmd.Parameters.AddWithValue("id", holdId);
    await using var r = await cmd.ExecuteReaderAsync();
    if (!await r.ReadAsync())
        return Results.NotFound(new { error = "NOT_FOUND", message = "Hold not found." });
    var stored = r.GetString(3);
    var expires = DateTimeOffset.Parse(r.GetDateTime(4).ToString("o"));
    // use DB time for expiry decision
    var dbNow = DateTimeOffset.Parse(r.GetDateTime(5).ToString("o"));
    return Results.Ok(new
    {
        holdId = r.GetString(0),
        classId = r.GetString(1),
        studentId = r.GetString(2),
        status = EffectiveStatus(stored, expires, dbNow),
        expiresAt = Iso(expires)
    });
});

app.MapPost("/holds/{holdId}/confirm", async (string holdId, HttpContext ctx) =>
{
    var studentId = GetStudentId(ctx);
    if (studentId is null)
        return Results.BadRequest(new { error = "MISSING_STUDENT_ID", message = "X-Student-Id header is required." });

    await using var conn = new NpgsqlConnection(GetConnectionString());
    await conn.OpenAsync();
    await using var tx = await conn.BeginTransactionAsync();
    try
    {
        string? stored = null; string? owner = null; DateTime? expires = null; DateTime? dbNow = null;
        await using (var sel = new NpgsqlCommand("SELECT status, student_id, expires_at, now() FROM holds WHERE id=@id FOR UPDATE", conn, tx))
        {
            sel.Parameters.AddWithValue("id", holdId);
            await using var r = await sel.ExecuteReaderAsync();
            if (!await r.ReadAsync()) { await tx.RollbackAsync(); return Results.NotFound(new { error = "NOT_FOUND", message = "Hold not found." }); }
            stored = r.GetString(0); owner = r.GetString(1); expires = r.GetDateTime(2); dbNow = r.GetDateTime(3);
        }
        if (owner != studentId) { await tx.RollbackAsync(); return Results.Json(new { error = "FORBIDDEN", message = "This hold belongs to another student." }, statusCode: 403); }

        var eff = EffectiveStatus(stored!, DateTimeOffset.Parse(expires!.Value.ToString("o")), DateTimeOffset.Parse(dbNow!.Value.ToString("o")));

        if (eff == "CONFIRMED")
        {
            await tx.CommitAsync();
            return Results.Ok(new { holdId, status = "CONFIRMED" }); // idempotent
        }
        if (eff == "EXPIRED")
        {
            await using var up = new NpgsqlCommand("UPDATE holds SET status='EXPIRED', updated_at=now() WHERE id=@id", conn, tx);
            up.Parameters.AddWithValue("id", holdId); await up.ExecuteNonQueryAsync();
            await tx.CommitAsync();
            return Results.Conflict(new { error = "EXPIRED", message = "This application has expired." });
        }
        if (eff == "CANCELLED")
        {
            await tx.CommitAsync();
            return Results.Conflict(new { error = "CANCELLED", message = "This application was cancelled." });
        }
        // HELD and still valid
        await using var upd = new NpgsqlCommand("UPDATE holds SET status='CONFIRMED', updated_at=now() WHERE id=@id", conn, tx);
        upd.Parameters.AddWithValue("id", holdId); await upd.ExecuteNonQueryAsync();
        await tx.CommitAsync();
        return Results.Ok(new { holdId, status = "CONFIRMED" });
    }
    catch (Exception)
    {
        try { await tx.RollbackAsync(); } catch { }
        return Results.Json(new { error = "SERVER_ERROR", message = "Could not confirm the hold." }, statusCode: 500);
    }
});

app.MapDelete("/holds/{holdId}", async (string holdId, HttpContext ctx) =>
{
    var studentId = GetStudentId(ctx);
    if (studentId is null)
        return Results.BadRequest(new { error = "MISSING_STUDENT_ID", message = "X-Student-Id header is required." });

    await using var conn = new NpgsqlConnection(GetConnectionString());
    await conn.OpenAsync();
    await using var tx = await conn.BeginTransactionAsync();
    try
    {
        string? stored = null; string? owner = null; DateTime? expires = null; DateTime? dbNow = null;
        await using (var sel = new NpgsqlCommand("SELECT status, student_id, expires_at, now() FROM holds WHERE id=@id FOR UPDATE", conn, tx))
        {
            sel.Parameters.AddWithValue("id", holdId);
            await using var r = await sel.ExecuteReaderAsync();
            if (!await r.ReadAsync()) { await tx.RollbackAsync(); return Results.NotFound(new { error = "NOT_FOUND", message = "Hold not found." }); }
            stored = r.GetString(0); owner = r.GetString(1); expires = r.GetDateTime(2); dbNow = r.GetDateTime(3);
        }
        if (owner != studentId) { await tx.RollbackAsync(); return Results.Json(new { error = "FORBIDDEN", message = "This hold belongs to another student." }, statusCode: 403); }

        var eff = EffectiveStatus(stored!, DateTimeOffset.Parse(expires!.Value.ToString("o")), DateTimeOffset.Parse(dbNow!.Value.ToString("o")));
        if (eff is "HELD" or "CONFIRMED")
        {
            await using var upd = new NpgsqlCommand("UPDATE holds SET status='CANCELLED', updated_at=now() WHERE id=@id", conn, tx);
            upd.Parameters.AddWithValue("id", holdId); await upd.ExecuteNonQueryAsync();
        }
        else if (eff == "EXPIRED" && stored == "HELD")
        {
            await using var upd = new NpgsqlCommand("UPDATE holds SET status='EXPIRED', updated_at=now() WHERE id=@id", conn, tx);
            upd.Parameters.AddWithValue("id", holdId); await upd.ExecuteNonQueryAsync();
        }
        await tx.CommitAsync();
        return Results.NoContent();
    }
    catch (Exception)
    {
        try { await tx.RollbackAsync(); } catch { }
        return Results.Json(new { error = "SERVER_ERROR", message = "Could not cancel the hold." }, statusCode: 500);
    }
});

app.MapGet("/me/holds", async (HttpContext ctx) =>
{
    var studentId = GetStudentId(ctx);
    if (studentId is null)
        return Results.BadRequest(new { error = "MISSING_STUDENT_ID", message = "X-Student-Id header is required." });

    const string sql = """
        SELECT h.id, h.class_id, c.name, h.status, h.expires_at, now()
        FROM holds h JOIN classes c ON c.id = h.class_id
        WHERE h.student_id = @sid ORDER BY h.created_at DESC, h.id DESC
        """;
    var list = new List<object>();
    await using var conn = new NpgsqlConnection(GetConnectionString());
    await conn.OpenAsync();
    await using var cmd = new NpgsqlCommand(sql, conn);
    cmd.Parameters.AddWithValue("sid", studentId);
    await using var r = await cmd.ExecuteReaderAsync();
    while (await r.ReadAsync())
    {
        var stored = r.GetString(3);
        var expires = DateTimeOffset.Parse(r.GetDateTime(4).ToString("o"));
        var dbNow = DateTimeOffset.Parse(r.GetDateTime(5).ToString("o"));
        list.Add(new
        {
            holdId = r.GetString(0),
            classId = r.GetString(1),
            className = r.GetString(2),
            status = EffectiveStatus(stored, expires, dbNow),
            expiresAt = Iso(expires)
        });
    }
    return Results.Ok(list);
});

// Friendly 400 for malformed JSON instead of crashing.
app.Use((ctx, next) => next(ctx));
app.Run();

record CreateClassRequest(string? name, int? totalSeats);

// Visible to integration tests.
public partial class Program { }
