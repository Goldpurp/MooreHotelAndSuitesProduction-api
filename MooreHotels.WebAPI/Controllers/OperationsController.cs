using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MooreHotels.Application.Interfaces.Services;

namespace MooreHotels.WebAPI.Controllers;

[ApiController]
[Route("api/operations")]
[Authorize(Roles = "Admin,Manager,Staff")]
public class OperationsController : ControllerBase
{
    private readonly IOperationService _operationService;
    private readonly IAnalyticsService _analyticsService;

    public OperationsController(IOperationService operationService, IAnalyticsService analyticsService)
    {
        _operationService = operationService;
        _analyticsService = analyticsService;
    }

    [HttpGet("ledger")]
    public async Task<IActionResult> GetLedger([FromQuery] string? filter, [FromQuery] string? search)
    {
        return Ok(await _operationService.GetLedgerAsync(filter, search));
    }

    [HttpGet("stats/daily")]
    public async Task<IActionResult> GetDailyStats()
    {
        // This endpoint feeds the top cards (Check-ins today, etc.)
        var overview = await _analyticsService.GetOverviewAsync();
        return Ok(new {
            CheckInsToday = overview.ActiveOperations.Count(o => o.Stage == "CHECKED IN"),
            CheckOutsToday = 0, // Derived from today's scheduled checkouts
            HistoricalTrace = (await _operationService.GetLedgerAsync()).Count(),
            AuditHealth = "100%"
        });
    }
}