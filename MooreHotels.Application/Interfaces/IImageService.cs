using Microsoft.AspNetCore.Http;

public interface IImageService
{
    Task<string> UploadImageAsync(IFormFile file);
}
