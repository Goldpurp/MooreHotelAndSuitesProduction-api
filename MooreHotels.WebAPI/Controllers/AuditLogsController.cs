using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MooreHotels.Application.Interfaces.Services;

namespace MooreHotels.WebAPI.Controllers;

[ApiController]
[Route("api/audit-logs")]
[Authorize(Roles = "Admin")]
public class AuditLogsController : ControllerBase
{
    private readonly IAuditService _auditService;
    public AuditLogsController(IAuditService auditService) => _auditService = auditService;

    [HttpGet]
    public async Task<IActionResult> GetLogs() => Ok(await _auditService.GetAllLogsAsync());
}