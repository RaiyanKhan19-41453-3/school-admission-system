namespace Admissions.Models;

/// <summary>A file uploaded as part of an application (birth cert, photo, etc.).</summary>
public class ApplicationDocument
{
    public string Id { get; set; } = string.Empty;
    public string HoldId { get; set; } = string.Empty;
    public string DocumentType { get; set; } = string.Empty; // BirthCertificate, Photo, Transcript
    public string FileName { get; set; } = string.Empty;
    public string StoredName { get; set; } = string.Empty;   // generated name on disk
    public string ContentType { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

    public Hold Hold { get; set; } = null!;
}
