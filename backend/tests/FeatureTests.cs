using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Admissions.Data;
using Xunit;

namespace Admissions.Tests;

/// <summary>
/// Integration tests for features added beyond the original contract:
/// application form, documents, receipt PDF, notifications, exports, analytics.
/// Each test uses its OWN HttpClient so auth headers never leak between tests.
/// </summary>
[Collection("Integration")] // serialise with other DB-using tests
public class FeatureTests : IClassFixture<AdmissionsFixture>
{
    private readonly WebApplicationFactory<Program> _factory;

    public FeatureTests(AdmissionsFixture fixture)
    {
        Environment.SetEnvironmentVariable("DATABASE_URL", fixture.ConnectionString);
        Environment.SetEnvironmentVariable("HOLD_DURATION_SECONDS", "600");
        _factory = new WebApplicationFactory<Program>();
        using var scope = _factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<AdmissionDbContext>().Database.Migrate();
    }

    HttpClient Client(string? token = null)
    {
        var c = _factory.CreateClient();
        if (token is not null)
            c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return c;
    }

    // exactly 17 digits, prefixed with 9 so it never collides with legacy test IDs
    static string Sid() => "9" + Random.Shared.Next(10000000, 99999999).ToString()
                              + Random.Shared.Next(10000000, 99999999).ToString();

    async Task<string> RegisterParentAsync()
    {
        using var c = Client();
        var nid = Sid();
        var r = await c.PostAsJsonAsync("/api/auth/register",
            new { nid, fullName = "Test " + nid[^4..], password = "pass123" });
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        return (await r.Content.ReadFromJsonAsync<LoginDto>())!.Token;
    }

    async Task<string> AdminTokenAsync()
    {
        using var c = Client();
        var r = await c.PostAsJsonAsync("/api/auth/login",
            new { nid = "ADMIN00000000000", password = "Admin@123" });
        return (await r.Content.ReadFromJsonAsync<LoginDto>())!.Token;
    }

    async Task<(string classId, string holdId, string token)> CreateHoldAsync()
    {
        var token = await RegisterParentAsync();
        var admin = await AdminTokenAsync();
        string cid;
        using (var ac = Client(admin))
        {
            var cr = await ac.PostAsJsonAsync("/api/admin/classes", new
            {
                name = "Feature " + Guid.NewGuid().ToString("N")[..8],
                grade = "Class 9", shift = "Morning", totalSeats = 3, feeAmount = 250m
            });
            Assert.Equal(HttpStatusCode.Created, cr.StatusCode);
            cid = (await cr.Content.ReadFromJsonAsync<ClassDto>())!.Id;
        }
        using var pc = Client(token);
        var hr = await pc.PostAsync($"/api/applications/classes/{cid}/apply", null);
        Assert.Equal(HttpStatusCode.Created, hr.StatusCode);
        var hid = (await hr.Content.ReadFromJsonAsync<HoldDto>())!.HoldId;
        return (cid, hid, token);
    }

    [Fact]
    public async Task SubmitAndGetDetails_Works()
    {
        var (_, hid, token) = await CreateHoldAsync();
        using var c = Client(token);
        var payload = new
        {
            dateOfBirth = new DateTime(2015, 5, 10, 0, 0, 0, DateTimeKind.Utc),
            guardianName = "Guardian", guardianPhone = "01700000000",
            address = "Dhaka", previousSchool = "Test KG"
        };
        var put = await c.PutAsJsonAsync($"/api/applications/{hid}/details", payload);
        Assert.Equal(HttpStatusCode.OK, put.StatusCode);
        var get = await c.GetFromJsonAsync<DetailDto>($"/api/applications/{hid}/details");
        Assert.NotNull(get);
        Assert.True(get.DetailsSubmitted);
        Assert.Equal("Guardian", get.GuardianName);
        Assert.Equal("Dhaka", get.Address);
    }

    [Fact]
    public async Task UploadListDownloadDeleteDocument_Works()
    {
        var (_, hid, token) = await CreateHoldAsync();
        using var c = Client(token);
        using var form = new MultipartFormDataContent();
        form.Add(new StringContent("BirthCertificate"), "documentType");
        var bytes = System.Text.Encoding.UTF8.GetBytes("fake certificate content");
        form.Add(new ByteArrayContent(bytes), "file", "cert.txt");
        var up = await c.PostAsync($"/api/applications/{hid}/documents", form);
        Assert.Equal(HttpStatusCode.Created, up.StatusCode);
        var doc = await up.Content.ReadFromJsonAsync<DocDto>();
        Assert.NotNull(doc);

        var list = await c.GetFromJsonAsync<List<DocDto>>($"/api/applications/{hid}/documents");
        Assert.Contains(list!, d => d.Id == doc.Id);

        var dl = await c.GetAsync($"/api/applications/documents/{doc.Id}/download");
        Assert.Equal(HttpStatusCode.OK, dl.StatusCode);
        Assert.Equal("fake certificate content", await dl.Content.ReadAsStringAsync());

        var del = await c.DeleteAsync($"/api/applications/documents/{doc.Id}");
        Assert.Equal(HttpStatusCode.NoContent, del.StatusCode);
    }

    [Fact]
    public async Task OtherStudentCannotAccessDocument()
    {
        var (_, hid, _) = await CreateHoldAsync();
        var other = await RegisterParentAsync();
        using var c = Client(other);
        var r = await c.GetAsync($"/api/applications/{hid}/details");
        Assert.Equal(HttpStatusCode.Forbidden, r.StatusCode);
    }

    [Fact]
    public async Task PayThenDownloadReceipt_ReturnsPdf()
    {
        var (_, hid, token) = await CreateHoldAsync();
        using var c = Client(token);
        var pay = await c.PostAsJsonAsync($"/api/applications/{hid}/pay", new
        { method = "BKASH", transactionRef = "TEST-123" });
        Assert.Equal(HttpStatusCode.OK, pay.StatusCode);

        var pdf = await c.GetAsync($"/api/applications/{hid}/receipt?token={Uri.EscapeDataString(token)}");
        Assert.Equal(HttpStatusCode.OK, pdf.StatusCode);
        Assert.Equal("application/pdf", pdf.Content.Headers.ContentType?.MediaType);
        var data = await pdf.Content.ReadAsByteArrayAsync();
        Assert.True(data.Length > 300);
        Assert.Equal(0x25, data[0]); // '%'
        Assert.Equal((byte)'P', data[1]);
    }

    [Fact]
    public async Task ReceiptBeforePayment_Returns400()
    {
        var (_, hid, token) = await CreateHoldAsync();
        using var c = Client(token);
        var r = await c.GetAsync($"/api/applications/{hid}/receipt");
        var body = await r.Content.ReadAsStringAsync();
        Assert.True(r.StatusCode == HttpStatusCode.BadRequest, $"got {(int)r.StatusCode}: {body}");
    }

    [Fact]
    public async Task NotificationsCreatedOnApplyAndPay()
    {
        var (_, hid, token) = await CreateHoldAsync();
        using var c = Client(token);
        var list = await c.GetFromJsonAsync<NotificationList>("/api/notifications");
        Assert.NotNull(list);
        Assert.Contains(list.Items, n => n.Type == "HOLD_CREATED");

        await c.PostAsJsonAsync($"/api/applications/{hid}/pay", new { method = "MOCK", transactionRef = "N-1" });
        var after = await c.GetFromJsonAsync<NotificationList>("/api/notifications");
        Assert.Contains(after!.Items, n => n.Type == "PAYMENT_CONFIRMED");

        await c.PutAsync("/api/notifications/read-all", null);
        var read = await c.GetFromJsonAsync<NotificationList>("/api/notifications");
        Assert.Equal(0, read!.UnreadCount);
    }

    [Fact]
    public async Task AdminCsvExportsAndAnalytics_Work()
    {
        var admin = await AdminTokenAsync();
        using var c = Client(admin);
        var csv = await c.GetStringAsync($"/api/admin/applications/export?token={Uri.EscapeDataString(admin)}");
        Assert.Contains("HoldId,Class,Student", csv);
        var students = await c.GetStringAsync($"/api/admin/students/export?token={Uri.EscapeDataString(admin)}");
        Assert.Contains("NID,FullName", students);
        var analytics = await c.GetFromJsonAsync<AnalyticsDto>("/api/admin/analytics");
        Assert.NotNull(analytics);
        Assert.NotEmpty(analytics.SeatsPerClass);
    }

    [Fact]
    public async Task ExportWithoutToken_Returns401()
    {
        using var c = Client(); // no Authorization header, no ?token=
        var r = await c.GetAsync("/api/admin/students/export");
        Assert.Equal(HttpStatusCode.Unauthorized, r.StatusCode);
    }

    record LoginDto(string Token);
    record ClassDto(string Id);
    record HoldDto(string HoldId);
    record DocDto(string Id, string DocumentType, string FileName, long FileSize);
    record DetailDto(string HoldId, string GuardianName, string Address, bool DetailsSubmitted, List<DocDto> Documents);
    record NotificationItem(string Id, string Type, string Title, string Message, bool IsRead);
    record NotificationList(List<NotificationItem> Items, int UnreadCount);
    record SeatRow(string ClassName, int Total, int Held, int Confirmed, int Available);
    record AnalyticsDto(List<SeatRow> SeatsPerClass, Dictionary<string, int> ApplicationsByStatus, Dictionary<string, decimal> RevenueByMethod);
}
