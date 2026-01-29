using Microsoft.EntityFrameworkCore;
using MooreHotels.Application.Interfaces.Repositories;
using MooreHotels.Domain.Entities;
using MooreHotels.Domain.Enums;
using MooreHotels.Infrastructure.Persistence;

namespace MooreHotels.Infrastructure.Repositories;

public class RoomRepository : IRoomRepository
{
    private readonly MooreHotelsDbContext _db;
    public RoomRepository(MooreHotelsDbContext db) => _db = db;

    public async Task<Room?> GetByIdAsync(Guid id) => await _db.Rooms.FindAsync(id);

    public async Task<Room?> GetByRoomNumberAsync(string roomNumber) => 
        await _db.Rooms.FirstOrDefaultAsync(r => r.RoomNumber == roomNumber);

    public async Task<IEnumerable<Room>> GetAllAsync(bool onlyOnline = true)
    {
        var query = _db.Rooms.AsQueryable();
        if (onlyOnline) query = query.Where(r => r.IsOnline);
        return await query.ToListAsync();
    }

    public async Task<IEnumerable<Room>> SearchAsync(
        DateTime? checkIn, 
        DateTime? checkOut, 
        RoomCategory? category, 
        int? capacity,
        string? roomNumber,
        string? amenity)
    {
        var query = _db.Rooms.AsQueryable();

        if (checkIn.HasValue && checkOut.HasValue)
        {
            var start = checkIn.Value;
            var end = checkOut.Value;

            // EXCLUDE Cancelled and CheckedOut bookings. 
            // Use exact timestamp comparison for the 3PM/11AM policy overlap.
            var bookedRoomIds = await _db.Bookings
                .Where(b => b.Status != BookingStatus.Cancelled && b.Status != BookingStatus.CheckedOut)
                .Where(b => b.CheckIn < end && b.CheckOut > start)
                .Select(b => b.RoomId)
                .Distinct()
                .ToListAsync();

            query = query.Where(r => !bookedRoomIds.Contains(r.Id));
        }

        if (!string.IsNullOrWhiteSpace(roomNumber))
            query = query.Where(r => r.RoomNumber.Contains(roomNumber));

        if (category.HasValue) 
            query = query.Where(r => r.Category == category.Value);

        if (capacity.HasValue && capacity.Value > 0) 
            query = query.Where(r => r.Capacity >= capacity.Value);

        // A room must be "Online" and not under "Maintenance" to be returned in general searches
        query = query.Where(r => r.IsOnline && r.Status != RoomStatus.Maintenance);

        var rooms = await query.ToListAsync();

        if (!string.IsNullOrWhiteSpace(amenity))
        {
            rooms = rooms.Where(r => r.Amenities.Any(a => a.Contains(amenity, StringComparison.OrdinalIgnoreCase))).ToList();
        }

        return rooms;
    }

    public async Task AddAsync(Room room)
    {
        await _db.Rooms.AddAsync(room);
        await _db.SaveChangesAsync();
    }

    public async Task UpdateAsync(Room room)
    {
        _db.Rooms.Update(room);
        await _db.SaveChangesAsync();
    }

    public async Task DeleteAsync(Room room)
    {
        _db.Rooms.Remove(room);
        await _db.SaveChangesAsync();
    }

    public async Task<bool> ExistsAsync(Guid id) => await _db.Rooms.AnyAsync(r => r.Id == id);
}