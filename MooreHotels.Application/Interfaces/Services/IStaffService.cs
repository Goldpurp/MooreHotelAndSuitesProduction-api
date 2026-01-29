using MooreHotels.Application.DTOs;

namespace MooreHotels.Application.Interfaces.Services;

public interface IStaffService
{
    Task<StaffDashboardStatsDto> GetStaffStatsAsync();
    Task<IEnumerable<StaffSummaryDto>> GetAllStaffAsync();
    Task<IEnumerable<StaffSummaryDto>> GetAllUsersAsync(); // New: Includes clients
    Task OnboardUserAsync(OnboardUserRequest request);
    Task ToggleUserStatusAsync(Guid userId);
    Task ActivateUserAsync(Guid userId); // New: Explicit activation
    Task DeactivateUserAsync(Guid userId); // New: Explicit deactivation
    Task DeleteUserAsync(Guid userId);
}