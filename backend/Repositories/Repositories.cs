using Admissions.Data;
using Admissions.Dtos;
using Admissions.Models;
using Microsoft.EntityFrameworkCore;

namespace Admissions.Repositories;

public sealed class AcademicYearRepository(AdmissionDbContext db) : IAcademicYearRepository
{
    public Task<List<AcademicYear>> GetAllAsync()
        => db.AcademicYears.AsNoTracking().OrderByDescending(y => y.StartDate).ToListAsync();

    public Task<AcademicYear?> GetByIdAsync(string id)
        => db.AcademicYears.AsNoTracking().FirstOrDefaultAsync(y => y.Id == id);

    public Task<AcademicYear?> GetActiveAsync()
        => db.AcademicYears.AsNoTracking().FirstOrDefaultAsync(y => y.IsActive);

    public async Task AddAsync(AcademicYear year)
    {
        db.AcademicYears.Add(year);
        await db.SaveChangesAsync();
    }

    public async Task UpdateAsync(AcademicYear year)
    {
        db.AcademicYears.Update(year);
        await db.SaveChangesAsync();
    }

    public async Task ClearActiveFlagsAsync()
    {
        await db.AcademicYears.ExecuteUpdateAsync(s => s.SetProperty(y => y.IsActive, false));
    }
}

public sealed class ClassRepository(AdmissionDbContext db) : IClassRepository
{
    public async Task<List<SchoolClass>> GetAllAsync(bool? activeOnly, string? yearId, string? search)
    {
        var q = db.Classes.AsNoTracking().Include(c => c.AcademicYear).AsQueryable();
        if (activeOnly.HasValue) q = q.Where(c => c.IsActive == activeOnly.Value);
        if (!string.IsNullOrWhiteSpace(yearId)) q = q.Where(c => c.AcademicYearId == yearId);
        if (!string.IsNullOrWhiteSpace(search))
            q = q.Where(c => c.Name.Contains(search) || (c.Grade != null && c.Grade.Contains(search)));
        return await q.OrderBy(c => c.Grade).ThenBy(c => c.Name).ToListAsync();
    }

    public Task<SchoolClass?> GetByIdAsync(string id)
        => db.Classes.AsNoTracking().Include(c => c.AcademicYear).FirstOrDefaultAsync(c => c.Id == id);

    public async Task AddAsync(SchoolClass cls)
    {
        db.Classes.Add(cls);
        await db.SaveChangesAsync();
    }

    public async Task UpdateAsync(SchoolClass cls)
    {
        db.Classes.Update(cls);
        await db.SaveChangesAsync();
    }

    public async Task<Dictionary<string, (int held, int confirmed)>> GetSeatCountsAsync()
    {
        var now = DateTime.UtcNow;
        var rows = await db.Holds.AsNoTracking()
            .Where(h => h.Status == HoldStatus.Held && h.ExpiresAt > now || h.Status == HoldStatus.Confirmed)
            .GroupBy(h => h.ClassId)
            .Select(g => new
            {
                ClassId = g.Key,
                Held = g.Count(h => h.Status == HoldStatus.Held && h.ExpiresAt > now),
                Confirmed = g.Count(h => h.Status == HoldStatus.Confirmed)
            }).ToListAsync();
        return rows.ToDictionary(r => r.ClassId, r => (r.Held, r.Confirmed));
    }

    public async Task<(int held, int confirmed)> GetSeatCountAsync(string classId)
    {
        var now = DateTime.UtcNow;
        var held = await db.Holds.AsNoTracking().CountAsync(h =>
            h.ClassId == classId && h.Status == HoldStatus.Held && h.ExpiresAt > now);
        var confirmed = await db.Holds.AsNoTracking().CountAsync(h =>
            h.ClassId == classId && h.Status == HoldStatus.Confirmed);
        return (held, confirmed);
    }
}

public sealed class StudentRepository(AdmissionDbContext db) : IStudentRepository
{
    public Task<Student?> GetByNidAsync(string nid)
        => db.Students.AsNoTracking().FirstOrDefaultAsync(s => s.Nid == nid);

    public Task<Student?> GetByIdAsync(string id)
        => db.Students.AsNoTracking().FirstOrDefaultAsync(s => s.Id == id);

    public async Task<List<Student>> GetAllAsync(int page, int pageSize, string? search)
    {
        var q = db.Students.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
            q = q.Where(s => s.FullName.Contains(search) || s.Nid.Contains(search));
        return await q.OrderByDescending(s => s.CreatedAt)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
    }

    public Task<int> CountAsync() => db.Students.CountAsync();

    public async Task AddAsync(Student student)
    {
        db.Students.Add(student);
        await db.SaveChangesAsync();
    }

    public async Task UpdateAsync(Student student)
    {
        db.Students.Update(student);
        await db.SaveChangesAsync();
    }
}

public sealed class HoldRepository(AdmissionDbContext db) : IHoldRepository
{
    public Task<Hold?> GetByIdAsync(string id)
        => db.Holds.AsNoTracking().FirstOrDefaultAsync(h => h.Id == id);

    public Task<Hold?> GetByIdWithDetailsAsync(string id)
        => db.Holds.AsNoTracking().Include(h => h.Class).Include(h => h.Student)
           .Include(h => h.Payment).FirstOrDefaultAsync(h => h.Id == id);

    public Task<int> CountActiveByStudentAsync(string classId, string studentId)
    {
        var now = DateTime.UtcNow;
        return db.Holds.CountAsync(h => h.ClassId == classId && h.StudentId == studentId &&
            (h.Status == HoldStatus.Confirmed || (h.Status == HoldStatus.Held && h.ExpiresAt > now)));
    }

    public Task<int> CountOccupiedAsync(string classId)
    {
        var now = DateTime.UtcNow;
        return db.Holds.CountAsync(h => h.ClassId == classId &&
            (h.Status == HoldStatus.Confirmed || (h.Status == HoldStatus.Held && h.ExpiresAt > now)));
    }

    public async Task<List<Hold>> GetByStudentAsync(string studentId)
        => await db.Holds.AsNoTracking().Include(h => h.Class)
           .Where(h => h.StudentId == studentId)
           .OrderByDescending(h => h.CreatedAt).ToListAsync();

    public async Task<List<Hold>> GetAllAsync(ApplicationFilter filter)
    {
        var q = db.Holds.AsNoTracking().Include(h => h.Class).Include(h => h.Student).AsQueryable();
        if (!string.IsNullOrWhiteSpace(filter.Status)) q = q.Where(h => h.Status == filter.Status);
        if (!string.IsNullOrWhiteSpace(filter.ClassId)) q = q.Where(h => h.ClassId == filter.ClassId);
        return await q.OrderByDescending(h => h.CreatedAt)
            .Skip((filter.Page - 1) * filter.PageSize).Take(filter.PageSize).ToListAsync();
    }

    public async Task<int> CountAllAsync(ApplicationFilter filter)
    {
        var q = db.Holds.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(filter.Status)) q = q.Where(h => h.Status == filter.Status);
        if (!string.IsNullOrWhiteSpace(filter.ClassId)) q = q.Where(h => h.ClassId == filter.ClassId);
        return await q.CountAsync();
    }

    public async Task AddAsync(Hold hold)
    {
        db.Holds.Add(hold);
        await db.SaveChangesAsync();
    }

    public async Task UpdateAsync(Hold hold)
    {
        db.Holds.Update(hold);
        await db.SaveChangesAsync();
    }
}

public sealed class PaymentRepository(AdmissionDbContext db) : IPaymentRepository
{
    public Task<Payment?> GetByHoldIdAsync(string holdId)
        => db.Payments.AsNoTracking().FirstOrDefaultAsync(p => p.HoldId == holdId);

    public Task<Payment?> GetByIdAsync(string id)
        => db.Payments.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id);

    public async Task AddAsync(Payment payment)
    {
        db.Payments.Add(payment);
        await db.SaveChangesAsync();
    }

    public async Task<decimal> SumCompletedAsync()
        => await db.Payments.AsNoTracking()
           .Where(p => p.Status == PaymentStatus.Completed).SumAsync(p => p.Amount);
}
