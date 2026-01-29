
namespace MooreHotels.Domain.Entities;

public class AuditLog
{
    public Guid Id { get; set; }
    public Guid ProfileId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;
    public string? OldDataJson { get; set; }
    public string? NewDataJson { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
