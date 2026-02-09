using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MooreHotels.Application.DTOs;
using MooreHotels.Application.Interfaces.Services;
using MooreHotels.Domain.Enums;

namespace MooreHotels.WebAPI.Controllers;

[ApiController]
[Route("api/rooms")]
public class RoomsController : ControllerBase
{
    private readonly IRoomService _roomService;
    public RoomsController(IRoomService roomService) => _roomService = roomService;

    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> GetRooms([FromQuery] RoomCategory? category) 
        => Ok(await _roomService.GetAllRoomsAsync(category));

    [HttpGet("search")]
    [AllowAnonymous]
    public async Task<IActionResult> SearchRooms(
        [FromQuery] DateTime? checkIn, 
        [FromQuery] DateTime? checkOut, 
        [FromQuery] RoomCategory? category,
        [FromQuery] string? roomNumber,
        [FromQuery] string? amenity)
    {
        var request = new RoomSearchRequest(checkIn, checkOut, category, 1, roomNumber, amenity);
        return Ok(await _roomService.SearchRoomsAsync(request));
    }

    [HttpGet("{id}/availability")]
    [AllowAnonymous]
    public async Task<IActionResult> GetAvailability(Guid id, [FromQuery] DateTime checkIn, [FromQuery] DateTime checkOut)
    {
        var result = await _roomService.CheckAvailabilityAsync(id, checkIn, checkOut);
        return Ok(result);
    }

    [HttpGet("{id}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetRoom(Guid id)
    {
        var dto = await _roomService.GetRoomByIdAsync(id);
        return dto == null ? NotFound() : Ok(dto);
    }

    [HttpPost]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<IActionResult> CreateRoom([FromBody] CreateRoomRequest request)
    {
        var dto = await _roomService.CreateRoomAsync(request);
        return CreatedAtAction(nameof(GetRoom), new { id = dto.Id }, dto);
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<IActionResult> UpdateRoom(Guid id, [FromBody] UpdateRoomRequest request)
    {
        await _roomService.UpdateRoomAsync(id, request);
        return NoContent();
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteRoom(Guid id)
    {
        await _roomService.DeleteRoomAsync(id);
        return NoContent();
    }
}