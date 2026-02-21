using Microsoft.AspNetCore.Http;
using MooreHotels.Application.DTOs;

namespace MooreHotels.Application.Interfaces.Services;

public interface IImageService
{
    /// <summary>
    /// Uploads a single image with optional folder categorization.
    /// Returns both PublicId and SecureUrl.
    /// </summary>
    Task<ImageUploadResult?> UploadImageAsync(IFormFile file, string folder = "general");

    /// <summary>
    /// Uploads multiple images in parallel. 
    /// Ideal for Room Galleries and multi-photo components.
    /// </summary>
    Task<List<ImageUploadResult>> UploadMultipleAsync(List<IFormFile> files, string folder = "rooms");

    /// <summary>
    /// Deletes an image from the cloud provider using its PublicId.
    /// Returns true if successfully removed.
    /// </summary>
    Task<bool> DeleteImageAsync(string publicId);
}
