using Admissions.Data;
using Admissions.Models;
using Admissions.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Admissions.Services;

public interface IReceiptService
{
    Task<(byte[] bytes, string fileName)> GenerateAsync(string holdId, string nid);
}

public sealed class ReceiptService(AdmissionDbContext db, IStudentRepository students) : IReceiptService
{
    static string NewId(string p) => p + Guid.NewGuid().ToString("N")[0..12].ToLowerInvariant();

    public async Task<(byte[] bytes, string fileName)> GenerateAsync(string holdId, string nid)
    {
        var student = await students.GetByNidAsync(nid)
                      ?? throw new NotFoundException("Student not found.");
        var h = await db.Holds.Include(x => x.Class).Include(x => x.Student).Include(x => x.Payment)
                    .FirstOrDefaultAsync(x => x.Id == holdId)
                ?? throw new NotFoundException("Application not found.");
        if (h.StudentId != student.Id)
            throw new ForbiddenException("This application belongs to another student.");
        if (h.Status != HoldStatus.Confirmed || h.Payment is null)
            throw new ValidationException("Admission is not confirmed — no receipt available.");

        const string school = "ONNOROKOM SCHOOL - ADMISSION RECEIPT";
        var lines = new List<string>
        {
            "--------------------------------------------------------",
            $"Receipt No : {NewId("RCP-")}",
            $"Date       : {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC",
            "--------------------------------------------------------",
            $"Student    : {h.Student?.FullName ?? "-"}",
            $"NID        : {h.Student?.Nid ?? "-"}",
            $"Class      : {h.Class?.Name ?? "-"} {h.Class?.Grade ?? ""} {h.Class?.Shift ?? ""}".TrimEnd(),
            $"Guardian   : {h.GuardianName ?? "-"} ({h.GuardianPhone ?? "-"})",
            $"DOB        : {h.DateOfBirth?.ToString("yyyy-MM-dd") ?? "-"}",
            "--------------------------------------------------------",
            $"Fee        : TK {h.Payment.Amount:N2}",
            $"Method     : {h.Payment.Method}",
            $"Ref        : {h.Payment.TransactionRef ?? "-"}",
            $"Paid At    : {h.Payment.PaidAt:yyyy-MM-dd HH:mm} UTC",
            "--------------------------------------------------------",
            "Admission CONFIRMED. This is a computer-generated receipt.",
            "",
            "Thank you for enrolling with us."
        };

        var bytes = SimplePdfWriter.Build(school, lines);
        return (bytes, $"Receipt_{h.Id}.pdf");
    }
}
