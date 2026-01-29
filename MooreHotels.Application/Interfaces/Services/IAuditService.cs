
using MooreHotels.Application.DTOs;

namespace MooreHotels.Application.Interfaces.Services;

public interface IAuditService
{
    Task<IEnumerable<AuditLogDto>> GetAllLogsAsync();
    Task LogActionAsync(Guid userId, string action, string entityType, string entityId, object? oldData = null, object? newData = null);
}
