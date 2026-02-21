using MooreHotels.Domain.Enums;

namespace MooreHotels.Application.DTOs;

public record UpdateRoomRequest(
    string Name, 
    RoomCategory Category,
    PropertyFloor Floor, 
    RoomStatus Status, 
    decimal PricePerNight, 
    int Guest, 
    bool IsOnline, 
    string Description, 
    List<string> Amenities,
    List<string> Images);
