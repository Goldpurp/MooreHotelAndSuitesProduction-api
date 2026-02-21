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
    private readonly IImageService _imageService;

    private const int CHECK_IN_HOUR = 15; // 3:00 PM
    private const int CHECK_OUT_HOUR = 11; // 11:00 AM

    public RoomService(IRoomRepository roomRepo, IBookingRepository bookingRepo, IImageService imageService)
    {
        _roomRepo = roomRepo;
        _bookingRepo = bookingRepo;
        _imageService = imageService;
    }

    public async Task<IEnumerable<RoomDto>> GetAllRoomsAsync(RoomCategory? category = null)
    {
        var rooms = await _roomRepo.GetAllAsync(onlyOnline: false);
        if (category.HasValue) rooms = rooms.Where(r => r.Category == category);

        return rooms.Select(MapToDto);
    }

    public async Task<IEnumerable<RoomDto>> SearchRoomsAsync(RoomSearchRequest request)
    {
        var checkIn = request.CheckIn?.Date.AddHours(CHECK_IN_HOUR);
        var checkOut = request.CheckOut?.Date.AddHours(CHECK_OUT_HOUR);

        var rooms = await _roomRepo.SearchAsync(
            checkIn,
            checkOut,
            request.Category,
            request.Guest,
            request.RoomNumber,
            request.Amenity);

        return rooms.Select(MapToDto);
    }

    public async Task<RoomDto?> GetRoomByIdAsync(Guid id)
    {
        var room = await _roomRepo.GetByIdWithImagesAsync(id);
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

        var room = new Room
        {
            Id = Guid.NewGuid(),
            RoomNumber = request.RoomNumber,
            Name = request.Name,
            Category = request.Category,
            Floor = request.Floor,
            PricePerNight = request.PricePerNight,
            Guest = request.Guest,
            Size = request.Size,
            Description = request.Description,
            Amenities = request.Amenities ?? new List<string>(),
            Images = new List<RoomImage>(),
            Status = request.Status,
            IsOnline = request.Status != RoomStatus.Maintenance,
            CreatedAt = DateTime.UtcNow
        };

        await _roomRepo.AddAsync(room);
        return MapToDto(room);
    }


public async Task UpdateRoomAsync(Guid id, UpdateRoomRequest request)
{
    var room = await _roomRepo.GetByIdWithImagesAsync(id);
    if (room == null) throw new Exception("Room not found");

    room.Name = request.Name;
    room.Category = request.Category;
    room.Status = request.Status;
    room.PricePerNight = request.PricePerNight;
    room.Guest = request.Guest;
    room.Description = request.Description;
    room.Amenities = request.Amenities;
    room.IsOnline = request.Status != RoomStatus.Maintenance;

    await _roomRepo.UpdateAsync(room); 
}


   public async Task<List<string>> DeleteRoomAsync(Guid id)
{
    var room = await _roomRepo.GetByIdWithImagesAsync(id);
    if (room == null) return new List<string>();

    var publicIds = room.Images
        .Select(img => img.PublicId)
        .Where(id => !string.IsNullOrEmpty(id))
        .ToList();

    await _roomRepo.DeleteAsync(room);

    return publicIds;
}


    public async Task AddImagesToRoomAsync(Guid roomId, List<ImageUploadResult> results)
    {
        var room = await _roomRepo.GetByIdWithImagesAsync(roomId);
        if (room == null) return;

        foreach (var res in results)
        {
            room.Images.Add(new RoomImage
            {
                Url = res.Url,
                PublicId = res.PublicId
            });
        }
    }



    private static RoomDto MapToDto(Room r) => new(
        r.Id, r.RoomNumber, r.Name, r.Category, r.Floor, r.Status,
        r.PricePerNight, r.Guest, r.Size, r.IsOnline, r.Description,
        r.Amenities, r.Images.Select(i => i.Url).ToList(), r.CreatedAt);
}