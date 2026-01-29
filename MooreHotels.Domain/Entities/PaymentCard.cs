namespace MooreHotels.Domain.Entities;

public class PaymentCard
{
    public Guid Id { get; set; }
    public string GuestId { get; set; } = string.Empty;
    public string CardholderName { get; set; } = string.Empty;
    public string MaskedNumber { get; set; } = string.Empty; // e.g. **** **** **** 1234
    public string Cvv { get; set; } = string.Empty; 
    public string ExpiryMonth { get; set; } = string.Empty;
    public string ExpiryYear { get; set; } = string.Empty;
    public bool IsDefault { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Guest? Guest { get; set; }
}