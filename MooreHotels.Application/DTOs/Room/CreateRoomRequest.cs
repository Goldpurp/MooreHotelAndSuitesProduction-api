
using MooreHotels.Domain.Enums;

namespace MooreHotels.Application.DTOs;

public record CreateRoomRequest(
    string RoomNumber, 
    string Name, 
    RoomCategory Category, 
    PropertyFloor Floor, 
    RoomStatus Status, // Added to match UI "Current Status"
    long PricePerNight, 
    int Capacity, 
    string Size, 
    string Description, 
    List<string> Amenities, 
    List<string> Images);
