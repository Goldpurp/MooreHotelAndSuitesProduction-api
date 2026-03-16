using Microsoft.Extensions.Options;
using MooreHotels.Application.Interfaces.Services;
using MooreHotels.Domain.Common;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace MooreHotels.Infrastructure.Services;

public class PaystackService : IPaymentService
{
    private readonly PaystackSettings _settings;
    private readonly IHttpClientFactory _httpClientFactory;

    public PaystackService(IOptions<PaystackSettings> settings, IHttpClientFactory httpClientFactory)
    {
        _settings = settings.Value;
        _httpClientFactory = httpClientFactory;
    }

    public async Task<string> InitializePaymentAsync(string email, decimal amount, string bookingCode, string callbackUrl)
    {
        var client = CreateClient();
        var payload = new
        {
            email,
            amount = (int)(amount * 100), // Paystack expects amount in Kobo
            reference = bookingCode,
            callback_url = callbackUrl,
            metadata = new { booking_code = bookingCode }
        };

        var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        var response = await client.PostAsync("transaction/initialize", content);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            throw new Exception($"Paystack Initialization Failed: {error}");
        }

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.GetProperty("data").GetProperty("authorization_url").GetString() 
               ?? throw new Exception("Paystack returned empty authorization URL.");
    }

    public async Task<bool> VerifyPaystackPaymentAsync(string reference)
    {
        var client = CreateClient();
        var response = await client.GetAsync($"transaction/verify/{reference}");

        if (!response.IsSuccessStatusCode) return false;

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var status = doc.RootElement.GetProperty("data").GetProperty("status").GetString();
        
        return status == "success";
    }

    public string GetSecretKey() => _settings.SecretKey;

    public string GetTransferInstructions()
    {
        return "Please transfer the total amount to:\n" +
               "Bank: Moore International Bank\n" +
               "Account Name: Moore Hotel and Suites Ltd\n" +
               "Account Number: 0078649036\n" +
               "Ref: [Your Booking Code]";
    }

    private HttpClient CreateClient()
    {
        var client = _httpClientFactory.CreateClient();
        client.BaseAddress = new Uri(_settings.BaseUrl);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _settings.SecretKey);
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return client;
    }
}
