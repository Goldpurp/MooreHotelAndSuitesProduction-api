using Microsoft.AspNetCore.Identity;
using MooreHotels.Application.DTOs;
using MooreHotels.Application.Interfaces;
using MooreHotels.Application.Interfaces.Repositories;
using MooreHotels.Application.Interfaces.Services;
using MooreHotels.Domain.Entities;
using MooreHotels.Domain.Enums;
using System.Text;

namespace MooreHotels.Application.Services;

public class ProfileService : IProfileService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IAuditService _auditService;
    private readonly IBookingRepository _bookingRepo;
    private readonly IGuestRepository _guestRepo;
    private readonly IEmailService _emailService;

    public ProfileService(
        UserManager<ApplicationUser> userManager, 
        IAuditService auditService, 
        IBookingRepository bookingRepo,
        IGuestRepository guestRepo,
        IEmailService emailService)
    {
        _userManager = userManager;
        _auditService = auditService;
        _bookingRepo = bookingRepo;
        _guestRepo = guestRepo;
        _emailService = emailService;
    }

    public async Task<UserProfileDto> GetProfileAsync(Guid userId)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null) throw new Exception("User not found");

        var guest = await _guestRepo.GetByEmailAsync(user.Email!);

        return new UserProfileDto(
            user.Id,
            user.Name,
            user.Email!,
            user.Role.ToString(),
            user.Status.ToString(),
            user.AvatarUrl,
            user.EmailConfirmed,
            user.CreatedAt,
            guest?.Id
        );
    }

    public async Task UpdateProfileAsync(Guid userId, UpdateProfileRequest request)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null) throw new Exception("User account not found.");

        var oldEmail = user.Email;
        bool isChanged = false;

        if (!string.IsNullOrWhiteSpace(request.FullName))
        {
            user.Name = request.FullName;
            isChanged = true;
        }

        if (!string.IsNullOrWhiteSpace(request.Email) && request.Email != user.Email)
        {
            var existing = await _userManager.FindByEmailAsync(request.Email);
            if (existing != null && existing.Id != userId)
                throw new Exception("Conflict: The requested email address is already associated with another account.");

            user.Email = request.Email;
            user.UserName = request.Email;
            isChanged = true;
        }

        if (request.Phone != null)
        {
            user.PhoneNumber = request.Phone;
            isChanged = true;
        }

        if (request.AvatarUrl != null)
        {
            user.AvatarUrl = request.AvatarUrl;
            isChanged = true;
        }

        if (isChanged)
        {
            var result = await _userManager.UpdateAsync(user);
            if (!result.Succeeded) 
                throw new Exception($"Identity Update Failed: {string.Join(", ", result.Errors.Select(e => e.Description))}");

            var guest = await _guestRepo.GetByEmailAsync(oldEmail!);
            if (guest != null)
            {
                if (!string.IsNullOrWhiteSpace(request.FullName))
                {
                    var names = request.FullName.Split(' ', 2);
                    guest.FirstName = names[0];
                    guest.LastName = names.Length > 1 ? names[1] : "";
                }
                
                if (!string.IsNullOrWhiteSpace(request.Email)) guest.Email = request.Email;
                if (request.Phone != null) guest.Phone = request.Phone;
                if (request.AvatarUrl != null) guest.AvatarUrl = request.AvatarUrl;
                
                await _guestRepo.UpdateAsync(guest);
            }

            await _auditService.LogActionAsync(userId, "PARTIAL_PROFILE_UPDATE", "User", userId.ToString(), new { UpdatedFields = request });
        }
    }

    public async Task<IEnumerable<BookingDto>> GetBookingHistoryAsync(Guid userId)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null) return Enumerable.Empty<BookingDto>();

        var all = await _bookingRepo.GetAllAsync();
        return all
            .Where(b => b.Guest?.Email == user.Email)
            .OrderByDescending(b => b.CheckIn)
            .Select(b => new BookingDto(
                b.Id, b.BookingCode, b.RoomId, b.GuestId, 
                b.Guest?.FirstName ?? "", b.Guest?.LastName ?? "", b.Guest?.Email ?? "", b.Guest?.Phone ?? "",
                b.CheckIn, b.CheckOut,
                b.Status, b.Amount, b.PaymentStatus, b.PaymentMethod, b.TransactionReference, b.Notes, b.CreatedAt, null, null));
    }

    public async Task RotateCredentialsAsync(Guid userId, RotateCredentialsRequest request)
    {
        if (request.NewPassword != request.ConfirmNewPassword)
            throw new Exception("Security Failure: The new password and confirmation password do not match.");

        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null) throw new Exception("User not found");

        var result = await _userManager.ChangePasswordAsync(user, request.OldPassword, request.NewPassword);
        if (!result.Succeeded) 
            throw new Exception($"Identity Update Failed: {string.Join(", ", result.Errors.Select(e => e.Description))}");

        await _auditService.LogActionAsync(userId, "ROTATE_CREDENTIALS", "User", userId.ToString(), 
            new { Message = "Security credentials updated via rotation protocol." });
    }

    public async Task ForgotPasswordAsync(string email)
    {
        var user = await _userManager.FindByEmailAsync(email);
        if (user == null) 
            return; // Silent return for security to prevent email enumeration

        // 1. Generate a secure random temporary password
        var tempPassword = GenerateRandomPassword(10);
        
        // 2. Generate reset token and apply the new password
        var token = await _userManager.GeneratePasswordResetTokenAsync(user);
        var result = await _userManager.ResetPasswordAsync(user, token, tempPassword);

        if (!result.Succeeded)
            throw new Exception("Internal Security Protocol Error: Could not reset password.");

        // 3. Dispatch communication
        await _emailService.SendTemporaryPasswordAsync(user.Email!, user.Name, tempPassword);

        await _auditService.LogActionAsync(user.Id, "FORGOT_PASSWORD_TRIGGERED", "User", user.Id.ToString(), 
            new { Message = "Temporary password generated and dispatched." });
    }

    public async Task DeactivateAccountAsync(Guid userId)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null) throw new Exception("User not found");

        if (user.Role == UserRole.Admin)
            throw new Exception("Security Constraint: System Administrator account cannot be deactivated.");

        user.Status = ProfileStatus.Suspended;
        await _userManager.UpdateAsync(user);

        await _auditService.LogActionAsync(userId, "DEACTIVATE_ACCOUNT", "User", userId.ToString(), 
            new { Status = "Suspended" });
    }

    public async Task ActivateAccountAsync(Guid userId)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null) throw new Exception("User not found");

        user.Status = ProfileStatus.Active;
        await _userManager.UpdateAsync(user);

        await _auditService.LogActionAsync(userId, "ACTIVATE_ACCOUNT", "User", userId.ToString(), 
            new { Status = "Active" });
    }

    private string GenerateRandomPassword(int length)
    {
        const string validChars = "ABCDEFGHJKLMNOPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz0123456789!@#$%^&*";
        var res = new StringBuilder();
        var rnd = new Random();
        while (0 < length--)
        {
            res.Append(validChars[rnd.Next(validChars.Length)]);
        }
        return res.ToString();
    }
}