using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MooreHotels.Application.Interfaces.Services;
using MooreHotels.Domain.Common;
using MooreHotels.Domain.Entities;
using MooreHotels.Infrastructure.Persistence;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace MooreHotels.WebAPI.Controllers;

[ApiController]
[Route("api/payments")]
public class MonnifyWebhookController : ControllerBase
{
    private readonly IMonnifyService _monnifyService;
    private readonly IBookingService _bookingService;
    private readonly MooreHotelsDbContext _dbContext;
    private readonly MonnifySettings _settings;
    private readonly ILogger<MonnifyWebhookController> _logger;

    public MonnifyWebhookController(
        IMonnifyService monnifyService,
        IBookingService bookingService,
        MooreHotelsDbContext dbContext,
        IOptions<MonnifySettings> settings,
        ILogger<MonnifyWebhookController> logger)
    {
        _monnifyService = monnifyService;
        _bookingService = bookingService;
        _dbContext = dbContext;
        _settings = settings.Value;
        _logger = logger;
    }

    [HttpPost("monnify-webhook")]
    public async Task<IActionResult> HandleWebhook()
    {
        // 1. Read Raw Body for Signature Verification
        Request.EnableBuffering();
        using var reader = new StreamReader(Request.Body, Encoding.UTF8, true, 1024, true);
        var requestBody = await reader.ReadToEndAsync();
        Request.Body.Position = 0;

        // 2. Validate Signature
        var signature = Request.Headers["x-monnify-signature"].ToString();
        if (string.IsNullOrEmpty(signature) || !VerifySignature(requestBody, signature))
        {
            _logger.LogWarning("Invalid Monnify signature detected.");
            return Unauthorized();
        }

        // 3. Process Payload
        try
        {
            using var doc = JsonDocument.Parse(requestBody);
            var eventData = doc.RootElement.GetProperty("eventData");
            var paymentReference = eventData.GetProperty("paymentReference").GetString();
            var bookingCode = eventData.GetProperty("metaData").GetProperty("bookingCode").GetString();
            var status = eventData.GetProperty("paymentStatus").GetString();

            if (string.IsNullOrEmpty(paymentReference) || string.IsNullOrEmpty(bookingCode))
            {
                return BadRequest("Missing required fields (bookingCode or paymentReference).");
            }

            // 4. Idempotency Check
            var existingTx = await _dbContext.MonnifyTransactions.FirstOrDefaultAsync(x => x.TransactionReference == paymentReference);
            if (existingTx != null && existingTx.Status == "PAID")
            {
                return Ok(); // Already processed
            }

            // 5. Server-Side Re-verification (Zero Trust)
            var isVerified = await _monnifyService.VerifyTransactionAsync(paymentReference);
            if (!isVerified)
            {
                _logger.LogWarning("Monnify transaction {Ref} failed re-verification.", paymentReference);
                return BadRequest("Verification failed.");
            }

            // 6. Update Database
            if (existingTx == null)
            {
                existingTx = new MonnifyTransaction
                {
                    Id = Guid.NewGuid(),
                    BookingCode = bookingCode,
                    TransactionReference = paymentReference,
                    Amount = eventData.GetProperty("amountPaid").GetDecimal(),
                    Fee = eventData.GetProperty("paymentFee").GetDecimal(),
                    SettledAmount = eventData.GetProperty("settleAmount").GetDecimal(),
                    Status = "PAID",
                    CustomerEmail = eventData.GetProperty("customer").GetProperty("email").GetString(),
                    CustomerName = eventData.GetProperty("customer").GetProperty("name").GetString(),
                    RawPayloadJson = requestBody,
                    CreatedAt = DateTime.UtcNow
                };
                _dbContext.MonnifyTransactions.Add(existingTx);
            }
            else
            {
                existingTx.Status = "PAID";
                existingTx.UpdatedAt = DateTime.UtcNow;
            }

            // 7. Update Booking Status via Service
            await _bookingService.ProcessPaymentSuccessAsync(bookingCode, paymentReference);

            await _dbContext.SaveChangesAsync();
            _logger.LogInformation("Successfully processed Monnify payment for Booking {Code}", bookingCode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing Monnify webhook.");
            return StatusCode(500); // Monnify will retry
        }

        return Ok();
    }

    private bool VerifySignature(string body, string signature)
    {
        var keyBytes = Encoding.UTF8.GetBytes(_settings.SecretKey);
        var bodyBytes = Encoding.UTF8.GetBytes(body);

        using var hmac = new HMACSHA512(keyBytes);
        var hashBytes = hmac.ComputeHash(bodyBytes);
        var hashHex = BitConverter.ToString(hashBytes).Replace("-", "").ToLower();

        return hashHex.Equals(signature, StringComparison.OrdinalIgnoreCase);
    }
}
