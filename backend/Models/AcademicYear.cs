using System.ComponentModel.DataAnnotations;

namespace Admissions.Models;

/// <summary>A school year / admission session, e.g. "2026-2027".</summary>
public class AcademicYear
{
    public string Id { get; set; } = string.Empty;

    [Required, MaxLength(50)]
    public string Name { get; set; } = string.Empty;   // e.g. "2026-2027"

    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }

    /// <summary>Only one active year at a time. Admins activate a year to open admissions.</summary>
    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<SchoolClass> Classes { get; set; } = [];
}
