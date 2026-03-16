using Microsoft.AspNetCore.Mvc;
using MooreHotels.Application.Interfaces.Services;
using MooreHotels.Infrastructure.Services;
using System.Text.Json;


namespace MooreHotels.WebAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PaymentsController : ControllerBase
{
    private readonly IPaymentService _paymentService;
    private readonly IBookingService _bookingService;
    private readonly ILogger<PaymentsController> _logger;

    public PaymentsController(
        IPaymentService paymentService, 
        IBookingService bookingService,
        ILogger<PaymentsController> logger)
    {
        _paymentService = paymentService;
        _bookingService = bookingService;
        _logger = logger;
    }

    [HttpGet("verify")]
    public async Task<IActionResult> VerifyPayment([FromQuery] string reference)
    {
        if (string.IsNullOrEmpty(reference)) return BadRequest(new { Message = "Reference required." });

        try
        {
            var isValid = await _paymentService.VerifyPaystackPaymentAsync(reference);
            if (isValid)
            {
                await _bookingService.ProcessPaymentSuccessAsync(reference, reference);
                return Ok(new { Message = "Payment verified successfully." });
            }
            return BadRequest(new { Message = "Payment verification failed or pending." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error verifying payment for reference {Ref}", reference);
            return StatusCode(500, new { Message = "Internal error during verification." });
        }
    }

    [HttpPost("webhook")]
    public async Task<IActionResult> Webhook()
    {
        var secretKey = _paymentService is PaystackService ps ? ps.GetSecretKey() : null;
        
        // Read body early for signature check
        Request.EnableBuffering();
        using var reader = new StreamReader(Request.Body, leaveOpen: true);
        var json = await reader.ReadToEndAsync();
        Request.Body.Position = 0;

        // Verify Signature
        if (!string.IsNullOrEmpty(secretKey))
        {
            var header = Request.Headers["X-Paystack-Signature"].ToString();
            if (string.IsNullOrEmpty(header) || !VerifySignature(json, header, secretKey))
            {
                _logger.LogWarning("Invalid Paystack Signature detected.");
                return BadRequest();
            }
        }
        
        try
        {
            using var doc = JsonDocument.Parse(json);
            var eventType = doc.RootElement.GetProperty("event").GetString();
            
            if (eventType == "charge.success")
            {
                var data = doc.RootElement.GetProperty("data");
                var reference = data.GetProperty("reference").GetString();
                
                if (!string.IsNullOrEmpty(reference))
                {
                    await _bookingService.ProcessPaymentSuccessAsync(reference, reference);
                    _logger.LogInformation("Webhook: Processed success for {Ref}", reference);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Webhook processing failed.");
        }

        return Ok();
    }

    private bool VerifySignature(string body, string signature, string secretKey)
    {
        var keyBytes = System.Text.Encoding.UTF8.GetBytes(secretKey);
        var bodyBytes = System.Text.Encoding.UTF8.GetBytes(body);

        using var hmac = new System.Security.Cryptography.HMACSHA512(keyBytes);
        var hashBytes = hmac.ComputeHash(bodyBytes);
        var hashHex = BitConverter.ToString(hashBytes).Replace("-", "").ToLower();

        return hashHex == signature;
    }
}
