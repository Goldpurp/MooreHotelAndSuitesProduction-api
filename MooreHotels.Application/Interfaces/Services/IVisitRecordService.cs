
using MooreHotels.Application.DTOs;

namespace MooreHotels.Application.Interfaces.Services;

public interface IVisitRecordService
{
    Task<IEnumerable<VisitRecordDto>> GetAllRecordsAsync();
    Task CreateRecordAsync(string bookingCode, string action, string authorizedBy);
}
