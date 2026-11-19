using System.ComponentModel.DataAnnotations;

namespace Admissions.Dtos;

// ── Academic years ───────────────────────────────────────────────────────────
public record CreateYearRequest(
    [Required, StringLength(50, MinimumLength = 4)] string Name,
    DateTime StartDate,
    DateTime EndDate);

public record YearResponse(
    string Id, string Name, DateTime StartDate, DateTime EndDate,
    bool IsActive, int ClassCount);

// ── Classes ──────────────────────────────────────────────────────────────────
public record CreateClassRequest(
    [Required, StringLength(120, MinimumLength = 2)] string Name,
    string? Shift,
    string? Grade,
    [Range(1, 10000)] int TotalSeats,
    [Range(0, 1000000)] decimal FeeAmount,
    string? AcademicYearId);

public record UpdateClassRequest(
    [Required, StringLength(120, MinimumLength = 2)] string Name,
    string? Shift,
    string? Grade,
    [Range(1, 10000)] int TotalSeats,
    [Range(0, 1000000)] decimal FeeAmount,
    bool IsActive);

public record ClassResponse(
    string Id, string Name, string? Shift, string? Grade,
    int TotalSeats, decimal FeeAmount, bool IsActive,
    string AcademicYearName,
    int HeldSeats, int ConfirmedSeats, int AvailableSeats);

// ── Applications / holds ────────────────────────────────────────────────────
public record HoldResponse(
    string HoldId, string ClassId, string StudentId,
    string Status, DateTime ExpiresAt, DateTime? ConfirmedAt);

public record MyHoldResponse(
    string HoldId, string ClassId, string ClassName,
    string Grade, string? Shift, decimal FeeAmount,
    string Status, DateTime ExpiresAt, DateTime? ConfirmedAt,
    bool CanConfirm, bool CanCancel, string? TransactionRef);

public record ApplicationFilter(string? Status, string? ClassId, int Page = 1, int PageSize = 20);

public record AdminApplicationResponse(
    string HoldId, string ClassName, string StudentName, string StudentNid,
    string Status, DateTime ExpiresAt, string? TransactionRef);

// ── Payments ─────────────────────────────────────────────────────────────────
public record PayRequest(
    [Required, StringLength(40)] string Method,
    [StringLength(100)] string? TransactionRef);

public record PaymentResponse(
    string PaymentId, string HoldId, decimal Amount,
    string Method, string? TransactionRef, string Status, DateTime PaidAt);

// ── Students / auth ──────────────────────────────────────────────────────────
public record RegisterRequest(
    [Required, StringLength(17, MinimumLength = 17)] string Nid,
    [Required, StringLength(150, MinimumLength = 2)] string FullName,
    [Phone] string? Phone,
    [EmailAddress] string? Email,
    [Required, StringLength(100, MinimumLength = 6)] string Password);

public record LoginRequest([Required, StringLength(17)] string Nid,
                           [Required] string Password);

public record LoginResponse(string Token, DateTime ExpiresAt, string Role, string FullName);

public record StudentResponse(
    string Id, string Nid, string FullName, string? Phone,
    string? Email, string Role, DateTime CreatedAt, int TotalApplications);

public record UpdateProfileRequest(
    [Required, StringLength(150, MinimumLength = 2)] string FullName,
    [Phone] string? Phone,
    [EmailAddress] string? Email);

// ── Dashboard ────────────────────────────────────────────────────────────────
public record DashboardResponse(
    string ActiveYear, int ActiveClasses, int TotalSeats, int SeatsFilled,
    int HeldApplications, int ConfirmedApplications, int ExpiredApplications,
    int TotalStudents, decimal RevenueCollected);

// ── Shared ───────────────────────────────────────────────────────────────────
public record PagedResponse<T>(IReadOnlyList<T> Items, int Page, int PageSize, int Total);
public record ErrorResponse(string Code, string Message);
public record MessageResponse(string Message);
