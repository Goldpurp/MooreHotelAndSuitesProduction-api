using MooreHotels.Domain.Enums;

namespace MooreHotels.Application.DTOs;

public record UpdateRoomRequest(
    string Name, 
    RoomCategory Category, 
    RoomStatus Status, 
    decimal PricePerNight, 
    int Capacity, 
    bool IsOnline, 
    string Description, 
    List<string> Amenities);