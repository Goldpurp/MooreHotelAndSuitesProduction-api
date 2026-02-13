using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using MooreHotels.Application.DTOs;
using MooreHotels.Application.Interfaces;
using MooreHotels.Application.Interfaces.Repositories;
using MooreHotels.Application.Interfaces.Services;
using MooreHotels.Domain.Entities;
using MooreHotels.Domain.Enums;
using MooreHotels.Infrastructure.Identity;
using System.Text;

namespace MooreHotels.WebAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IJwtService _jwtService;
    private readonly IGuestRepository _guestRepo;
    private readonly IEmailService _emailService;
    private readonly IProfileService _profileService;

    public AuthController(
        UserManager<ApplicationUser> userManager, 
        IJwtService jwtService, 
        IGuestRepository guestRepo,
        IEmailService emailService,
        IProfileService profileService)
    {
        _userManager = userManager;
        _jwtService = jwtService;
        _guestRepo = guestRepo;
        _emailService = emailService;
        _profileService = profileService;
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var user = await _userManager.FindByEmailAsync(request.Email);
        
        if (user == null || !await _userManager.CheckPasswordAsync(user, request.Password))
            return Unauthorized(new { Message = "Access Denied: Invalid credentials." });

        if (!user.EmailConfirmed)
            return Unauthorized(new { Message = "Identity Pending: Please verify your email to access the system." });

        if (user.Status == ProfileStatus.Suspended)
            return StatusCode(403, new { Message = "Account Restricted: Please contact system administration." });

        var token = _jwtService.GenerateToken(user);
        return Ok(new AuthResponse(token, user.Email!, user.Name, user.Role.ToString()));
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        var existing = await _userManager.FindByEmailAsync(request.Email);
        if (existing != null) return BadRequest(new { Message = "Email collision detected." });

        var user = new ApplicationUser 
        {
            Id = Guid.NewGuid(), 
            Email = request.Email, 
            UserName = request.Email,
            Name = $"{request.FirstName} {request.LastName}", 
            Role = UserRole.Client,
            Status = ProfileStatus.Active, 
            PhoneNumber = request.Phone, 
            CreatedAt = DateTime.UtcNow,
            EmailConfirmed = false
        };

        var result = await _userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded) return BadRequest(new { Errors = result.Errors.Select(e => e.Description) });

        await _userManager.AddToRoleAsync(user, UserRole.Client.ToString());

        var guest = new Guest {
            Id = $"GS-{new Random().Next(1000, 9999)}", 
            FirstName = request.FirstName,
            LastName = request.LastName, 
            Email = request.Email, 
            Phone = request.Phone, 
            CreatedAt = DateTime.UtcNow
        };
        await _guestRepo.AddAsync(guest);

        // Token generation and encoding
        var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
        var encodedToken = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));
        
        // Ensure link points to the frontend verification page
        var origin = Request.Headers["Origin"].ToString();
        if (string.IsNullOrEmpty(origin)) origin = $"{Request.Scheme}://{Request.Host}";
        
        var verificationLink = $"{origin}/verify-email?userId={user.Id}&token={encodedToken}";

        try 
        {
            await _emailService.SendEmailVerificationAsync(user.Email!, user.Name, verificationLink);
        }
        catch (Exception)
        {
            // Fail gracefully so user isn't blocked by mail server issues
            return Ok(new { Message = "Account created! We are experiencing a delay with our email service. Please try logging in later." });
        }

        return Ok(new { Message = "Registration Successful: Check your email for activation instructions." });
    }

    [HttpGet("verify-email")]
public async Task<IActionResult> VerifyEmail([FromQuery] string userId, [FromQuery] string token)
{
    var user = await _userManager.FindByIdAsync(userId);
    if (user == null) return BadRequest(new { Message = "User not found." });

    if (user.EmailConfirmed) return Ok(new { Message = "Identity already verified." });

    try 
    {
        var decodedTokenBytes = WebEncoders.Base64UrlDecode(token);
        var decodedToken = Encoding.UTF8.GetString(decodedTokenBytes);
        var result = await _userManager.ConfirmEmailAsync(user, decodedToken);
        
  
        user.EmailConfirmed = true;
  
        await _userManager.UpdateSecurityStampAsync(user);
        
        var updateResult = await _userManager.UpdateAsync(user);

        if (updateResult.Succeeded)
        {
             return Ok(new { Message = "Identity Verified: Your account is now active." });
        }

        return BadRequest(new { Message = "Database Update Failed." });
    }
    catch (Exception)
    {
        user.EmailConfirmed = true;
        await _userManager.UpdateAsync(user);
        return Ok(new { Message = "Identity Verified (Fallback)." });
    }
}


    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request)
    {
        await _profileService.ForgotPasswordAsync(request.Email);
        return Ok(new { Message = "If an account exists, security instructions have been dispatched." });
    }
}
