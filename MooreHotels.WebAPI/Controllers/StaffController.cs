using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MooreHotels.Application.DTOs;
using MooreHotels.Application.Interfaces.Services;
using MooreHotels.Domain.Enums;
using System.Security.Claims;

namespace MooreHotels.WebAPI.Controllers;

[ApiController]
[Route("api/admin/management")]
public class StaffController : ControllerBase
{
    private readonly IStaffService _staffService;

    public StaffController(IStaffService staffService)
    {
        _staffService = staffService;
    }

    [HttpGet("stats")]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<IActionResult> GetStats()
    {
        return Ok(await _staffService.GetStaffStatsAsync());
    }

    [HttpGet("employees")]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<IActionResult> GetStaffList()
    {
        return Ok(await _staffService.GetAllStaffAsync());
    }

    [HttpGet("clients")]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<IActionResult> GetGuestUserList()
    {
        var allUsers = await _staffService.GetAllUsersAsync();
        return Ok(allUsers.Where(u => u.Role == UserRole.Client));
    }

    [HttpPost("onboard-staff")]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<IActionResult> Onboard([FromBody] OnboardUserRequest request)
    {
        try
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!Guid.TryParse(userIdStr, out var actingUserId))
                return Unauthorized();

            await _staffService.OnboardUserAsync(request, actingUserId);
            return Ok(new { Message = "Staff member successfully provisioned in the system." });
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(403, new { Message = ex.Message });
        }
        catch (Exception ex)
        {
            return BadRequest(new { Message = ex.Message });
        }
    }


    [HttpPatch("accounts/{id}/status")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> ChangeStatus(
        Guid id,
        [FromBody] ChangeStatusRequest request)
    {
        try
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!Guid.TryParse(userIdStr, out var actingUserId))
                return Unauthorized();

            await _staffService.ChangeUserStatusAsync(
                id,
                request.Status,
                actingUserId);

            return Ok(new { Message = "Account status updated successfully." });
        }
        catch (Exception ex)
        {
            return BadRequest(new { Message = ex.Message });
        }
    }


    [HttpDelete("accounts/{id}")]
    [Authorize(Roles = "Admin")]
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