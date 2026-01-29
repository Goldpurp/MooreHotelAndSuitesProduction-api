
using MooreHotels.Domain.Entities;

namespace MooreHotels.Application.Interfaces.Repositories;

public interface IVisitRecordRepository
{
    Task<IEnumerable<VisitRecord>> GetAllAsync();
    Task AddAsync(VisitRecord record);
}
