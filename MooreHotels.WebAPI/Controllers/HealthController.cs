using Microsoft.AspNetCore.Mvc;
using MooreHotels.Infrastructure.Persistence;

namespace MooreHotels.WebAPI.Controllers;

[ApiController]
[Route("api/health")]
public class HealthController : ControllerBase
{
    private readonly MooreHotelsDbContext _context;
    public HealthController(MooreHotelsDbContext context) => _context = context;

    [HttpGet]
    public async Task<IActionResult> Check()
    {
        try
        {
            var canConnect = await _context.Database.CanConnectAsync();
            return Ok(new
            {
                Status = "Healthy",
                Timestamp = DateTime.UtcNow,
                Database = canConnect ? "Connected" : "Disconnected",
                Version = "1.0.0-PROD"
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Status = "Unhealthy", Error = ex.Message });
        }
    }
}