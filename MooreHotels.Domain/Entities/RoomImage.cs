namespace MooreHotels.Domain.Entities;

public class RoomImage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Url { get; set; } = string.Empty;
    public string PublicId { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Guid RoomId { get; set; }
    public Room Room { get; set; } = null!;
}
