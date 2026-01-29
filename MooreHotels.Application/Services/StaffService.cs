using Microsoft.AspNetCore.Identity;
using MooreHotels.Application.DTOs;
using MooreHotels.Application.Interfaces.Services;
using MooreHotels.Domain.Entities;
using MooreHotels.Domain.Enums;

namespace MooreHotels.Application.Services;

public class StaffService : IStaffService
{
    private readonly UserManager<ApplicationUser> _userManager;

    public StaffService(UserManager<ApplicationUser> userManager)
    {
        _userManager = userManager;
    }

    public Task<StaffDashboardStatsDto> GetStaffStatsAsync()
    {
        var users = _userManager.Users.ToList();
        var stats = new StaffDashboardStatsDto(
            ActiveAccounts: users.Count(u => u.Status == ProfileStatus.Active && u.Role != UserRole.Client),
            TotalStaffCount: users.Count(u => u.Role != UserRole.Client),
            AccessSuspended: users.Count(u => u.Status == ProfileStatus.Suspended && u.Role != UserRole.Client)
        );
        return Task.FromResult(stats);
    }

    public Task<IEnumerable<StaffSummaryDto>> GetAllStaffAsync()
    {
        var staff = _userManager.Users
            .Where(u => u.Role == UserRole.Admin || u.Role == UserRole.Manager || u.Role == UserRole.Staff)
            .OrderByDescending(u => u.CreatedAt)
            .ToList()
            .Select(u => new StaffSummaryDto(
                u.Id,
                u.Name,
                u.Email!,
                u.PhoneNumber,
                u.AvatarUrl,
                u.Role,
                u.CreatedAt,
                u.Status
            ));
        return Task.FromResult(staff);
    }

    public Task<IEnumerable<StaffSummaryDto>> GetAllUsersAsync()
    {
        var users = _userManager.Users
            .OrderByDescending(u => u.CreatedAt)
            .ToList()
            .Select(u => new StaffSummaryDto(
                u.Id,
                u.Name,
                u.Email!,
                u.PhoneNumber,
                u.AvatarUrl,
                u.Role,
                u.CreatedAt,
                u.Status
            ));
        return Task.FromResult(users);
    }

    public async Task OnboardUserAsync(OnboardUserRequest request)
    {
        if (request.AssignedRole == UserRole.Client)
            throw new Exception("Operational Error: Client accounts must be registered via the public portal.");

        var existing = await _userManager.FindByEmailAsync(request.Email);
        if (existing != null) throw new Exception("A user with this email already exists.");

        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = request.Email,
            Email = request.Email,
            Name = request.FullName,
            Role = request.AssignedRole,
            Status = request.Status,
            EmailConfirmed = true,
            CreatedAt = DateTime.UtcNow
        };

        var result = await _userManager.CreateAsync(user, request.TemporaryPassword);
        if (!result.Succeeded)
        {
            var errors = string.Join(", ", result.Errors.Select(e => e.Description));
            throw new Exception($"Provisioning failed: {errors}");
        }

        await _userManager.AddToRoleAsync(user, request.AssignedRole.ToString());
    }

    public async Task ToggleUserStatusAsync(Guid userId)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null) throw new Exception("Target user profile not found.");

        if (user.Role == UserRole.Admin)
            throw new Exception("Security Violation: System Administrator status is immutable via this interface.");

        user.Status = user.Status == ProfileStatus.Active ? ProfileStatus.Suspended : ProfileStatus.Active;
        await _userManager.UpdateAsync(user);
    }

    public async Task ActivateUserAsync(Guid userId)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null) throw new Exception("Target user profile not found.");

        user.Status = ProfileStatus.Active;
        await _userManager.UpdateAsync(user);
    }

    public async Task DeactivateUserAsync(Guid userId)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null) throw new Exception("Target user profile not found.");

        if (user.Role == UserRole.Admin)
            throw new Exception("Security Violation: Administrative accounts cannot be deactivated.");

        user.Status = ProfileStatus.Suspended;
        await _userManager.UpdateAsync(user);
    }

    public async Task DeleteUserAsync(Guid userId)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null) return;

        if (user.Role == UserRole.Admin)
            throw new Exception("Security Violation: Root Administrator accounts cannot be deleted.");
        
        await _userManager.DeleteAsync(user);
    }
}