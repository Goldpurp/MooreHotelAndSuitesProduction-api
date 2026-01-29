using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MooreHotels.Application.Interfaces.Services;
using System.Security.Claims;

namespace MooreHotels.WebAPI.Controllers;

[ApiController]
[Route("api/visit-records")]
[Authorize]
public class VisitRecordsController : ControllerBase
{
    private readonly IVisitRecordService _visitService;
    public VisitRecordsController(IVisitRecordService visitService) => _visitService = visitService;

    [HttpGet]
    public async Task<IActionResult> GetRecords() => Ok(await _visitService.GetAllRecordsAsync());

    [HttpPost]
    public async Task<IActionResult> LogVisit([FromQuery] string code, [FromQuery] string action)
    {
        // Fixed: Use the 'name' claim for a readable authorized name
        var author = User.FindFirstValue("name") ?? User.Identity?.Name ?? "System";
        await _visitService.CreateRecordAsync(code, action, author);
        return Ok();
    }
}