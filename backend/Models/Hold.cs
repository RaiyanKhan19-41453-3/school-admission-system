using System.ComponentModel.DataAnnotations;

namespace Admissions.Models;

/// <summary>An application: holds a seat until payment or expiry.</summary>
public class Hold
{
    public string Id { get; set; } = string.Empty;

    [Required] public string ClassId { get; set; } = string.Empty;
    [Required] public string StudentId { get; set; } = string.Empty;

    public string Status { get; set; } = HoldStatus.Held;

    public DateTime ExpiresAt { get; set; }
    public DateTime? ConfirmedAt { get; set; }
    public DateTime? CancelledAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public SchoolClass Class { get; set; } = null!;
    public Student Student { get; set; } = null!;
    public Payment? Payment { get; set; }
}
