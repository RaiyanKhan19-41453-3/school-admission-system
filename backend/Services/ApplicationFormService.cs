using Admissions.Data;
using Admissions.Dtos;
using Admissions.Models;
using Admissions.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Admissions.Services;

public interface IApplicationFormService
{
    Task<ApplicationDetailResponse> SubmitDetailsAsync(string holdId, string nid, SubmitDetailsRequest req);
    Task<ApplicationDetailResponse> GetDetailsAsync(string holdId, string nid);
    Task<ApplicationDetailResponse> GetDetailsForAdminAsync(string holdId);
    Task<DocumentResponse> UploadDocumentAsync(string holdId, string nid, string documentType,
        string fileName, string contentType, Stream stream);
    Task<(string storedName, string fileName, string contentType)?> GetDocumentForDownloadAsync(string docId, string nid);
    Task<bool> DeleteDocumentAsync(string docId, string nid);
}

public sealed class ApplicationFormService(
    AdmissionDbContext db,
    IStudentRepository students,
    IWebHostEnvironment env) : IApplicationFormService
{
    static string NewId(string p) => p + Guid.NewGuid().ToString("N")[0..12].ToLowerInvariant();

    string UploadDir
    {
        get
        {
            var configured = Environment.GetEnvironmentVariable("UPLOAD_DIR");
            var dir = string.IsNullOrWhiteSpace(configured)
                ? Path.Combine(env.ContentRootPath, "uploads")
                : configured;
            Directory.CreateDirectory(dir);
            return dir;
        }
    }

    async Task<Hold> OwnedHoldAsync(string holdId, string nid)
    {
        var student = await students.GetByNidAsync(nid)
                      ?? throw new NotFoundException("Student not found.");
        var h = await db.Holds.Include(x => x.Class).Include(x => x.Documents)
                   .FirstOrDefaultAsync(x => x.Id == holdId)
                ?? throw new NotFoundException("Application not found.");
        if (h.StudentId != student.Id)
            throw new ForbiddenException("This application belongs to another student.");
        return h;
    }

    static ApplicationDetailResponse ToResponse(Hold h) => new(
        h.Id, h.Class?.Name ?? "", "", "",
        h.Status, h.ExpiresAt,
        h.DateOfBirth, h.GuardianName, h.GuardianPhone, h.Address, h.PreviousSchool,
        h.DetailsSubmitted,
        h.Documents.Select(d => new DocumentResponse(d.Id, d.DocumentType, d.FileName, d.FileSize, d.UploadedAt)).ToList());

    public async Task<ApplicationDetailResponse> SubmitDetailsAsync(string holdId, string nid, SubmitDetailsRequest req)
    {
        var h = await OwnedHoldAsync(holdId, nid);
        if (string.IsNullOrWhiteSpace(req.GuardianName) || string.IsNullOrWhiteSpace(req.GuardianPhone))
            throw new ValidationException("Guardian name and phone are required.");
        h.DateOfBirth = req.DateOfBirth.ToUniversalTime();
        h.GuardianName = req.GuardianName.Trim();
        h.GuardianPhone = req.GuardianPhone.Trim();
        h.Address = req.Address?.Trim();
        h.PreviousSchool = req.PreviousSchool?.Trim();
        h.DetailsSubmitted = true;
        h.DetailsSubmittedAt = DateTime.UtcNow;
        h.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return ToResponse(h);
    }

    public async Task<ApplicationDetailResponse> GetDetailsAsync(string holdId, string nid)
    {
        var h = await OwnedHoldAsync(holdId, nid);
        return ToResponse(h);
    }

    public async Task<ApplicationDetailResponse> GetDetailsForAdminAsync(string holdId)
    {
        var h = await db.Holds.Include(x => x.Class).Include(x => x.Student).Include(x => x.Documents)
                   .FirstOrDefaultAsync(x => x.Id == holdId)
                ?? throw new NotFoundException("Application not found.");
        var r = ToResponse(h);
        return r with
        {
            StudentName = h.Student?.FullName ?? "",
            StudentNid = h.Student?.Nid ?? ""
        };
    }

    public async Task<DocumentResponse> UploadDocumentAsync(string holdId, string nid, string documentType,
        string fileName, string contentType, Stream stream)
    {
        var h = await OwnedHoldAsync(holdId, nid);
        if (string.IsNullOrWhiteSpace(documentType)) documentType = "Other";
        var safeType = Path.GetInvalidFileNameChars().Aggregate(documentType.Trim(), (s, c) => s.Replace(c, '_'));
        var extension = Path.GetExtension(fileName);
        if (extension.Length > 10) extension = "";
        var stored = $"{NewId("doc_")}_{safeType}{extension}";
        var path = Path.Combine(UploadDir, stored);
        await using (var fs = new FileStream(path, FileMode.CreateNew, FileAccess.Write))
            await stream.CopyToAsync(fs);

        var doc = new ApplicationDocument
        {
            Id = NewId("d_"), HoldId = h.Id, DocumentType = documentType.Trim(),
            FileName = Path.GetFileName(fileName), StoredName = stored,
            ContentType = string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType,
            FileSize = new FileInfo(path).Length
        };
        db.Documents.Add(doc);
        await db.SaveChangesAsync();
        return new DocumentResponse(doc.Id, doc.DocumentType, doc.FileName, doc.FileSize, doc.UploadedAt);
    }

    public async Task<(string storedName, string fileName, string contentType)?> GetDocumentForDownloadAsync(string docId, string nid)
    {
        var student = await students.GetByNidAsync(nid)
                      ?? throw new NotFoundException("Student not found.");
        var doc = await db.Documents.Include(d => d.Hold)
                      .FirstOrDefaultAsync(d => d.Id == docId)
                  ?? throw new NotFoundException("Document not found.");
        if (doc.Hold.StudentId != student.Id)
            throw new ForbiddenException("This document belongs to another student.");
        return (doc.StoredName, doc.FileName, doc.ContentType);
    }

    public async Task<bool> DeleteDocumentAsync(string docId, string nid)
    {
        var student = await students.GetByNidAsync(nid)
                      ?? throw new NotFoundException("Student not found.");
        var doc = await db.Documents.Include(d => d.Hold)
                      .FirstOrDefaultAsync(d => d.Id == docId)
                  ?? throw new NotFoundException("Document not found.");
        if (doc.Hold.StudentId != student.Id)
            throw new ForbiddenException("This document belongs to another student.");
        var path = Path.Combine(UploadDir, doc.StoredName);
        db.Documents.Remove(doc);
        await db.SaveChangesAsync();
        if (File.Exists(path)) File.Delete(path);
        return true;
    }
}
