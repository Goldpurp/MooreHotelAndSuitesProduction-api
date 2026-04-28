using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MooreHotels.Application.Interfaces.Services;
using MooreHotels.Domain.Common;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MooreHotels.Infrastructure.Services;

public class MonnifyService : IMonnifyService
{
    private readonly MonnifySettings _settings;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<MonnifyService> _logger;
    private string? _cachedToken;
    private DateTime _tokenExpiry;

    public MonnifyService(
        IOptions<MonnifySettings> settings, 
        IHttpClientFactory httpClientFactory,
        ILogger<MonnifyService> logger)
    {
        _settings = settings.Value;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<string> GetAccessTokenAsync()
    {
        if (!string.IsNullOrEmpty(_cachedToken) && DateTime.UtcNow < _tokenExpiry)
        {
            return _cachedToken;
        }

        var client = _httpClientFactory.CreateClient();
        var authString = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_settings.ApiKey}:{_settings.SecretKey}"));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", authString);

        var response = await client.PostAsync($"{_settings.BaseUrl}/api/v1/auth/login", null);
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            _logger.LogError("Monnify Auth Failed: {Error}", error);
            throw new Exception("Failed to authenticate with Monnify.");
        }

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var responseBody = doc.RootElement.GetProperty("responseBody");
        
        _cachedToken = responseBody.GetProperty("accessToken").GetString();
        var expires = responseBody.GetProperty("expiresIn").GetInt32();
        _tokenExpiry = DateTime.UtcNow.AddSeconds(expires - 60); // Buffer of 1 minute

        return _cachedToken ?? throw new Exception("Monnify returned empty token.");
    }

    public async Task<string> InitializeMonnifyPaymentAsync(string email, string name, decimal amount, string bookingCode, string callbackUrl)
    {
        var token = await GetAccessTokenAsync();
        var client = _httpClientFactory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var payload = new
        {
            amount,
            customerName = name,
            customerEmail = email,
            paymentReference = Guid.NewGuid().ToString("N"), // Internal transaction ref
            paymentDescription = $"Booking {bookingCode}",
            currencyCode = "NGN",
            contractCode = _settings.ContractCode,
            redirectUrl = callbackUrl,
            metadata = new { bookingCode }
        };

        var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        var response = await client.PostAsync($"{_settings.BaseUrl}/api/v1/merchant/transactions/init-transaction", content);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            _logger.LogError("Monnify Initialization Failed: {Error}", error);
            throw new Exception($"Monnify Initialization Failed: {error}");
        }

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var responseBody = doc.RootElement.GetProperty("responseBody");
        
        return responseBody.GetProperty("checkoutUrl").GetString() 
               ?? throw new Exception("Monnify returned empty checkout URL.");
    }

    public async Task<bool> VerifyTransactionAsync(string transactionReference)
    {
        var token = await GetAccessTokenAsync();
        var client = _httpClientFactory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync($"{_settings.BaseUrl}/api/v1/merchant/transactions/query?paymentReference={transactionReference}");
        if (!response.IsSuccessStatusCode) return false;

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var responseBody = doc.RootElement.GetProperty("responseBody");
        var status = responseBody.GetProperty("paymentStatus").GetString();

        return status == "PAID" || status == "OVERPAID";
    }
}
