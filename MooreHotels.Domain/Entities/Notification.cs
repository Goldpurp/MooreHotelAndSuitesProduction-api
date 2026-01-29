namespace MooreHotels.Domain.Entities;

public class Notification
{
    public Guid Id { get; set; }
    public Guid? UserId { get; set; } // Targeted user, null for broadcast to all staff/admins
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? BookingCode { get; set; }
    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}