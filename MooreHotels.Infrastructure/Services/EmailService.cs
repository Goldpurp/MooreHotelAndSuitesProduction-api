using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using MooreHotels.Application.Interfaces;
using MooreHotels.Application.DTOs;
using System.Text;
using System.Text.Json;
using System.Net.Http.Headers;

namespace MooreHotels.Infrastructure.Services;

public class EmailService : IEmailService
{
    private readonly EmailSettings _settings;
    private readonly ILogger<EmailService> _logger;
    private readonly IHttpClientFactory _httpClientFactory;

    public EmailService(IOptions<EmailSettings> settings, ILogger<EmailService> logger, IHttpClientFactory httpClientFactory)
    {
        _settings = settings.Value;
        _logger = logger;
        _httpClientFactory = httpClientFactory;
    }

    private async Task SendEmailAsync(string toEmail, string subject, string body)
    {
        var client = _httpClientFactory.CreateClient();
        
        // We repurpose 'SmtpPass' to hold your Brevo API Key
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        client.DefaultRequestHeaders.Add("api-key", _settings.SmtpPass);

        var payload = new
        {
            sender = new { name = _settings.SenderName, email = _settings.SenderEmail },
            to = new[] { new { email = toEmail } },
            subject = subject,
            htmlContent = body
        };

        try
        {
            var json = JsonSerializer.Serialize(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await client.PostAsync("https://api.brevo.com/v3/smtp/email", content);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                _logger.LogError("Brevo API Rejected Email: {Error}", error);
                throw new Exception("Email delivery failed via API.");
            }

            _logger.LogInformation("Email sent successfully to {Email}", toEmail);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "API Connection Error for {Email}", toEmail);
            throw; 
        }
    }

    // Guest Communications
    public async Task SendBookingConfirmationAsync(string email, string guestName, string bookingCode, string roomName, DateTime checkIn) 
        => await SendEmailAsync(email, $"Booking Confirmed: {bookingCode}", $"<p>Hi {guestName}, booking {bookingCode} for {roomName} on {checkIn:MMM dd} is confirmed.</p>");

    public async Task SendCancellationNoticeAsync(string email, string guestName, string bookingCode) 
        => await SendEmailAsync(email, "Booking Cancelled", $"<p>Booking {bookingCode} has been voided.</p>");

    public async Task SendCheckInReminderAsync(string email, string guestName, string bookingCode, DateTime checkIn) 
        => await SendEmailAsync(email, "Check-in Tomorrow", $"<p>We look forward to seeing you for stay {bookingCode} tomorrow.</p>");

    public async Task SendTemporaryPasswordAsync(string email, string guestName, string tempPassword) 
        => await SendEmailAsync(email, "Password Reset", $"<p>Hello {guestName}, your temporary password is: <strong>{tempPassword}</strong></p>");

       public async Task SendEmailVerificationAsync(string email, string name, string link)
    {
        var subject = "Verification Required: Moore Hotels";
        var body = $@"<div style='font-family: sans-serif; max-width: 600px; padding: 20px; border: 1px solid #eee;'>
            <h2 style='color: #d4af37;'>Verify Your Identity</h2>
            <p>Hello {name}, please confirm your email to activate your profile:</p>
            <a href='{link}' style='display: inline-block; background: #d4af37; color: #fff; padding: 10px 20px; text-decoration: none; border-radius: 5px;'>Activate Account</a>
        </div>";
        await SendEmailAsync(email, subject, body);
    }

    public async Task SendPaymentSuccessAsync(string email, string guestName, string bookingCode, decimal amount, string reference) 
        => await SendEmailAsync(email, "Payment Verified", $"<p>Received NGN {amount:N2} for {bookingCode}. Ref: {reference}</p>");

    public async Task SendCheckOutThankYouAsync(string email, string guestName, string bookingCode) 
        => await SendEmailAsync(email, "Thank You", $"<p>Thank you for staying at Moore Hotels! Code: {bookingCode}</p>");

    // Admin & Security
    public async Task SendAdminNewBookingAlertAsync(string adminEmail, string guestName, string bookingCode, string roomName, decimal amount) 
        => await SendEmailAsync(adminEmail, "ALERT: New Booking", $"<p>New booking {bookingCode} from {guestName}. Value: NGN {amount:N2}</p>");

    public async Task SendStaffWelcomeEmailAsync(string email, string name, string tempPassword, string role) 
        => await SendEmailAsync(email, "Welcome to the Team", $"<p>Welcome {name}, your account is set up as {role}. Temp pass: {tempPassword}</p>");

    public async Task SendAccountSuspendedAsync(string email, string name, string? reason = null)
    {
        var subject = "Security Alert: Access Suspended";
        var body = $@"<div style='border: 2px solid #e11d48; padding: 20px;'>
            <h1 style='color: #e11d48;'>Account Suspended</h1>
            <p>Hello {name}, your access has been suspended.</p>
            <p>Reason: {reason ?? "Administrative review."}</p>
        </div>";
        await SendEmailAsync(email, subject, body);
    }

    public async Task SendAccountActivatedAsync(string email, string name)
    {
        var subject = "Access Restored";
        var body = $"<p>Hello {name}, your Moore Hotels account has been reactivated.</p>";
        await SendEmailAsync(email, subject, body);
    }
}
