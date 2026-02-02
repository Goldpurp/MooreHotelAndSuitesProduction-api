using Microsoft.AspNetCore.Http;
using MooreHotels.Application.Interfaces.Services;
using MooreHotels.Domain.Enums;

namespace MooreHotels.Infrastructure.Services;

public class MockPaymentService : IPaymentService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public MockPaymentService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public string GeneratePaystackLink(string bookingCode, decimal amount, string email)
    {
        var request = _httpContextAccessor.HttpContext?.Request;
        
        // Use the production API address provided: https://api.moorehotelandsuites.com
        // If the request comes in via that host, we use it. Otherwise, we fallback to the request host.
        var apiBaseUrl = request != null 
            ? $"{request.Scheme}://{request.Host}" 
            : "https://api.moorehotelandsuites.com";

        // Detect the frontend origin from the Referer header to return the user to the correct UI
        var referrer = request?.Headers["Referer"].ToString();
        var clientOrigin = "https://moorehotelandsuites.com"; // Default production fallback
        
        if (!string.IsNullOrEmpty(referrer) && Uri.TryCreate(referrer, UriKind.Absolute, out var referrerUri))
        {
            clientOrigin = $"{referrerUri.Scheme}://{referrerUri.Host}";
            if (!referrerUri.IsDefaultPort)
            {
                clientOrigin += $":{referrerUri.Port}";
            }
        }

        return $"{apiBaseUrl}/api/payment-simulator?code={bookingCode}&amount={amount}&email={email}&redirectUrl={Uri.EscapeDataString(clientOrigin)}";
    }

    public async Task<bool> VerifyPaystackPaymentAsync(string reference)
    {
        // Mocking external verification delay
        await Task.Delay(800); 
        return !string.IsNullOrEmpty(reference);
    }

    public string GetTransferInstructions()
    {
        return "Please transfer the total amount to:\n" +
               "Bank: Moore International Bank\n" +
               "Account Name: Moore Hotel and Suites Ltd\n" +
               "Account Number: 0078649036\n" +
               "Ref: [Your Booking Code]";
    }
}