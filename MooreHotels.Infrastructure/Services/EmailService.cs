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
    var subject = "Email Verification – Moore Hotels & Suites";

    var body = $@"
    <div style='margin:0; padding:0; background-color:#f5f5f5; font-family:Segoe UI, Arial, sans-serif;'>

        <table align='center' width='100%' cellspacing='0' cellpadding='0' style='padding:40px 0;'>
            <tr>
                <td align='center'>
                
                    <table width='600' cellspacing='0' cellpadding='0' 
                           style='background:#ffffff; border-radius:10px; overflow:hidden; box-shadow:0 4px 12px rgba(0,0,0,0.08);'>

                        <!-- Logo -->
                        <tr>
                            <td align='center' style='padding:30px 20px; background:#000000;'>
                                <img src='https://res.cloudinary.com/diovckpyb/image/upload/v1770752301/d6qqrpcxf1cqnkm9mzm5.jpg'
                                     alt='Moore Hotels Logo'
                                     style='display:block; max-width:200px;' />
                            </td>
                        </tr>

                        <!-- Header -->
                        <tr>
                            <td style='padding:35px 40px 10px 40px; text-align:center;'>
                                <h2 style='margin:0; color:#d4af37; font-weight:600; letter-spacing:1px;'>
                                    Email Verification Required
                                </h2>
                            </td>
                        </tr>

                        <!-- Message -->
                        <tr>
                            <td style='padding:20px 40px; color:#333333; font-size:15px; line-height:1.6; text-align:center;'>
                                Dear <strong>{name}</strong>,<br/><br/>
                                Thank you for choosing <strong>Moore Hotels & Suites</strong>. 
                                To complete your registration and activate your account, 
                                kindly confirm your email address by clicking the button below.
                            </td>
                        </tr>

                        <!-- Button -->
                        <tr>
                            <td align='center' style='padding:10px 40px 40px 40px;'>
                                <a href='{link}' 
                                   style='background:#d4af37; color:#ffffff; text-decoration:none; 
                                          padding:14px 34px; border-radius:6px; font-size:15px;
                                          font-weight:600; display:inline-block;'>
                                   Verify Email Address
                                </a>
                            </td>
                        </tr>

                        <!-- Footer -->
                        <tr>
                            <td style='background:#fafafa; padding:25px 30px; text-align:center; 
                                       font-size:12px; color:#777777;'>
                                Moore Hotels & Suites<br/>
                                Luxury Comfort • Exceptional Experience<br/><br/>
                                If you did not create this account, please ignore this email.
                            </td>
                        </tr>

                    </table>

                </td>
            </tr>
        </table>

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
