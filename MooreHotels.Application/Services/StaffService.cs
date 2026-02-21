using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging; // Added for diagnostic tracking
using MooreHotels.Application.DTOs;
using MooreHotels.Application.Interfaces;
using MooreHotels.Application.Interfaces.Services;
using MooreHotels.Domain.Entities;
using MooreHotels.Domain.Enums;
using System.Text.Json;

namespace MooreHotels.Application.Services;

public class StaffService : IStaffService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IAuditService _auditService;
    private readonly IEmailService _emailService;
    private readonly ILogger<StaffService> _logger;

    private static readonly string[] AllowedDepartments = { "Housekeeping", "Reception", "FrontDesk", "Concierge" };

    public StaffService(
        UserManager<ApplicationUser> userManager,
        IAuditService auditService,
        IEmailService emailService,
        ILogger<StaffService> logger)
    {
        _userManager = userManager;
        _auditService = auditService;
        _emailService = emailService;
        _logger = logger;
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
                u.Department,
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
                u.Department,
                u.CreatedAt,
                u.Status
      ));
        return Task.FromResult(users);
    }

    public Task OnboardUserAsync(OnboardUserRequest request)
    {
        throw new NotImplementedException("Provisioning protocol requires an acting user ID. Use the appropriate overload.");
    }

    public async Task OnboardUserAsync(OnboardUserRequest request, Guid actingUserId)
    {
        var actingUser = await _userManager.FindByIdAsync(actingUserId.ToString());
        if (actingUser == null) throw new UnauthorizedAccessException("Identity Fault: Acting user context not found.");

        // Security Check: Role Hierarchy
        if (actingUser.Role == UserRole.Manager && request.AssignedRole != UserRole.Staff)
            throw new UnauthorizedAccessException("Security Policy: Managers can only onboard 'Staff' roles.");

        if (actingUser.Role != UserRole.Admin && actingUser.Role != UserRole.Manager)
            throw new UnauthorizedAccessException("Permission Denied: Insufficient clearance.");

        // Validation: Departments
        if ((request.AssignedRole == UserRole.Staff || request.AssignedRole == UserRole.Manager) && !string.IsNullOrEmpty(request.Department))
        {
            if (!AllowedDepartments.Contains(request.Department))
                throw new Exception($"Validation Error: '{request.Department}' is not a recognized department.");
        }

        var existing = await _userManager.FindByEmailAsync(request.Email);
        if (existing != null) throw new Exception("Conflict: Email already assigned.");

        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = request.Email,
            Email = request.Email,
            Name = request.FullName,
            PhoneNumber = request.Phone,
            Role = request.AssignedRole,
            Status = request.Status,
            Department = (request.AssignedRole == UserRole.Staff || request.AssignedRole == UserRole.Manager) ? request.Department : null,
            EmailConfirmed = true,
            CreatedAt = DateTime.UtcNow
        };

        var result = await _userManager.CreateAsync(user, request.TemporaryPassword);
        if (!result.Succeeded)
        {
            var errors = string.Join(", ", result.Errors.Select(e => e.Description));
            throw new Exception($"Provisioning Error: {errors}");
        }

        await _userManager.AddToRoleAsync(user, request.AssignedRole.ToString());

        try
        {
            await _emailService.SendStaffWelcomeEmailAsync(user.Email!, user.Name, request.TemporaryPassword, user.Role.ToString());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send onboarding email to {Email}", user.Email);
        }

        await _auditService.LogActionAsync(
            actingUserId, "USER_PROVISIONED", "User", user.Id.ToString(), null, new
            {
                Email = user.Email,
                Role = user.Role.ToString(),
                Department = user.Department,
                Phone = user.PhoneNumber,
                CreatedBy = actingUser.Name,
                Timestamp = user.CreatedAt
            });
    }

    public async Task ChangeUserStatusAsync(
        Guid userId,
        ProfileStatus newStatus,
        Guid actingUserId)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null)
            throw new Exception("Target user profile not found.");

        if (user.Role == UserRole.Admin)
            throw new Exception("Administrators cannot be modified.");

        if (!Enum.IsDefined(typeof(ProfileStatus), newStatus))
            throw new Exception("Invalid status value.");

        var oldStatus = user.Status;

        if (oldStatus != newStatus)
        {
            user.Status = newStatus;

            if (newStatus == ProfileStatus.Suspended)
                await _userManager.UpdateSecurityStampAsync(user);

            var updateResult = await _userManager.UpdateAsync(user);
            if (!updateResult.Succeeded)
                throw new Exception("Failed to update account status.");
        }

        // Background email send to ensure it doesn't block
        if (!string.IsNullOrWhiteSpace(user.Email))
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    if (newStatus == ProfileStatus.Active)
                        await _emailService.SendAccountActivatedAsync(user.Email!, user.Name);
                    else if (newStatus == ProfileStatus.Suspended)
                        await _emailService.SendAccountSuspendedAsync(user.Email!, user.Name);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to send status change email for {Email}", user.Email);
                }
            });
        }

        // Audit log
        await _auditService.LogActionAsync(
            actingUserId,
            "ACCOUNT_STATUS_CHANGED",
            "User",
            user.Id.ToString(),
            new { OldStatus = oldStatus.ToString() },
            new { NewStatus = newStatus.ToString() }
        );
    }

    public async Task DeleteUserAsync(Guid userId)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null) return;
        if (user.Role == UserRole.Admin) throw new Exception("Root Admin accounts are protected.");
        await _userManager.DeleteAsync(user);
    }
}
