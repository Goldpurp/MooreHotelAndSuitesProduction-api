// using CloudinaryDotNet;
// using CloudinaryDotNet.Actions;
using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

public class CloudinaryService : IImageService
{
    private readonly Cloudinary _cloudinary;

public CloudinaryService(IOptions<CloudinarySettings> config)
{
    var settings = config.Value;

    if (string.IsNullOrWhiteSpace(settings.CloudName) ||
        string.IsNullOrWhiteSpace(settings.ApiKey) ||
        string.IsNullOrWhiteSpace(settings.ApiSecret))
    {
        throw new ArgumentException("Cloudinary settings must be provided (CloudName, ApiKey, ApiSecret).");
    }

    _cloudinary = new Cloudinary(new Account(
        settings.CloudName,
        settings.ApiKey,
        settings.ApiSecret));
}


    public async Task<string> UploadImageAsync(IFormFile file)
    {
        await using var stream = file.OpenReadStream();

        var uploadParams = new ImageUploadParams
        {
            File = new FileDescription(file.FileName, stream)
        };

        var result = await _cloudinary.UploadAsync(uploadParams);

        return result.SecureUrl.ToString();
    }
}
