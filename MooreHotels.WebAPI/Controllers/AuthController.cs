using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using MooreHotels.Application.DTOs;
using MooreHotels.Application.Interfaces.Repositories;
using MooreHotels.Application.Interfaces.Services;
using MooreHotels.Domain.Entities;
using MooreHotels.Domain.Enums;
using MooreHotels.Infrastructure.Identity;

namespace MooreHotels.WebAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IJwtService _jwtService;
    private readonly IGuestRepository _guestRepo;
    private readonly IProfileService _profileService;

    public AuthController(
        UserManager<ApplicationUser> userManager, 
        IJwtService jwtService, 
        IGuestRepository guestRepo,
        IProfileService profileService)
    {
        _userManager = userManager;
        _jwtService = jwtService;
        _guestRepo = guestRepo;
        _profileService = profileService;
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user == null || !await _userManager.CheckPasswordAsync(user, request.Password))
            return Unauthorized(new { Message = "Invalid email or password." });

        if (user.Status == ProfileStatus.Suspended)
            return StatusCode(403, new { Message = "Access denied. Your account has been suspended by an administrator." });

        var token = _jwtService.GenerateToken(user);
        return Ok(new AuthResponse(token, user.Email!, user.Name, user.Role.ToString()));
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        var existing = await _userManager.FindByEmailAsync(request.Email);
        if (existing != null) return BadRequest(new { Message = "This email address is already registered." });

        var assignedRole = UserRole.Client;
        
        var fullName = $"{request.FirstName} {request.LastName}";
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            Email = request.Email,
            UserName = request.Email,
            Name = fullName,
            Role = assignedRole,
            Status = ProfileStatus.Active,
            PhoneNumber = request.Phone,
            CreatedAt = DateTime.UtcNow,
            EmailConfirmed = true 
        };

        var result = await _userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded) return BadRequest(new { Errors = result.Errors.Select(e => e.Description) });

        await _userManager.AddToRoleAsync(user, assignedRole.ToString());

        var guest = new Guest
        {
            Id = $"GS-{new Random().Next(1000, 9999)}",
            FirstName = request.FirstName,
            LastName = request.LastName,
            Email = request.Email,
            Phone = request.Phone,
            CreatedAt = DateTime.UtcNow
        };
        await _guestRepo.AddAsync(guest);

        var token = _jwtService.GenerateToken(user);
        return Ok(new AuthResponse(token, user.Email!, user.Name, user.Role.ToString()));
    }

    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request)
    {
        try
        {
            await _profileService.ForgotPasswordAsync(request.Email);
            return Ok(new { Message = "If an account is associated with this email, a temporary password has been dispatched." });
        }
        catch (Exception ex)
        {
            return BadRequest(new { Message = ex.Message });
        }
    }
}