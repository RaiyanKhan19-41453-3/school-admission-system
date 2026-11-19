namespace Admissions.Models;

public static class NotificationType
{
    public const string HoldCreated = "HOLD_CREATED";
    public const string ExpiryWarning = "EXPIRY_WARNING";
    public const string Expired = "EXPIRED";
    public const string PaymentConfirmed = "PAYMENT_CONFIRMED";
    public const string Cancelled = "CANCELLED";
}

/// <summary>In-app notification for a parent about their application.</summary>
public class Notification
{
    public string Id { get; set; } = string.Empty;
    public string StudentId { get; set; } = string.Empty;   // owner (Student.Id)
    public string Type { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? HoldId { get; set; }
    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Student Student { get; set; } = null!;
}
