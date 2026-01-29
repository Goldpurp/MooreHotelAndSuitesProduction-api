using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MooreHotels.Application.DTOs;
using MooreHotels.Application.Interfaces.Services;
using MooreHotels.Domain.Enums;

namespace MooreHotels.WebAPI.Controllers;

[ApiController]
[Route("api/admin/management")]
[Authorize(Roles = "Admin")]
public class StaffController : ControllerBase
{
    private readonly IStaffService _staffService;

    public StaffController(IStaffService staffService)
    {
        _staffService = staffService;
    }

    [HttpGet("stats")]
    public async Task<IActionResult> GetStats()
    {
        return Ok(await _staffService.GetStaffStatsAsync());
    }

    [HttpGet("employees")]
    public async Task<IActionResult> GetStaffList()
    {
        // Strictly returns company staff members (Manager, Staff, Admin)
        return Ok(await _staffService.GetAllStaffAsync());
    }

    [HttpGet("clients")]
    public async Task<IActionResult> GetGuestUserList()
    {
        // Returns only registered website clients/guests
        var allUsers = await _staffService.GetAllUsersAsync();
        return Ok(allUsers.Where(u => u.Role == UserRole.Client));
    }

    [HttpPost("onboard-staff")]
    public async Task<IActionResult> Onboard([FromBody] OnboardUserRequest request)
    {
        try
        {
            await _staffService.OnboardUserAsync(request);
            return Ok(new { Message = "Staff member successfully provisioned in the system." });
        }
        catch (Exception ex)
        {
            return BadRequest(new { Message = ex.Message });
        }
    }

    [HttpPost("accounts/{id}/activate")]
    public async Task<IActionResult> ActivateUser(Guid id)
    {
        try
        {
            await _staffService.ActivateUserAsync(id);
            return Ok(new { Message = "Account has been activated." });
        }
        catch (Exception ex)
        {
            return BadRequest(new { Message = ex.Message });
        }
    }

    [HttpPost("accounts/{id}/deactivate")]
    public async Task<IActionResult> DeactivateUser(Guid id)
    {
        try
        {
            await _staffService.DeactivateUserAsync(id);
            return Ok(new { Message = "Account has been suspended." });
        }
        catch (Exception ex)
        {
            return BadRequest(new { Message = ex.Message });
        }
    }

    [HttpDelete("accounts/{id}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        try
        {
            await _staffService.DeleteUserAsync(id);
            return NoContent();
        }
        catch (Exception ex)
        {
            return BadRequest(new { Message = ex.Message });
        }
    }
}