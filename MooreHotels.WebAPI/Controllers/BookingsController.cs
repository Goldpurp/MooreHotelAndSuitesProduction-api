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

    public BookingsController(IBookingService bookingService, IPaymentService paymentService)
    {
        _bookingService = bookingService;
        _paymentService = paymentService;
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
        var dto = await _bookingService.GetBookingByCodeAndEmailAsync(code, email);
        return dto == null 
            ? NotFound(new { Message = "No booking found with the provided credentials." }) 
            : Ok(dto);
    }

    [HttpPost]
    [AllowAnonymous]
    public async Task<IActionResult> CreateBooking([FromBody] CreateBookingRequest request)
    {
        var dto = await _bookingService.CreateBookingAsync(request);
        return Ok(dto);
    }

    [HttpPost("{code}/verify-paystack")]
    [AllowAnonymous]
    public async Task<IActionResult> VerifyPaystack(string code)
    {
        var success = await _paymentService.VerifyPaystackPaymentAsync(code);
        if (success)
        {
            var reference = $"PS-{Guid.NewGuid().ToString("N")[..12].ToUpper()}";
            var dto = await _bookingService.ProcessPaymentSuccessAsync(code, reference);
            return Ok(new { Message = "Payment verified successfully.", Data = dto });
        }
        return BadRequest(new { Message = "Payment verification failed." });
    }

    [HttpPost("{code}/confirm-transfer")]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<IActionResult> ConfirmTransfer(string code)
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        Guid userId = Guid.TryParse(userIdStr, out var g) ? g : Guid.Empty;

        var reference = $"TRF-{Guid.NewGuid().ToString("N")[..12].ToUpper()}";
        var dto = await _bookingService.ProcessPaymentSuccessAsync(code, reference, userId);
        return Ok(new { Message = "Bank transfer confirmed manually.", Data = dto });
    }

    [HttpPut("{id}/status")]
    [Authorize(Roles = "Admin,Manager,Staff")]
    public async Task<IActionResult> UpdateBookingStatus(Guid id, [FromQuery] BookingStatus status)
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        Guid userId = Guid.TryParse(userIdStr, out var g) ? g : Guid.Empty;
        
        var dto = await _bookingService.UpdateStatusAsync(id, status, userId);
        return Ok(dto);
    }
}