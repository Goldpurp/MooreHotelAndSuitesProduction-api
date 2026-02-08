using MooreHotels.Application.DTOs;
using MooreHotels.Application.Interfaces.Repositories;
using MooreHotels.Application.Interfaces.Services;
using MooreHotels.Domain.Entities;
using MooreHotels.Domain.Enums;

namespace MooreHotels.Application.Services;

public class RoomService : IRoomService
{
    private readonly IRoomRepository _roomRepo;
    private readonly IBookingRepository _bookingRepo;

    private const int CHECK_IN_HOUR = 15; // 3:00 PM
    private const int CHECK_OUT_HOUR = 11; // 11:00 AM

    public RoomService(IRoomRepository roomRepo, IBookingRepository bookingRepo)
    {
        _roomRepo = roomRepo;
        _bookingRepo = bookingRepo;
    }

    public async Task<PagedResultDto<RoomDto>> GetAllRoomsAsync(RoomCategory? category = null, int? page = null, int? pageSize = null)
    {
        var result = await _roomRepo.GetAllAsync(onlyOnline: false, page, pageSize); 
        var items = result.Items;
        if (category.HasValue) items = items.Where(r => r.Category == category);

        return new PagedResultDto<RoomDto>(
            items.Select(MapToDto),
            result.TotalCount,
            page,
            pageSize);
    }

    public async Task<PagedResultDto<RoomDto>> SearchRoomsAsync(RoomSearchRequest request)
    {
        var checkIn = request.CheckIn?.Date.AddHours(CHECK_IN_HOUR);
        var checkOut = request.CheckOut?.Date.AddHours(CHECK_OUT_HOUR);

        var result = await _roomRepo.SearchAsync(
            checkIn, 
            checkOut, 
            request.Category, 
            request.Capacity,
            request.RoomNumber,
            request.Amenity,
            request.Page,
            request.PageSize);

        return new PagedResultDto<RoomDto>(
            result.Items.Select(MapToDto),
            result.TotalCount,
            request.Page,
            request.PageSize);
    }

    public async Task<RoomDto?> GetRoomByIdAsync(Guid id)
    {
        var room = await _roomRepo.GetByIdAsync(id);
        return room != null ? MapToDto(room) : null;
    }

    public async Task<RoomAvailabilityResponse> CheckAvailabilityAsync(Guid roomId, DateTime checkIn, DateTime checkOut)
    {
        var room = await _roomRepo.GetByIdAsync(roomId);
        if (room == null) return new RoomAvailabilityResponse(false, "Asset not found in registry.");
        
        if (!room.IsOnline) 
            return new RoomAvailabilityResponse(false, "Asset is currently offline or under maintenance.");

        var start = checkIn.Date.AddHours(CHECK_IN_HOUR);
        var end = checkOut.Date.AddHours(CHECK_OUT_HOUR);

        if (start >= end)
            return new RoomAvailabilityResponse(false, $"Invalid range. Standard policy: Check-out by {CHECK_OUT_HOUR}:00 AM.");

        var isBooked = await _bookingRepo.IsRoomBookedAsync(roomId, start, end);
        
        if (isBooked)
            return new RoomAvailabilityResponse(false, "Asset is already secured for these dates.");

        return new RoomAvailabilityResponse(true, $"Available (Check-in {start:HH:mm}, Check-out {end:HH:mm}).");
    }

    public async Task<RoomDto> CreateRoomAsync(CreateRoomRequest request)
    {
        var existingRoom = await _roomRepo.GetByRoomNumberAsync(request.RoomNumber);
        if (existingRoom != null) throw new Exception($"Conflict: Room unit '{request.RoomNumber}' is already registered.");

        bool syncedOnlineStatus = request.Status != RoomStatus.Maintenance;

        var room = new Room
        {
            Id = Guid.NewGuid(),
            RoomNumber = request.RoomNumber,
            Name = request.Name,
            Category = request.Category,
            Floor = request.Floor,
            PricePerNight = request.PricePerNight,
            Capacity = request.Capacity,
            Size = request.Size,
            Description = request.Description,
            Amenities = request.Amenities ?? new List<string>(),
            Images = request.Images ?? new List<string>(),
            Status = request.Status,
            IsOnline = syncedOnlineStatus,
            CreatedAt = DateTime.UtcNow
        };

        await _roomRepo.AddAsync(room);
        return MapToDto(room);
    }

    public async Task UpdateRoomAsync(Guid id, UpdateRoomRequest request)
    {
        var room = await _roomRepo.GetByIdAsync(id);
        if (room == null) throw new Exception("Room not found");

        room.Name = request.Name;
        room.Category = request.Category;
        room.Status = request.Status;
        room.PricePerNight = request.PricePerNight;
        room.Capacity = request.Capacity;
        room.Description = request.Description;
        room.Amenities = request.Amenities;

        room.IsOnline = request.Status != RoomStatus.Maintenance;

        await _roomRepo.UpdateAsync(room);
    }

    public async Task DeleteRoomAsync(Guid id)
    {
        var room = await _roomRepo.GetByIdAsync(id);
        if (room != null) await _roomRepo.DeleteAsync(room);
    }

    private static RoomDto MapToDto(Room r) => new(
        r.Id, r.RoomNumber, r.Name, r.Category, r.Floor, r.Status, 
        r.PricePerNight, r.Capacity, r.Size, r.IsOnline, r.Description, 
        r.Amenities, r.Images, r.CreatedAt);
}