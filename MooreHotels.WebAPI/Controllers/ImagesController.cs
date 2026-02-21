using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using MooreHotels.Application.Interfaces.Services;
using MooreHotels.Application.DTOs;
using Microsoft.EntityFrameworkCore;

namespace MooreHotels.WebAPI.Controllers;

[ApiController]
[Route("api/images")]
public class ImagesController : ControllerBase
{
    private readonly IImageService _imageService;

    public ImagesController(IImageService imageService)
    {
        _imageService = imageService;
    }

    /// <summary>
    /// Uploads a single image to a specified folder.
    /// Default folder is 'website-assets' for general UI components.
    /// </summary>
    [HttpPost("upload")]
    [ProducesResponseType(typeof(ImageUploadResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Upload(IFormFile file, [FromQuery] string folder = "website-assets")
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { message = "File is empty or not provided." });

        // Ensure we only accept image formats
        var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp", ".avif" };
        var extension = Path.GetExtension(file.FileName).ToLower();

        if (!allowedExtensions.Contains(extension))
            return BadRequest(new { message = "Invalid file type. Only images are allowed." });

        try
        {
            var result = await _imageService.UploadImageAsync(file, folder);

            if (result == null)
                return BadRequest(new { message = "Upload failed at the cloud provider." });

            return Ok(result);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Internal server error during upload.", details = ex.Message });
        }
    }

    /// <summary>
    /// Deletes an image from Cloudinary using its PublicId.
    /// </summary>
 [HttpDelete("delete")]
public async Task<IActionResult> Delete([FromQuery] string publicId, [FromServices] MooreHotels.Infrastructure.Persistence.MooreHotelsDbContext context)
{
    if (string.IsNullOrWhiteSpace(publicId))
        return BadRequest(new { message = "PublicId is required." });

    var cloudDeleted = await _imageService.DeleteImageAsync(publicId);

    var dbImage = await context.RoomImages.FirstOrDefaultAsync(x => x.PublicId == publicId);
    
    if (dbImage != null)
    {
        context.RoomImages.Remove(dbImage);
        await context.SaveChangesAsync();
    }

    if (!cloudDeleted && dbImage == null)
        return NotFound(new { message = "Image not found in Cloud or Database." });

    return Ok(new { message = "Image successfully removed from cloud and database." });
}


}
