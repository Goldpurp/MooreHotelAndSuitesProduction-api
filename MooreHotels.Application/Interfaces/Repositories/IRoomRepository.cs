using MooreHotels.Domain.Entities;
using MooreHotels.Domain.Enums;

namespace MooreHotels.Application.Interfaces.Repositories;

public interface IRoomRepository
{
    Task<Room?> GetByIdAsync(Guid id);
    Task<Room?> GetByRoomNumberAsync(string roomNumber);
    Task<(IEnumerable<Room> Items, int TotalCount)> GetAllAsync(bool onlyOnline = true, int? page = null, int? pageSize = null);
    Task<(IEnumerable<Room> Items, int TotalCount)> SearchAsync(
        DateTime? checkIn, 
        DateTime? checkOut, 
        RoomCategory? category, 
        int? capacity,
        string? roomNumber,
        string? amenity,
        int? page = null,
        int? pageSize = null);
    Task AddAsync(Room room);
    Task UpdateAsync(Room room);
    Task DeleteAsync(Room room);
    Task<bool> ExistsAsync(Guid id);
}