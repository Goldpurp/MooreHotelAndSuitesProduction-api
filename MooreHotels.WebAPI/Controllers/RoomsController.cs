using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MooreHotels.Application.DTOs;
using MooreHotels.Application.Interfaces.Services;
using MooreHotels.Domain.Entities;
using MooreHotels.Domain.Enums;
using MooreHotels.Infrastructure.Persistence;

namespace MooreHotels.WebAPI.Controllers;

[ApiController]
[Route("api/rooms")]
public class RoomsController : ControllerBase
{
    private readonly IRoomService _roomService;
    private readonly IImageService _imageService;
    private readonly MooreHotelsDbContext _context;
    private readonly ILogger<RoomsController> _logger;

    public RoomsController(
        IRoomService roomService,
        IImageService imageService,
        MooreHotelsDbContext context,
        ILogger<RoomsController> logger)
    {
        _roomService = roomService;
        _imageService = imageService;
        _context = context;
        _logger = logger;
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<IEnumerable<RoomDto>>> GetRooms([FromQuery] RoomCategory? category)
    {
        var rooms = await _roomService.GetAllRoomsAsync(category);
        return Ok(rooms);
    }

    [HttpGet("search")]
    [AllowAnonymous]
    public async Task<ActionResult<IEnumerable<RoomDto>>> SearchRooms(
        [FromQuery] DateTime? checkIn,
        [FromQuery] DateTime? checkOut,
        [FromQuery] RoomCategory? category,
        [FromQuery] int guest = 1,
        [FromQuery] string? roomNumber = null,
        [FromQuery] string? amenity = null)
    {
        if (guest <= 0) return BadRequest("Guest count must be greater than zero.");

        if (checkIn.HasValue && checkOut.HasValue)
        {
            if (checkIn >= checkOut) return BadRequest("Check-out must be after check-in.");
            if (checkIn < DateTime.UtcNow.Date) return BadRequest("Check-in cannot be in the past.");
        }

        var request = new RoomSearchRequest(checkIn, checkOut, category, guest, roomNumber, amenity);
        var result = await _roomService.SearchRoomsAsync(request);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    [AllowAnonymous]
    public async Task<ActionResult<RoomDto>> GetRoom(Guid id)
    {
        var room = await _roomService.GetRoomByIdAsync(id);
        return room == null ? NotFound(new { message = "Room not found." }) : Ok(room);
    }

    [HttpGet("{id:guid}/availability")]
    [AllowAnonymous]
    public async Task<IActionResult> GetAvailability(Guid id, [FromQuery] DateTime checkIn, [FromQuery] DateTime checkOut)
    {
        if (checkIn >= checkOut) return BadRequest("Check-out must be after check-in.");
        var exists = await _roomService.GetRoomByIdAsync(id);
        if (exists == null) return NotFound(new { message = "Room not found." });

        var result = await _roomService.CheckAvailabilityAsync(id, checkIn, checkOut);
        return Ok(result);
    }


    [HttpPost]
    [Authorize(Roles = "Admin,Manager")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> CreateRoom([FromForm] CreateRoomRequest request, List<IFormFile> files)
    {
        var strategy = _context.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async () =>
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // 1. Stage the Room (returns a DTO)
                var roomDto = await _roomService.CreateRoomAsync(request);

                // 2. Upload images
                var uploadResults = await _imageService.UploadMultipleAsync(files, "rooms");

                // 3. FORCE PERSISTENCE: Manually add images to the context here
                // This bypasses the service tracking issues entirely
                foreach (var result in uploadResults)
                {
                    _context.RoomImages.Add(new RoomImage
                    {
                        Id = Guid.NewGuid(),
                        RoomId = roomDto.Id, // Link directly to the new room ID
                        Url = result.Url,
                        PublicId = result.PublicId,
                        CreatedAt = DateTime.UtcNow
                    });
                }

                // 4. THE ONLY SAVE CALL
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                // 5. Build response
                var imageUrls = uploadResults.Select(u => u.Url).ToList();
                var finalResponse = roomDto with { Images = imageUrls };

                return CreatedAtAction(nameof(GetRoom), new { id = roomDto.Id }, finalResponse);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "An error occurred while creating the room with ID: {RoomId}", request.RoomNumber); 
                throw;
            }
        });
    }




    [HttpPost("{id:guid}/images")]
    [Authorize(Roles = "Admin,Manager")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> AddImages(Guid id, List<IFormFile> files)
    {
        if (files == null || !files.Any()) return BadRequest("No files uploaded.");

        var strategy = _context.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync<IActionResult>(async () =>
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var room = await _roomService.GetRoomByIdAsync(id);
                if (room == null) return NotFound(new { message = "Room not found." });

                var uploadResults = await _imageService.UploadMultipleAsync(files, "rooms");

                // Manual Add to ensure persistence
                foreach (var result in uploadResults)
                {
                    _context.RoomImages.Add(new RoomImage
                    {
                        Id = Guid.NewGuid(),
                        RoomId = id,
                        Url = result.Url,
                        PublicId = result.PublicId,
                        CreatedAt = DateTime.UtcNow
                    });
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return Ok(new { message = "Images added successfully." });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return StatusCode(500, new { message = "Failed to add images.", details = ex.Message });
            }
        });
    }



    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<IActionResult> UpdateRoom(Guid id, [FromBody] UpdateRoomRequest request)
    {
        var strategy = _context.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync<IActionResult>(async () =>
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                await _roomService.UpdateRoomAsync(id, request);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
                return NoContent();
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return StatusCode(500, new { message = "Update failed", details = ex.Message });
            }
        });
    }



    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Admin, Manager")]
    public async Task<IActionResult> DeleteRoom(Guid id)
    {
        var strategy = _context.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync<IActionResult>(async () =>
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // 1. Fetch images associated with the room first
                var roomImages = await _context.RoomImages
                    .Where(img => img.RoomId == id)
                    .ToListAsync();

                var publicIds = roomImages.Select(img => img.PublicId).ToList();

                // 2. Delete image records from the Database
                if (roomImages.Any())
                {
                    _context.RoomImages.RemoveRange(roomImages);
                }

                // 3. Delete the Room itself
                // If your service only deletes the Room entity, the RemoveRange above 
                // prevents Foreign Key constraint errors.
                var deleted = await _roomService.DeleteRoomAsync(id);
                if (deleted == null)
                    return NotFound(new { message = "Room not found." });

                // 4. Commit DB changes
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                // 5. Cleanup Cloudinary (Perform after DB success)
                foreach (var publicId in publicIds)
                {
                    // This uses your IImageService which now handles the 'MooreHotels/' prefix
                    await _imageService.DeleteImageAsync(publicId);
                }

                return Ok(new { message = "Room and all associated images deleted successfully." });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return StatusCode(500, new { message = "Delete failed", details = ex.Message });
            }
        });
    }




}
