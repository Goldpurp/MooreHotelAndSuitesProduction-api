using MooreHotels.Application.DTOs;
using MooreHotels.Domain.Enums;

namespace MooreHotels.Application.Interfaces.Services;

public interface IRoomService
{
    Task<PagedResultDto<RoomDto>> GetAllRoomsAsync(RoomCategory? category = null, int? page = null, int? pageSize = null);
    Task<PagedResultDto<RoomDto>> SearchRoomsAsync(RoomSearchRequest request);
    Task<RoomDto?> GetRoomByIdAsync(Guid id);
    Task<RoomDto> CreateRoomAsync(CreateRoomRequest request);
    Task UpdateRoomAsync(Guid id, UpdateRoomRequest request);
    Task DeleteRoomAsync(Guid id);
    Task<RoomAvailabilityResponse> CheckAvailabilityAsync(Guid roomId, DateTime checkIn, DateTime checkOut);
}