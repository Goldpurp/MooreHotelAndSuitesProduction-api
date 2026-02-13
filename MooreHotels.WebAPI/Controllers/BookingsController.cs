using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MooreHotels.Application.DTOs;
using MooreHotels.Application.Interfaces.Repositories;
using MooreHotels.Application.Interfaces.Services;
using MooreHotels.Domain.Enums;
using System.Security.Claims;

namespace MooreHotels.WebAPI.Controllers;

[ApiController]
[Route("api/bookings")]
public class BookingsController : ControllerBase
{
    private readonly IBookingService _bookingService;
    private readonly IPaymentService _paymentService;
    private readonly IBookingRepository _bookingRepo;

    public BookingsController(IBookingService bookingService, IPaymentService paymentService, IBookingRepository bookingRepo)
    {
        _bookingService = bookingService;
        _paymentService = paymentService;
        _bookingRepo = bookingRepo;
    }

    [HttpGet]
    [Authorize(Roles = "Admin,Manager,Staff")]
    public async Task<IActionResult> GetAllBookings() 
        => Ok(await _bookingService.GetAllBookingsAsync());

    [HttpGet("{code}")]
    [Authorize(Roles = "Admin,Manager,Staff")]
    public async Task<IActionResult> GetBookingByCode(string code)
    {
        var dto = await _bookingService.GetBookingByCodeAsync(code);
        return dto == null ? NotFound() : Ok(dto);
    }

    [HttpGet("lookup")]
    [AllowAnonymous]
    public async Task<IActionResult> LookupBooking([FromQuery] string code, [FromQuery] string email)
    {
        if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(email))
            return BadRequest(new { Message = "Booking code and associated email are required for lookup." });

        var dto = await _bookingService.GetBookingByCodeAndEmailAsync(code, email);
        return dto == null 
            ? NotFound(new { Message = "No booking found with the provided credentials." }) 
            : Ok(dto);
    }

    [HttpPost]
    [AllowAnonymous]
    public async Task<IActionResult> CreateBooking([FromBody] CreateBookingRequest request)
    {
        try 
        {
            var dto = await _bookingService.CreateBookingAsync(request);
            return Ok(dto);
        }
        catch (Exception ex)
        {
            return BadRequest(new { Message = ex.Message });
        }
    }

    [HttpPost("{code}/verify-paystack")]
    [AllowAnonymous]
    public async Task<IActionResult> VerifyPaystack(string code)
    {
        var success = await _paymentService.VerifyPaystackPaymentAsync(code);
        if (success)
        {
            try
            {
                var reference = $"PS-{Guid.NewGuid().ToString("N")[..12].ToUpper()}";
                var dto = await _bookingService.ProcessPaymentSuccessAsync(code, reference);
                return Ok(new { Message = "Payment verified automatically.", Data = dto });
            }
            catch (Exception ex)
            {
                return BadRequest(new { Message = ex.Message });
            }
        }
        return BadRequest(new { Message = "Payment verification failed." });
    }

    [HttpPost("{code}/confirm-transfer")]
    [Authorize(Roles = "Admin,Manager,Staff")]
    public async Task<IActionResult> ConfirmTransfer(string code)
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        Guid userId = Guid.TryParse(userIdStr, out var g) ? g : Guid.Empty;

        try
        {
            var reference = $"TRF-{Guid.NewGuid().ToString("N")[..12].ToUpper()}";
            var dto = await _bookingService.ProcessPaymentSuccessAsync(code, reference, userId);
            return Ok(new { Message = "Bank transfer confirmed manually.", Data = dto });
        }
        catch (Exception ex)
        {
            return BadRequest(new { Message = ex.Message });
        }
    }

    [HttpPut("{id}/status")]
    [Authorize(Roles = "Admin,Manager,Staff")]
    public async Task<IActionResult> UpdateBookingStatus(Guid id, [FromQuery] BookingStatus status)
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        Guid userId = Guid.TryParse(userIdStr, out var g) ? g : Guid.Empty;
        
        try 
        {
            var dto = await _bookingService.UpdateStatusAsync(id, status, userId);
            return Ok(dto);
        }
        catch (Exception ex)
        {
            return BadRequest(new { Message = ex.Message });
        }
    }
    
    [HttpPost("{id}/cancel")]
[Authorize(Roles = "Admin,Manager,Staff")]
public async Task<IActionResult> CancelBookingAdmin(Guid id, [FromQuery] string? reason = null)
{
    // 1. Robust User ID Extraction
    var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
    if (!Guid.TryParse(userIdStr, out var userId))
    {
        return Unauthorized(new { Message = "User identity is invalid or expired." });
    }

    try
    {
        var userRole = User.FindFirstValue(ClaimTypes.Role) ?? "Staff";
        var dto = await _bookingService.CancelBookingAsync(id, userId, reason);

        return Ok(new { 
            Message = $"Booking successfully voided by {userRole}.", 
            Data = dto 
        });
    }
    catch (Exception ex)
    {
        // 2. Consistent Error Response
        return BadRequest(new { Message = ex.Message });
    }
}

[HttpPost("guest/cancel")]
[AllowAnonymous]
public async Task<IActionResult> CancelBookingGuest([FromBody] CancelBookingRequest request)
{
    // 3. Model State Check (Ensures BookingCode and Email aren't null)
    if (!ModelState.IsValid)
    {
        return BadRequest(ModelState);
    }

    try
    {
        var dto = await _bookingService.CancelBookingByGuestAsync(
            request.BookingCode,
            request.Email,
            request.Reason
        );

        return Ok(new { 
            Message = "Your reservation has been cancelled successfully.", 
            Data = dto 
        });
    }
    catch (UnauthorizedAccessException ex)
    {
        // 4. Specific Security Exception Handling
        return Unauthorized(new { Message = ex.Message });
    }
    catch (Exception ex)
    {
        return BadRequest(new { Message = ex.Message });
    }
}

[HttpPost("{id}/complete-refund")]
[Authorize(Roles = "Admin,Manager")]
public async Task<IActionResult> CompleteRefund(Guid id, [FromQuery] string transactionRef)
{
    var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
    if (!Guid.TryParse(userIdStr, out var adminId)) return Unauthorized();

    try
    {
        var dto = await _bookingService.CompleteRefundAsync(id, transactionRef, adminId);
        return Ok(new { Message = "Refund marked as completed in system.", Data = dto });
    }
    catch (Exception ex)
    {
        return BadRequest(new { Message = ex.Message });
    }
}

[HttpGet("pending-refunds")]
[Authorize(Roles = "Admin,Manager")]
public async Task<IActionResult> GetPendingRefunds()
{
    try
    {
        var dtos = await _bookingService.GetPendingRefundsAsync();
        return Ok(dtos);
    }
    catch (Exception ex)
    {
        return BadRequest(new { Message = ex.Message });
    }
}



}