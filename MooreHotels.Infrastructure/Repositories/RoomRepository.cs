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

    public async Task<(IEnumerable<Room> Items, int TotalCount)> GetAllAsync(bool onlyOnline = true, int? page = null, int? pageSize = null)
    {
        var query = _db.Rooms.AsQueryable();
        if (onlyOnline) query = query.Where(r => r.IsOnline);

        int totalCount = await query.CountAsync();

        if (page.HasValue && pageSize.HasValue)
        {
            query = query.OrderBy(r => r.RoomNumber)
                         .Skip((page.Value - 1) * pageSize.Value)
                         .Take(pageSize.Value);
        }
        else
        {
            query = query.OrderBy(r => r.RoomNumber);
        }

        var items = await query.ToListAsync();
        return (items, totalCount);
    }

    public async Task<(IEnumerable<Room> Items, int TotalCount)> SearchAsync(
        DateTime? checkIn, 
        DateTime? checkOut, 
        RoomCategory? category, 
        int? capacity,
        string? roomNumber,
        string? amenity,
        int? page = null,
        int? pageSize = null)
    {
        var query = _db.Rooms.AsQueryable();

        if (checkIn.HasValue && checkOut.HasValue)
        {
            var start = DateTime.SpecifyKind(checkIn.Value, DateTimeKind.Utc);
            var end = DateTime.SpecifyKind(checkOut.Value, DateTimeKind.Utc);

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

        query = query.Where(r => r.IsOnline);

        // Pre-pagination filter for amenities (complex check)
        if (!string.IsNullOrWhiteSpace(amenity))
        {
            // Note: Since amenities is a jsonb list, we search using EF.Functions or materialization
            // For simplicity in this implementation, we use a string contains if searched
            query = query.Where(r => EF.Functions.JsonContains(r.Amenities, $"[\"{amenity}\"]"));
        }

        int totalCount = await query.CountAsync();

        if (page.HasValue && pageSize.HasValue)
        {
            query = query.OrderBy(r => r.RoomNumber)
                         .Skip((page.Value - 1) * pageSize.Value)
                         .Take(pageSize.Value);
        }
        else
        {
            query = query.OrderBy(r => r.RoomNumber);
        }

        var items = await query.ToListAsync();
        return (items, totalCount);
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