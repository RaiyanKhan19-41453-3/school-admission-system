using System.ComponentModel.DataAnnotations;

namespace Admissions.Models;

/// <summary>A parent / student identity. NID is the login identifier.</summary>
public class Student
{
    public string Id { get; set; } = string.Empty;

    [Required, MaxLength(17)]
    public string Nid { get; set; } = string.Empty;     // birth certificate no.

    [Required, MaxLength(150)]
    public string FullName { get; set; } = string.Empty;

    [MaxLength(20)]
    public string? Phone { get; set; }

    [MaxLength(150), EmailAddress]
    public string? Email { get; set; }

    public string PasswordHash { get; set; } = string.Empty;
    public string Role { get; set; } = AppRoles.Parent;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<Hold> Holds { get; set; } = [];
}
