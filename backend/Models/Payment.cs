using System.ComponentModel.DataAnnotations;

namespace Admissions.Models;

public static class PaymentStatus
{
    public const string Completed = "COMPLETED";
    public const string Refunded = "REFUNDED";
}

/// <summary>A recorded admission-fee payment (payment gateway is simulated).</summary>
public class Payment
{
    public string Id { get; set; } = string.Empty;

    [Required] public string HoldId { get; set; } = string.Empty;
    public decimal Amount { get; set; }

    [MaxLength(40)]
    public string Method { get; set; } = "MOCK";       // MOCK / BKASH / CARD ...

    [MaxLength(100)]
    public string? TransactionRef { get; set; }

    public string Status { get; set; } = PaymentStatus.Completed;
    public DateTime PaidAt { get; set; } = DateTime.UtcNow;

    public Hold Hold { get; set; } = null!;
}
