using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using MyImageResult = MooreHotels.Application.DTOs.ImageUploadResult; 
using MooreHotels.Application.Interfaces.Services;
namespace MooreHotels.Infrastructure.Services;

public class CloudinaryService : IImageService
{
    private readonly Cloudinary _cloudinary;

    public CloudinaryService(IOptions<CloudinarySettings> config)
    {
        var settings = config.Value;
        _cloudinary = new Cloudinary(new Account(settings.CloudName, settings.ApiKey, settings.ApiSecret));
    }

    public async Task<MyImageResult?> UploadImageAsync(IFormFile file, string folder = "general")
    {
        if (file == null || file.Length == 0) return null;

        await using var stream = file.OpenReadStream();
        var uploadParams = new ImageUploadParams
        {
            File = new FileDescription(file.FileName, stream),
            Folder = $"MooreHotels/{folder}",
            
            // 1. PRIMARY TRANSFORMATION (Main high-res view)
            // f_auto: best format (WebP/AVIF), q_auto: smart compression
            Transformation = new Transformation()
                .Width(1200).Height(800).Crop("limit")
                .Quality("auto").FetchFormat("auto"),

            // 2. EAGER TRANSFORMATIONS (Generated instantly in the background)
            EagerTransforms = new List<Transformation>
            {
                // Dashboard Thumbnail: 300x300 square crop, AI-centered on the subject
                new Transformation().Width(300).Height(300).Crop("fill").Gravity("auto").Quality("auto"),
                
                // Mobile Version: Optimized for smaller screens
                new Transformation().Width(640).Crop("scale").Quality("auto")
            },
            
            // 3. PERFORMANCE: Don't wait for thumbnails to finish to return the main URL
            EagerAsync = true 
        };

        var result = await _cloudinary.UploadAsync(uploadParams);

        if (result.Error != null) 
            throw new Exception($"Cloudinary Error: {result.Error.Message}");

        return new MyImageResult(result.PublicId, result.SecureUrl.ToString());
    }

    public async Task<List<MyImageResult>> UploadMultipleAsync(List<IFormFile> files, string folder = "rooms")
    {
        if (files == null || !files.Any()) return new List<MyImageResult>();

        // Parallel processing: Upload all images simultaneously
        var tasks = files.Select(file => UploadImageAsync(file, folder));
        var results = await Task.WhenAll(tasks);
        
        return results.Where(r => r != null).ToList()!;
    }

    public async Task<bool> DeleteImageAsync(string publicId)
{
    if (string.IsNullOrEmpty(publicId)) return false;
    
    var deleteParams = new DeletionParams(publicId)
    {
        Invalidate = true 
    };
    
    var result = await _cloudinary.DestroyAsync(deleteParams);
    return result.Result == "ok";
}

}
