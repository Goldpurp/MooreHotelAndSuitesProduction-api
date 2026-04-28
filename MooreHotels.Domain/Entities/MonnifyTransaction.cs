namespace MooreHotels.Domain.Entities;

public class MonnifyTransaction
{
    public Guid Id { get; set; }
    public string BookingCode { get; set; } = string.Empty;
    public string TransactionReference { get; set; } = string.Empty; // Internal ref
    public string? MonnifyReference { get; set; } // External ref from Monnify
    public string? MerchantReference { get; set; } 
    public decimal Amount { get; set; }
    public decimal? Fee { get; set; }
    public decimal? SettledAmount { get; set; }
    public string Status { get; set; } = "PENDING"; // PENDING, PAID, OVERPAID, PARTIALLY_PAID, FAILED, CANCELLED
    public string? PaymentMethod { get; set; } // CARD, ACCOUNT_TRANSFER, etc.
    public string? CustomerEmail { get; set; }
    public string? CustomerName { get; set; }
    public string? RawPayloadJson { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}
