using MooreHotels.Application.DTOs;

namespace MooreHotels.Application.Interfaces.Services;

public interface IOperationService
{
    Task<IEnumerable<OperationLogEntryDto>> GetLedgerAsync(string? filter = null, string? search = null);
    Task<DashboardKpis> GetOperationalKpisAsync();
}