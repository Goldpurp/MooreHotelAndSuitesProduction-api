using System.ComponentModel.DataAnnotations;

public record CancelBookingRequest(
    [Required] string BookingCode,
    [Required, EmailAddress] string Email,
    [StringLength(500)] string? Reason = null
);
