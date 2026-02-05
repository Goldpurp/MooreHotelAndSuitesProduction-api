using MooreHotels.Application.DTOs;

namespace MooreHotels.Application.Interfaces.Services;

public interface IStaffService
{
    Task<StaffDashboardStatsDto> GetStaffStatsAsync();
    Task<IEnumerable<StaffSummaryDto>> GetAllStaffAsync();
    Task<IEnumerable<StaffSummaryDto>> GetAllUsersAsync(); 
    Task OnboardUserAsync(OnboardUserRequest request);
    Task OnboardUserAsync(OnboardUserRequest request, Guid actingUserId);
    Task ToggleUserStatusAsync(Guid userId);
    Task ActivateUserAsync(Guid userId); 
    Task DeactivateUserAsync(Guid userId); 
    Task DeleteUserAsync(Guid userId);
}