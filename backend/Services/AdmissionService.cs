using System.Globalization;
using System.Text;
using Admissions.Data;
using Admissions.Dtos;
using Admissions.Models;
using Admissions.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Admissions.Services;

public sealed class AdmissionService(
    AdmissionDbContext db,
    IAcademicYearRepository years,
    IClassRepository classes,
    IStudentRepository students,
    IHoldRepository holds,
    IPaymentRepository payments,
    ILogger<AdmissionService> logger) : IAdmissionService
{
    static string NewId(string p) => p + Guid.NewGuid().ToString("N")[0..12].ToLowerInvariant();

    static string Status(string stored, DateTime expires, DateTime now)
        => stored == HoldStatus.Held && expires <= now ? HoldStatus.Expired : stored;

    // ── years ────────────────────────────────────────────────────────────────
    public async Task<List<YearResponse>> GetYearsAsync()
    {
        var list = await years.GetAllAsync();
        var counts = await db.Classes.AsNoTracking()
            .GroupBy(c => c.AcademicYearId)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToListAsync();
        var map = counts.ToDictionary(x => x.Key, x => x.Count);
        return list.Select(y => new YearResponse(y.Id, y.Name, y.StartDate, y.EndDate,
            y.IsActive, map.GetValueOrDefault(y.Id))).ToList();
    }

    public async Task<YearResponse> CreateYearAsync(CreateYearRequest req)
    {
        if (req.EndDate <= req.StartDate) throw new ValidationException("EndDate must be after StartDate.");
        if (await db.AcademicYears.AnyAsync(y => y.Name == req.Name.Trim()))
            throw new ValidationException("A year with this name already exists.");
        var y = new AcademicYear
        {
            Id = NewId("y_"), Name = req.Name.Trim(),
            StartDate = req.StartDate, EndDate = req.EndDate
        };
        await years.AddAsync(y);
        return new YearResponse(y.Id, y.Name, y.StartDate, y.EndDate, y.IsActive, 0);
    }

    public async Task ActivateYearAsync(string id)
    {
        var y = await db.AcademicYears.FirstOrDefaultAsync(x => x.Id == id)
                ?? throw new NotFoundException("Academic year not found.");
        await years.ClearActiveFlagsAsync();
        y.IsActive = true;
        await years.UpdateAsync(y);
    }

    // ── classes ──────────────────────────────────────────────────────────────
    public async Task<List<ClassResponse>> GetClassesAsync(bool? activeOnly, string? yearId, string? search)
    {
        var list = await classes.GetAllAsync(activeOnly, yearId, search);
        var counts = await classes.GetSeatCountsAsync();
        return list.Select(c =>
        {
            counts.TryGetValue(c.Id, out var cc);
            return ToClassResponse(c, cc.held, cc.confirmed);
        }).ToList();
    }

    public async Task<ClassResponse?> GetClassAsync(string id)
    {
        var c = await classes.GetByIdAsync(id);
        if (c is null) return null;
        var (h, conf) = await classes.GetSeatCountAsync(c.Id);
        return ToClassResponse(c, h, conf);
    }

    public async Task<ClassResponse> CreateClassAsync(CreateClassRequest req)
    {
        var yearId = req.AcademicYearId;
        if (string.IsNullOrWhiteSpace(yearId))
            yearId = (await years.GetActiveAsync())?.Id
                     ?? throw new ValidationException("No active academic year. Create and activate one first.");
        if (!await db.AcademicYears.AnyAsync(y => y.Id == yearId))
            throw new NotFoundException("Academic year not found.");

        var c = new SchoolClass
        {
            Id = NewId("c_"), AcademicYearId = yearId,
            Name = req.Name.Trim(), Shift = req.Shift?.Trim(), Grade = req.Grade?.Trim(),
            TotalSeats = req.TotalSeats, FeeAmount = req.FeeAmount,
            IsActive = true
        };
        await classes.AddAsync(c);
        return ToClassResponse(c, 0, 0);
    }

    public async Task<ClassResponse?> UpdateClassAsync(string id, UpdateClassRequest req)
    {
        var c = await db.Classes.FirstOrDefaultAsync(x => x.Id == id);
        if (c is null) return null;
        c.Name = req.Name.Trim(); c.Shift = req.Shift?.Trim(); c.Grade = req.Grade?.Trim();
        c.TotalSeats = req.TotalSeats; c.FeeAmount = req.FeeAmount;
        c.IsActive = req.IsActive; c.UpdatedAt = DateTime.UtcNow;
        await classes.UpdateAsync(c);
        var (h, conf) = await classes.GetSeatCountAsync(c.Id);
        return ToClassResponse(c, h, conf);
    }

    static ClassResponse ToClassResponse(SchoolClass c, int held, int confirmed) => new(
        c.Id, c.Name, c.Shift, c.Grade, c.TotalSeats, c.FeeAmount, c.IsActive,
        c.AcademicYear?.Name ?? "", held, confirmed,
        Math.Max(0, c.TotalSeats - held - confirmed));

    // ── student resolution (legacy header support) ──────────────────────────
    async Task<Student> ResolveStudentAsync(string nid)
    {
        var s = await students.GetByNidAsync(nid);
        if (s is not null) return s;
        s = new Student
        {
            Id = NewId("s_"), Nid = nid, FullName = "Parent " + nid[^4..],
            PasswordHash = "", Role = AppRoles.Parent
        };
        await students.AddAsync(s);
        logger.LogInformation("Auto-provisioned student {Nid}", nid);
        return s;
    }

    // ── apply: the critical concurrency path ────────────────────────────────
    public async Task<HoldResponse> ApplyAsync(string classId, string nid)
    {
        var duration = int.TryParse(Environment.GetEnvironmentVariable("HOLD_DURATION_SECONDS"), out var v) && v >= 1
            ? v : 600;

        await using var tx = await db.Database.BeginTransactionAsync();
        try
        {
            // Row lock: serialises all hold writers for this class, across processes.
            var cls = await db.Classes.FromSqlInterpolated(
                $"SELECT * FROM \"Classes\" WHERE \"Id\" = {classId} FOR UPDATE").FirstOrDefaultAsync();
            if (cls is null) { await tx.RollbackAsync(); throw new NotFoundException("Class not found."); }
            if (!cls.IsActive) { await tx.RollbackAsync(); throw new ValidationException("Class is not active."); }

            var student = await ResolveStudentAsync(nid);
            var dup = await holds.CountActiveByStudentAsync(classId, student.Id);
            if (dup > 0) { await tx.RollbackAsync(); throw new ConflictException("DUPLICATE_ACTIVE_HOLD", "You already have an active application or confirmed admission for this class."); }

            var occupied = await holds.CountOccupiedAsync(classId);
            if (occupied >= cls.TotalSeats) { await tx.RollbackAsync(); throw new ConflictException("NO_SEATS", "No seats are available for this class."); }

            var hold = new Hold
            {
                Id = NewId("h_"), ClassId = classId, StudentId = student.Id,
                Status = HoldStatus.Held,
                ExpiresAt = DateTime.UtcNow.AddSeconds(duration)
            };
            db.Holds.Add(hold);
            await db.SaveChangesAsync();
            await tx.CommitAsync();

            return new HoldResponse(hold.Id, classId, nid, hold.Status, hold.ExpiresAt, null);
        }
        catch (Exception)
        {
            try { await tx.RollbackAsync(); } catch { }
            throw;
        }
    }

    public async Task<HoldResponse?> GetHoldAsync(string holdId)
    {
        var h = await holds.GetByIdAsync(holdId);
        if (h is null) return null;
        var s = await students.GetByIdAsync(h.StudentId);
        return new HoldResponse(h.Id, h.ClassId, s?.Nid ?? h.StudentId,
            Status(h.Status, h.ExpiresAt, DateTime.UtcNow), h.ExpiresAt, h.ConfirmedAt);
    }

    // ── confirm / pay ────────────────────────────────────────────────────────
    public async Task<HoldResponse?> ConfirmAsync(string holdId, string nid, PayRequest? pay = null)
    {
        var student = await students.GetByNidAsync(nid);
        var hold = await db.Holds.FirstOrDefaultAsync(h => h.Id == holdId)
                   ?? throw new NotFoundException("Hold not found.");
        if (student is null || hold.StudentId != student.Id)
            throw new ForbiddenException("This application belongs to another student.");

        var eff = Status(hold.Status, hold.ExpiresAt, DateTime.UtcNow);
        if (eff == HoldStatus.Confirmed)
            return new HoldResponse(hold.Id, hold.ClassId, nid, HoldStatus.Confirmed, hold.ExpiresAt, hold.ConfirmedAt);
        if (eff != HoldStatus.Held)
            throw new ConflictException(eff, "This application can no longer be confirmed.");

        // Record payment (mock gateway) exactly once.
        if (!await db.Payments.AnyAsync(p => p.HoldId == hold.Id))
        {
            var cls = await db.Classes.FirstOrDefaultAsync(c => c.Id == hold.ClassId);
            db.Payments.Add(new Payment
            {
                Id = NewId("p_"), HoldId = hold.Id,
                Amount = cls?.FeeAmount ?? 0,
                Method = pay?.Method ?? "MOCK",
                TransactionRef = pay?.TransactionRef,
                Status = PaymentStatus.Completed,
                PaidAt = DateTime.UtcNow
            });
        }

        hold.Status = HoldStatus.Confirmed;
        hold.ConfirmedAt = DateTime.UtcNow;
        hold.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        return new HoldResponse(hold.Id, hold.ClassId, nid, HoldStatus.Confirmed, hold.ExpiresAt, hold.ConfirmedAt);
    }

    public async Task<bool> CancelAsync(string holdId, string nid)
    {
        var student = await students.GetByNidAsync(nid);
        var hold = await db.Holds.FirstOrDefaultAsync(h => h.Id == holdId)
                   ?? throw new NotFoundException("Hold not found.");
        if (student is null || hold.StudentId != student.Id)
            throw new ForbiddenException("This application belongs to another student.");

        var eff = Status(hold.Status, hold.ExpiresAt, DateTime.UtcNow);
        if (eff is HoldStatus.Held or HoldStatus.Confirmed)
        {
            hold.Status = HoldStatus.Cancelled;
            hold.CancelledAt = DateTime.UtcNow;
            hold.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
        }
        return true;
    }

    // ── lists ────────────────────────────────────────────────────────────────
    public async Task<List<MyHoldResponse>> GetMyHoldsAsync(string nid)
    {
        var s = await students.GetByNidAsync(nid);
        if (s is null) return [];
        var list = await holds.GetByStudentAsync(s.Id);
        var now = DateTime.UtcNow;
        var result = new List<MyHoldResponse>();
        foreach (var h in list)
        {
            var pay = await payments.GetByHoldIdAsync(h.Id);
            result.Add(new MyHoldResponse(
                h.Id, h.ClassId, h.Class?.Name ?? "", h.Class?.Grade ?? "", h.Class?.Shift,
                h.Class?.FeeAmount ?? 0, Status(h.Status, h.ExpiresAt, now),
                h.ExpiresAt, h.ConfirmedAt,
                Status(h.Status, h.ExpiresAt, now) == HoldStatus.Held && h.ExpiresAt > now,
                Status(h.Status, h.ExpiresAt, now) is HoldStatus.Held or HoldStatus.Confirmed,
                pay?.TransactionRef));
        }
        return result;
    }

    public async Task<PagedResponse<AdminApplicationResponse>> GetApplicationsAsync(ApplicationFilter filter)
    {
        var list = await holds.GetAllAsync(filter);
        var total = await holds.CountAllAsync(filter);
        var items = new List<AdminApplicationResponse>(list.Count);
        foreach (var h in list)
        {
            var pay = await payments.GetByHoldIdAsync(h.Id);
            items.Add(new AdminApplicationResponse(
                h.Id, h.Class?.Name ?? "", h.Student?.FullName ?? "", h.Student?.Nid ?? "",
                Status(h.Status, h.ExpiresAt, DateTime.UtcNow), h.ExpiresAt, pay?.TransactionRef));
        }
        return new PagedResponse<AdminApplicationResponse>(items, filter.Page, filter.PageSize, total);
    }

    public async Task<DashboardResponse> GetDashboardAsync()
    {
        var activeYear = await years.GetActiveAsync();
        var activeClasses = await db.Classes.AsNoTracking().CountAsync(c => c.IsActive);
        var totalSeats = await db.Classes.AsNoTracking().Where(c => c.IsActive).SumAsync(c => c.TotalSeats);
        var counts = await db.Holds.AsNoTracking()
            .GroupBy(h => 1).Select(g => new
            {
                H = g.Count(x => x.Status == HoldStatus.Held && x.ExpiresAt > DateTime.UtcNow),
                C = g.Count(x => x.Status == HoldStatus.Confirmed)
            }).FirstOrDefaultAsync();
        var held = counts?.H ?? 0;
        var confirmed = counts?.C ?? 0;
        var expired = await db.Holds.AsNoTracking().CountAsync(h => h.Status == HoldStatus.Expired);
        var studentCount = await students.CountAsync();
        var revenue = await payments.SumCompletedAsync();

        return new DashboardResponse(
            activeYear?.Name ?? "None", activeClasses, totalSeats, held + confirmed,
            held, confirmed, expired, studentCount, revenue);
    }

    // ── CSV export ───────────────────────────────────────────────────────────
    static string Csv(string value)
    {
        var v = value ?? "";
        return v.Contains(',') || v.Contains('"') || v.Contains('\n')
            ? "\"" + v.Replace("\"", "\"\"") + "\"" : v;
    }

    static byte[] ToCsv(IEnumerable<string[]> rows)
    {
        var sb = new StringBuilder();
        foreach (var row in rows)
            sb.AppendLine(string.Join(",", row.Select(Csv)));
        // BOM so Excel opens UTF-8 correctly
        var bytes = Encoding.UTF8.GetBytes(sb.ToString());
        var withBom = new byte[bytes.Length + 3];
        withBom[0] = 0xEF; withBom[1] = 0xBB; withBom[2] = 0xBF;
        Array.Copy(bytes, 0, withBom, 3, bytes.Length);
        return withBom;
    }

    public async Task<byte[]> ExportApplicationsCsvAsync(string? status, string? classId)
    {
        var filter = new ApplicationFilter(status, classId, 1, int.MaxValue);
        var all = await holds.GetAllAsync(filter);
        var now = DateTime.UtcNow;
        var rows = new List<string[]>
        {
            new[] { "HoldId", "Class", "Student", "NID", "Status", "ExpiresAt", "TransactionRef", "Guardian", "GuardianPhone" }
        };
        foreach (var h in all)
        {
            var payment = await payments.GetByHoldIdAsync(h.Id);
            rows.Add(new[] {
                h.Id, h.Class?.Name ?? "", h.Student?.FullName ?? "", h.Student?.Nid ?? "",
                Status(h.Status, h.ExpiresAt, now),
                h.ExpiresAt.ToString("yyyy-MM-dd HH:mm"),
                payment?.TransactionRef ?? "", h.GuardianName ?? "", h.GuardianPhone ?? ""
            });
        }
        return ToCsv(rows);
    }

    public async Task<byte[]> ExportStudentsCsvAsync()
    {
        var all = await students.GetAllAsync(1, int.MaxValue, null);
        var rows = new List<string[]> { new[] { "Id", "NID", "FullName", "Phone", "Email", "Role", "CreatedAt" } };
        foreach (var s in all)
            rows.Add(new[] { s.Id, s.Nid, s.FullName, s.Phone ?? "", s.Email ?? "", s.Role, s.CreatedAt.ToString("yyyy-MM-dd") });
        return ToCsv(rows);
    }

    public async Task<AnalyticsResponse> GetAnalyticsAsync()
    {
        var classRows = await db.Classes.AsNoTracking()
            .Select(c => new { c.Id, c.Name, c.TotalSeats })
            .ToListAsync();
        var counts = await classes.GetSeatCountsAsync();
        var seats = classRows.Select(c =>
        {
            counts.TryGetValue(c.Id, out var cc);
            return new ClassSeatRow(c.Name, c.TotalSeats, cc.held, cc.confirmed,
                Math.Max(0, c.TotalSeats - cc.held - cc.confirmed));
        }).OrderByDescending(x => x.Total).ToList();

        var byStatus = await db.Holds.AsNoTracking()
            .GroupBy(h => h.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Status, x => x.Count);

        var byMethod = await db.Payments.AsNoTracking()
            .Where(p => p.Status == PaymentStatus.Completed)
            .GroupBy(p => p.Method)
            .Select(g => new { Method = g.Key, Total = g.Sum(p => p.Amount) })
            .ToDictionaryAsync(x => x.Method, x => x.Total);

        return new AnalyticsResponse(seats, byStatus, byMethod);
    }
}
