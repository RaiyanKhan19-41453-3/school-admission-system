using Admissions.Dtos;
using Admissions.Models;

namespace Admissions.Repositories;

public interface IAcademicYearRepository
{
    Task<List<AcademicYear>> GetAllAsync();
    Task<AcademicYear?> GetByIdAsync(string id);
    Task<AcademicYear?> GetActiveAsync();
    Task AddAsync(AcademicYear year);
    Task UpdateAsync(AcademicYear year);
    Task ClearActiveFlagsAsync();
}

public interface IClassRepository
{
    Task<List<SchoolClass>> GetAllAsync(bool? activeOnly, string? yearId, string? search);
    Task<SchoolClass?> GetByIdAsync(string id);
    Task AddAsync(SchoolClass cls);
    Task UpdateAsync(SchoolClass cls);
    Task<Dictionary<string, (int held, int confirmed)>> GetSeatCountsAsync();
    Task<(int held, int confirmed)> GetSeatCountAsync(string classId);
}

public interface IStudentRepository
{
    Task<Student?> GetByNidAsync(string nid);
    Task<Student?> GetByIdAsync(string id);
    Task<List<Student>> GetAllAsync(int page, int pageSize, string? search);
    Task<int> CountAsync();
    Task AddAsync(Student student);
    Task UpdateAsync(Student student);
}

public interface IHoldRepository
{
    Task<Hold?> GetByIdAsync(string id);
    Task<Hold?> GetByIdWithDetailsAsync(string id);
    Task<int> CountActiveByStudentAsync(string classId, string studentId);
    Task<int> CountOccupiedAsync(string classId);
    Task<List<Hold>> GetByStudentAsync(string studentId);
    Task<List<Hold>> GetAllAsync(ApplicationFilter filter);
    Task<int> CountAllAsync(ApplicationFilter filter);
    Task AddAsync(Hold hold);
    Task UpdateAsync(Hold hold);
}

public interface IPaymentRepository
{
    Task<Payment?> GetByHoldIdAsync(string holdId);
    Task<Payment?> GetByIdAsync(string id);
    Task AddAsync(Payment payment);
    Task<decimal> SumCompletedAsync();
}
