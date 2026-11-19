using System.Net;
using System.Net.Http.Json;
using Admissions.Data;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;

namespace Admissions.Tests;

/// <summary>
/// Integration tests run against a real PostgreSQL (Testcontainers).
/// The container is shared per test collection; each test uses a
/// unique class/hold prefix. DATABASE_URL env, if set, is honoured
/// (useful in CI where postgres is provided externally).
/// </summary>
public class AdmissionsTests : IClassFixture<AdmissionsFixture>, IDisposable
{
    private readonly HttpClient _http;
    private readonly WebApplicationFactory<Program> _factory;

    public AdmissionsTests(AdmissionsFixture fixture)
    {
        // App reads DATABASE_URL on every connection, so setting it
        // before the factory starts is enough.
        Environment.SetEnvironmentVariable("DATABASE_URL", fixture.ConnectionString);
        Environment.SetEnvironmentVariable("HOLD_DURATION_SECONDS", "600");
        var factory = new WebApplicationFactory<Program>();
        _factory = factory;
        _http = factory.CreateClient();

        // Ensure schema exists in the test DB (app startup normally does this,
        // but be explicit so external DBs work too).
        using var scope = factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<AdmissionDbContext>().Database.Migrate();
    }

    public void Dispose() => _http.Dispose();

    // --- helpers -------------------------------------------------
    static string ParseUrl(string url)
    {
        var uri = new Uri(url);
        var userInfo = uri.UserInfo.Split(':');
        return new NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            Port = uri.IsDefaultPort ? 5432 : uri.Port,
            Database = uri.AbsolutePath.TrimStart('/'),
            Username = Uri.UnescapeDataString(userInfo[0]),
            Password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : "",
            Pooling = false
        }.ConnectionString;
    }
    static string Student() => "2015" + Random.Shared.Next(100000000, 999999999).ToString();

    async Task<string> CreateClassAsync(int seats)
    {
        var res = await _http.PostAsJsonAsync("/classes", new { name = $"Class {Guid.NewGuid():N}"[..12], totalSeats = seats });
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        var body = await res.Content.ReadFromJsonAsync<Json>();
        return body!.Id;
    }

    async Task<HttpResponseMessage> HoldAsync(string classId, string student)
    {
        var req = new HttpRequestMessage(HttpMethod.Post, $"/classes/{classId}/holds");
        req.Headers.Add("X-Student-Id", student);
        return await _http.SendAsync(req);
    }

    async Task<HttpResponseMessage> ConfirmAsync(string holdId, string student)
    {
        var req = new HttpRequestMessage(HttpMethod.Post, $"/holds/{holdId}/confirm");
        req.Headers.Add("X-Student-Id", student);
        return await _http.SendAsync(req);
    }

    // --- tests ---------------------------------------------------
    [Fact]
    public async Task CreateClass_Validation_Returns400()
    {
        var res = await _http.PostAsJsonAsync("/classes", new { name = "", totalSeats = 0 });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task Hold_Confirm_IsIdempotent_And_CountsMatch()
    {
        var classId = await CreateClassAsync(5);
        var student = Student();
        var holdRes = await HoldAsync(classId, student);
        Assert.Equal(HttpStatusCode.Created, holdRes.StatusCode);
        var hold = await holdRes.Content.ReadFromJsonAsync<Hold>();
        Assert.NotNull(hold);

        Assert.Equal(HttpStatusCode.OK, (await ConfirmAsync(hold!.HoldId, student)).StatusCode);
        // second confirm must not double-count
        Assert.Equal(HttpStatusCode.OK, (await ConfirmAsync(hold.HoldId, student)).StatusCode);

        var list = await _http.GetFromJsonAsync<List<ClassDto>>("/classes");
        var c = Assert.Single(list!.Where(x => x.Id == classId));
        Assert.Equal(1, c.ConfirmedSeats);
        Assert.Equal(0, c.HeldSeats);
        Assert.Equal(4, c.AvailableSeats);
    }

    [Fact]
    public async Task OnlyOwner_CanConfirm_AndDuplicateBlocked()
    {
        var classId = await CreateClassAsync(5);
        var owner = Student();
        var intruder = Student();
        var hold = await (await HoldAsync(classId, owner)).Content.ReadFromJsonAsync<Hold>();
        Assert.NotNull(hold);

        Assert.Equal(HttpStatusCode.Forbidden, (await ConfirmAsync(hold!.HoldId, intruder)).StatusCode);
        // same student, same class, second hold while first still active
        Assert.Equal(HttpStatusCode.Conflict, (await HoldAsync(classId, owner)).StatusCode);
    }

    [Fact]
    public async Task ExpiredHold_FreesSeat_AndCannotConfirm()
    {
        var classId = await CreateClassAsync(1);
        var student = Student();
        var hold = await (await HoldAsync(classId, student)).Content.ReadFromJsonAsync<Hold>();
        Assert.NotNull(hold);

        // Force expiry directly in DB (equivalent to waiting out the window)
        var rawCs = AdmissionsFixture.Shared.ConnectionString;
        string cs;
        try { cs = new NpgsqlConnectionStringBuilder(rawCs).ConnectionString; }
        catch { cs = ParseUrl(rawCs); }
        // Force expiry via EF Core (table name handled by EF, no raw SQL casing issues)
        using var scope = _factory.Services.CreateScope();
        var testDb = scope.ServiceProvider.GetRequiredService<AdmissionDbContext>();
        var entity = await testDb.Holds.FindAsync(hold!.HoldId);
        if (entity is not null)
        {
            entity.ExpiresAt = DateTime.UtcNow.AddSeconds(-1);
            await testDb.SaveChangesAsync();
        }

        var me = new HttpRequestMessage(HttpMethod.Get, "/me/holds");
        me.Headers.Add("X-Student-Id", student);
        var my = await (await _http.SendAsync(me)).Content.ReadFromJsonAsync<List<MeHold>>();
        Assert.Equal("EXPIRED", my![0].Status);

        Assert.Equal(HttpStatusCode.Conflict, (await ConfirmAsync(hold.HoldId, student)).StatusCode);

        var list = await _http.GetFromJsonAsync<List<ClassDto>>("/classes");
        var c = Assert.Single(list!.Where(x => x.Id == classId));
        Assert.Equal(1, c.AvailableSeats);
    }

    [Fact]
    public async Task Concurrency_ManyRequests_NeverExceedSeats()
    {
        var classId = await CreateClassAsync(5);
        var students = Enumerable.Range(0, 30).Select(_ => Student()).Distinct().ToList();
        // ensure 30 distinct ids
        while (students.Count < 30) students.Add(Student());

        var tasks = students.Select(s => HoldAsync(classId, s)).ToList();
        var results = await Task.WhenAll(tasks);
        var created = results.Count(r => r.StatusCode == HttpStatusCode.Created);
        var conflicts = results.Count(r => r.StatusCode == HttpStatusCode.Conflict);
        Assert.Equal(5, created);
        Assert.Equal(25, conflicts);

        var list = await _http.GetFromJsonAsync<List<ClassDto>>("/classes");
        var c = Assert.Single(list!.Where(x => x.Id == classId));
        Assert.Equal(5, c.HeldSeats);
        Assert.Equal(0, c.AvailableSeats);
        Assert.True(c.HeldSeats + c.ConfirmedSeats <= c.TotalSeats);
        foreach (var r in results) r.Dispose();
    }

    // --- DTOs ----------------------------------------------------
    record Json(string Id);
    record Hold(string HoldId, string ClassId, string StudentId, string Status, string ExpiresAt);
    record ClassDto(string Id, string Name, int TotalSeats, int HeldSeats, int ConfirmedSeats, int AvailableSeats);
    record MeHold(string HoldId, string ClassId, string ClassName, string Status, string ExpiresAt);
}

public class AdmissionsFixture
{
    public static AdmissionsFixture Shared { get; } = new();
    public string ConnectionString { get; }

    public AdmissionsFixture()
    {
        var env = Environment.GetEnvironmentVariable("DATABASE_URL");
        if (!string.IsNullOrWhiteSpace(env))
        {
            ConnectionString = env;
            return;
        }
        // Fallback: spin up postgres via Testcontainers
        var container = new PostgreSqlBuilder()
            .WithImage("postgres:16-alpine")
            .WithUsername("postgres")
            .WithPassword("postgres")
            .WithDatabase("admissions")
            .Build();
        container.StartAsync().GetAwaiter().GetResult();
        ConnectionString = container.GetConnectionString();
    }
}
