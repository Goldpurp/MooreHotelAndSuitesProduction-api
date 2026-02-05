using Microsoft.AspNetCore.Identity;
using MooreHotels.Application.DTOs;
using MooreHotels.Application.Interfaces.Services;
using MooreHotels.Domain.Entities;
using MooreHotels.Domain.Enums;

namespace MooreHotels.Application.Services;

public class StaffService : IStaffService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IAuditService _auditService;

    private static readonly string[] AllowedDepartments = { "Housekeeping", "Reception", "FrontDesk", "Concierge" };

    public StaffService(UserManager<ApplicationUser> userManager, IAuditService auditService)
    {
        _userManager = userManager;
        _auditService = auditService;
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
        // This method signature expects to be called within a context where the acting user is known
        // (Handled via the internal logic or by adding actingUserId to the interface)
        throw new NotImplementedException("Use the overload with actingUserId.");
    }

    public async Task OnboardUserAsync(OnboardUserRequest request, Guid actingUserId)
    {
        // 1. Resolve Acting Admin & Enforce RBAC
        var actingUser = await _userManager.FindByIdAsync(actingUserId.ToString());
        if (actingUser == null) throw new UnauthorizedAccessException("Acting user not found.");

        if (actingUser.Role == UserRole.Manager && request.AssignedRole != UserRole.Staff)
        {
            throw new UnauthorizedAccessException("Managerial constraint: Managers are only permitted to onboard 'Staff' roles.");
        }

        if (actingUser.Role != UserRole.Admin && actingUser.Role != UserRole.Manager)
        {
            throw new UnauthorizedAccessException("Insufficient permissions for security provisioning.");
        }

        // 2. Validate Department for Staff
        if (request.AssignedRole == UserRole.Staff && !string.IsNullOrEmpty(request.Department))
        {
            if (!AllowedDepartments.Contains(request.Department))
            {
                throw new Exception($"Invalid department. Allowed values: {string.Join(", ", AllowedDepartments)}");
            }
        }

        // 3. Existence Check
        var existing = await _userManager.FindByEmailAsync(request.Email);
        if (existing != null) throw new Exception("Conflict: A user with this email identity is already registered.");

        // 4. Provision User
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = request.Email,
            Email = request.Email,
            Name = request.FullName,
            Role = request.AssignedRole,
            Status = request.Status,
            Department = request.AssignedRole == UserRole.Staff ? request.Department : null,
            EmailConfirmed = true,
            CreatedAt = DateTime.UtcNow
        };

        var result = await _userManager.CreateAsync(user, request.TemporaryPassword);
        if (!result.Succeeded)
        {
            var errors = string.Join(", ", result.Errors.Select(e => e.Description));
            throw new Exception($"Provisioning protocol failed: {errors}");
        }

        await _userManager.AddToRoleAsync(user, request.AssignedRole.ToString());

        // 5. Audit Logging
        await _auditService.LogActionAsync(
            actingUserId, 
            "USER_ONBOARDED", 
            "User", 
            user.Id.ToString(), 
            null, 
            new { 
                Email = user.Email, 
                Role = user.Role.ToString(), 
                Department = user.Department,
                CreatedBy = actingUser.Name 
            });
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