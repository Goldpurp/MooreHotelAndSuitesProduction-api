using MooreHotels.Domain.Enums;

namespace MooreHotels.Application.DTOs;

public record CreateRoomRequest(
    string RoomNumber, 
    string Name, 
    RoomCategory Category, 
    PropertyFloor Floor, 
    RoomStatus Status,
    decimal PricePerNight, 
    int Guest, 
    string Size, 
    string Description, 
    List<string> Amenities);
