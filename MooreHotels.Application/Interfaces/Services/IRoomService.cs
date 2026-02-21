using MooreHotels.Application.DTOs;
using MooreHotels.Domain.Enums;

namespace MooreHotels.Application.Interfaces.Services;

public interface IRoomService
{
    Task<IEnumerable<RoomDto>> GetAllRoomsAsync(RoomCategory? category = null);
    Task<IEnumerable<RoomDto>> SearchRoomsAsync(RoomSearchRequest request);
    Task<RoomDto?> GetRoomByIdAsync(Guid id);
    Task<RoomDto> CreateRoomAsync(CreateRoomRequest request);
    Task UpdateRoomAsync(Guid id, UpdateRoomRequest request);
    Task<List<string>> DeleteRoomAsync(Guid id);
    Task AddImagesToRoomAsync(Guid roomId, List<ImageUploadResult> results);
    Task<RoomAvailabilityResponse> CheckAvailabilityAsync(Guid roomId, DateTime checkIn, DateTime checkOut);
}