using Microsoft.EntityFrameworkCore;
using MooreHotels.Application.Interfaces.Repositories;
using MooreHotels.Domain.Entities;
using MooreHotels.Domain.Enums;
using MooreHotels.Infrastructure.Persistence;

namespace MooreHotels.Infrastructure.Repositories;

public class BookingRepository : IBookingRepository
{
    private readonly MooreHotelsDbContext _db;
    public BookingRepository(MooreHotelsDbContext db) => _db = db;

    public async Task<Booking?> GetByIdAsync(Guid id) => 
        await _db.Bookings.Include(b => b.Room).Include(b => b.Guest).FirstOrDefaultAsync(b => b.Id == id);

    public async Task<Booking?> GetByCodeAsync(string code) => 
        await _db.Bookings.Include(b => b.Room).Include(b => b.Guest).FirstOrDefaultAsync(b => b.BookingCode == code);

    public async Task<IEnumerable<Booking>> GetAllAsync() => 
        await _db.Bookings.Include(b => b.Room).Include(b => b.Guest).ToListAsync();

    public async Task<bool> IsRoomBookedAsync(Guid roomId, DateTime checkIn, DateTime checkOut) =>
        await _db.Bookings.AnyAsync(b => b.RoomId == roomId && 
                                         b.Status != BookingStatus.Cancelled && 
                                         b.Status != BookingStatus.CheckedOut &&
                                         b.CheckIn < checkOut && b.CheckOut > checkIn);

    public async Task AddAsync(Booking booking)
    {
        await _db.Bookings.AddAsync(booking);
        await _db.SaveChangesAsync();
    }

    public async Task UpdateAsync(Booking booking)
    {
        _db.Bookings.Update(booking);
        await _db.SaveChangesAsync();
    }

    public async Task<IEnumerable<Booking>> GetPendingRefundsAsync()
{
    return await _db.Bookings
        .Include(b => b.Guest)
        .Include(b => b.Room)
        .Where(b => b.Status == BookingStatus.Cancelled && 
                    b.PaymentStatus == PaymentStatus.RefundPending)
        .OrderByDescending(b => b.CreatedAt)
        .ToListAsync();
}

}