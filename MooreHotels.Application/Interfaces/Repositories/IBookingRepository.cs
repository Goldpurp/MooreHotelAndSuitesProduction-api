
using MooreHotels.Domain.Entities;

namespace MooreHotels.Application.Interfaces.Repositories;

public interface IBookingRepository
{
    Task<Booking?> GetByIdAsync(Guid id);
    Task<Booking?> GetByCodeAsync(string code);
    Task<IEnumerable<Booking>> GetAllAsync();
    Task<bool> IsRoomBookedAsync(Guid roomId, DateTime checkIn, DateTime checkOut);
    Task AddAsync(Booking booking);
    Task UpdateAsync(Booking booking);
    Task<IEnumerable<Booking>> GetPendingRefundsAsync();
}
