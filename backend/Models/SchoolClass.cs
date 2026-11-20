using System.ComponentModel.DataAnnotations;

namespace Admissions.Models;

/// <summary>
/// One "class + shift" combination, e.g. "Class 6 - Morning Shift".
/// Each has its own seat limit and admission fee.
/// </summary>
public class SchoolClass
{
    public string Id { get; set; } = string.Empty;

    [Required]
    public string AcademicYearId { get; set; } = string.Empty;

    [Required, MaxLength(120)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(40)]
    public string? Shift { get; set; }          // Morning / Day / Evening

    [MaxLength(20)]
    public string? Grade { get; set; }          // "Class 6"

    [Range(1, 10000)]
    public int TotalSeats { get; set; }

    /// <summary>Atomic counter — decremented on hold, restored on cancel/expiry.</summary>
    public int SeatsRemaining { get; set; }

    [Range(0, 1000000)]
    public decimal FeeAmount { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public AcademicYear AcademicYear { get; set; } = null!;
    public ICollection<Hold> Holds { get; set; } = [];
}
