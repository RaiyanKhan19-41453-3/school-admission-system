using System.Text;
using Admissions.Controllers;
using Admissions.Data;
using Admissions.Middleware;
using Admissions.Models;
using Admissions.Repositories;
using Admissions.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls("http://*:8080");

// ── Database ─────────────────────────────────────────────────────────────────
var conn = builder.Configuration.GetConnectionString("DefaultConnection")
           ?? Environment.GetEnvironmentVariable("DATABASE_URL")
           ?? throw new InvalidOperationException("Database connection string is not configured.");
builder.Services.AddDbContext<AdmissionDbContext>(o => o.UseNpgsql(conn));

// ── CORS ─────────────────────────────────────────────────────────────────────
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()));

// ── Authentication (JWT) ─────────────────────────────────────────────────────
var jwt = builder.Configuration.GetSection("Jwt");
var key = Encoding.UTF8.GetBytes(jwt["Key"]!);
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o => o.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true, ValidateAudience = true, ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwt["Issuer"], ValidAudience = jwt["Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(key)
    });
builder.Services.AddAuthorization();
builder.Services.AddScoped<IAcademicYearRepository, AcademicYearRepository>();
builder.Services.AddScoped<IClassRepository, ClassRepository>();
builder.Services.AddScoped<IStudentRepository, StudentRepository>();
builder.Services.AddScoped<IHoldRepository, HoldRepository>();
builder.Services.AddScoped<IPaymentRepository, PaymentRepository>();
builder.Services.AddScoped<IAdmissionService, AdmissionService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IApplicationFormService, ApplicationFormService>();
builder.Services.AddScoped<IReceiptService, ReceiptService>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddHostedService<ExpiredHoldSweeper>();

// ── MVC / OpenAPI / health ───────────────────────────────────────────────────
builder.Services.AddControllers();
builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer((document, _, _) =>
    {
        document.Info.Title = "School Admission System API";
        document.Info.Version = "v1";
        document.Info.Description = "First-come-first-served admissions with seat holds, fees, and expiry.";
        return Task.CompletedTask;
    });
});
// ── DI: repositories & services ──────────────────────────────────────────────

var app = builder.Build();

// ── Migrate + seed on startup ────────────────────────────────────────────────
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var db = services.GetRequiredService<AdmissionDbContext>();
    Exception? last = null;
    for (int i = 0; i < 40; i++)
    {
        try { await db.Database.MigrateAsync(); last = null; break; }
        catch (Exception ex) { last = ex; await Task.Delay(2000); }
    }
    if (last is not null) throw new InvalidOperationException("Database migration failed", last);

    var auth = services.GetRequiredService<IAuthService>();
    await auth.SeedAdminAsync("ADMIN00000000000", "School Admin", "Admin@123");

    if (!await db.AcademicYears.AnyAsync())
    {
        var year = new AcademicYear
        {
            Id = "y_" + Guid.NewGuid().ToString("N")[..12],
            Name = DateTime.UtcNow.Year + "-" + (DateTime.UtcNow.Year + 1),
            StartDate = new DateTime(DateTime.UtcNow.Year, 6, 1, 0, 0, 0, DateTimeKind.Utc),
            EndDate = new DateTime(DateTime.UtcNow.Year + 1, 5, 31, 0, 0, 0, DateTimeKind.Utc),
            IsActive = true
        };
        db.AcademicYears.Add(year);
        await db.SaveChangesAsync();
    }
}

// ── Pipeline ─────────────────────────────────────────────────────────────────
app.UseMiddleware<ExceptionHandlingMiddleware>();
app.MapOpenApi();
app.UseCors();
app.UseStaticFiles();   // uploads served under /uploads if ever needed directly
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapGet("/health", async (AdmissionDbContext ctx) =>
    Results.Ok(new { status = "healthy", database = await ctx.Database.CanConnectAsync() ? "up" : "down" }));

app.Run();

public partial class Program { }
