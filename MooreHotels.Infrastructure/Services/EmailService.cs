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

    private const string LogoUrl =
        "https://res.cloudinary.com/diovckpyb/image/upload/v1771366502/input-onlinepngtools_oxwqhd.png";

    public EmailService(
        IOptions<EmailSettings> settings,
        ILogger<EmailService> logger,
        IHttpClientFactory httpClientFactory)
    {
        _settings = settings.Value;
        _logger = logger;
        _httpClientFactory = httpClientFactory;
    }

    private async Task SendEmailAsync(string toEmail, string subject, string body)
    {
        var client = _httpClientFactory.CreateClient();
        client.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json"));
        client.DefaultRequestHeaders.Add("api-key", _settings.ApiPass);

        var payload = new
        {
            sender = new { name = _settings.SenderName, email = _settings.SenderEmail },
            to = new[] { new { email = toEmail } },
            subject,
            htmlContent = body
        };

        var json = JsonSerializer.Serialize(payload);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await client.PostAsync("https://api.brevo.com/v3/smtp/email", content);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            _logger.LogError("Brevo API rejected email: {Error}", error);
            throw new Exception("Email delivery failed.");
        }
    }

    private string BuildTemplate(string title, string content, string accentColor = "#C94B11")
    {
        string optimizedLogo = LogoUrl.Contains("cloudinary.com")
            ? LogoUrl.Replace("/upload/", "/upload/e_trim/f_auto,q_auto/")
            : LogoUrl;

        return $@"
<!DOCTYPE html>
<html lang='en'>
<head>
    <meta charset='UTF-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <meta http-equiv='X-UA-Compatible' content='IE=edge'>
    <!--[if mso]>
    <style>
        table {{ border-collapse: collapse; }}
        .container {{ width: 600px !important; }}
    </style>
    <![endif]-->
</head>
<body style='margin:0; padding:0; background-color:#ffffff; -webkit-text-size-adjust:100%;'>
    <table role='presentation' width='100%' border='0' cellspacing='0' cellpadding='0'>
        <tr>
            <td align='center'>
                <!-- This table expands to device width up to 600px -->
                <table class='container' role='presentation' width='100%' border='0' cellspacing='0' cellpadding='0' style='max-width:600px; background-color:#ffffff;'>
                    
                    <!-- Full-Width Header Image/Logo -->
                   <tr>
                        <td align='center' style='padding: 30px; line-height:0; font-size:0;'>
                            <img src='{optimizedLogo}' 
                                 alt='Moore Hotels' 
                                 style='width:100%; max-width:240px; height:auto; display:inline-block; border:0;' 
                                 width='240' />
                        </td>
                    </tr>

                    <!-- Body Content -->
                    <tr>
                        <td style='padding: 40px 20px; font-family: ""Helvetica Neue"", Helvetica, Arial, sans-serif; color:#2D3436;'>
                            <h1 style='margin:0 0 20px 0; font-size:26px; font-weight:700; color:{accentColor}; line-height:1.2;'>
                                {title}
                            </h1>
                            <div style='font-size:16px; line-height:1.7; color:#444444;'>
                                {content}
                            </div>
                        </td>
                    </tr>

                    <!-- Luxury Footer (Edge-to-Edge) -->
                    <tr>
                        <td align='center' style='padding: 40px 20px; background-color:#111111; color:#FFFFFF; font-family: Arial, sans-serif;'>
                            <div style='font-size:14px; font-weight:bold; letter-spacing:3px; text-transform:uppercase; margin-bottom:10px;'>
                                Moore Hotels & Suites
                            </div>
                            <div style='font-size:11px; color:#888888; letter-spacing:1px;'>
                                LUXURY COMFORT • EXCEPTIONAL EXPERIENCE
                            </div>
                        </td>
                    </tr>

                </table>
            </td>
        </tr>
    </table>
</body>
</html>";
    }




    // Guest emails
    public async Task SendBookingConfirmationAsync(string email, string guestName, string bookingCode, string roomName, DateTime checkIn)
    {
        var content = $"Dear <strong>{guestName}</strong>,<br/><br/>Your booking <strong>{bookingCode}</strong> for <strong>{roomName}</strong> on <strong>{checkIn:MMM dd, yyyy}</strong> has been confirmed.";
        await SendEmailAsync(email, $"Booking Confirmed: {bookingCode}", BuildTemplate("Booking Confirmed", content));
    }

    public async Task SendCancellationNoticeAsync(string email, string guestName, string bookingCode, string roomName, DateTime checkIn, string? reason = null)
    {
        var content = $@"Dear <strong>{guestName}</strong>,<br/><br/>Your booking for <strong>{roomName}</strong> on <strong>{checkIn:MMM dd, yyyy}</strong> has been cancelled.<br/>{(string.IsNullOrWhiteSpace(reason) ? "" : $"<br/><strong>Reason:</strong> {reason}")}";
        await SendEmailAsync(email, $"Booking Cancelled: {bookingCode}", BuildTemplate("Booking Cancelled", content, "#e11d48"));
    }

    public async Task SendCheckInReminderAsync(string email, string guestName, string bookingCode, DateTime checkIn)
    {
        var content = $@"Dear <strong>{guestName}</strong>,<br/><br/>Your stay (<strong>{bookingCode}</strong>) begins on <strong>{checkIn:MMM dd, yyyy}</strong>. We look forward to hosting you.";
        await SendEmailAsync(email, "Check-In Reminder", BuildTemplate("Upcoming Stay Reminder", content));
    }

    public async Task SendTemporaryPasswordAsync(string email, string guestName, string tempPassword)
    {
        var content = $@"Dear <strong>{guestName}</strong>,<br/><br/>Your temporary password is:<br/><br/><div style='font-size:20px;font-weight:600;color:#C94B11'>{tempPassword}</div>";
        await SendEmailAsync(email, "Temporary Password", BuildTemplate("Security Notification", content));
    }

    public async Task SendEmailVerificationAsync(string email, string name, string link)
    {
        var content = $@"Dear <strong>{name}</strong>,<br/><br/>Please verify your email address by clicking below.<br/><br/>
<a href='{link}' style='background:#C94B11;color:#fff;padding:12px 28px;border-radius:6px;text-decoration:none;font-weight:600;'>Verify Email</a>";
        await SendEmailAsync(email, "Email Verification", BuildTemplate("Email Verification Required", content));
    }

    public async Task SendPaymentSuccessAsync(string email, string guestName, string bookingCode, decimal amount, string reference)
    {
        var content = $@"Dear <strong>{guestName}</strong>,<br/><br/>Payment of <strong>NGN {amount:N2}</strong> for booking <strong>{bookingCode}</strong> has been received.<br/>Reference: {reference}";
        await SendEmailAsync(email, "Payment Confirmed", BuildTemplate("Payment Successful", content));
    }

    public async Task SendCheckOutThankYouAsync(string email, string guestName, string bookingCode)
    {
        var content = $@"Dear <strong>{guestName}</strong>,<br/><br/>Thank you for staying with us. We hope your stay (<strong>{bookingCode}</strong>) was exceptional.";
        await SendEmailAsync(email, "Thank You", BuildTemplate("We Appreciate Your Stay", content));
    }

    // Vibrant staff welcome email
    public async Task SendStaffWelcomeEmailAsync(
        string email,
        string name,
        string tempPassword,
        string role)
    {
        var subject = "Welcome to Moore Hotels Team";
        var accentGreen = "#16a34a";

        var content = $@"
        <p style='margin-top:0; font-size:16px; color:#2D3436;'>
            Dear <strong>{name}</strong>,
        </p>

        <p style='font-size:16px; color:#4A4A4A;'>
            Welcome to the <strong>Moore Hotels & Suites</strong> family. 
            Your staff account has been successfully provisioned.
        </p>

        <!-- Luxury Info Box -->
        <table role='presentation' width='100%' border='0' cellspacing='0' cellpadding='0' 
               style='margin:30px 0; background-color:#F0FDF4; border: 1px solid #DCFCE7; border-radius:8px;'>
            <tr>
                <td style='padding:25px;'>
                    <div style='margin-bottom:15px;'>
                        <span style='font-size:12px; font-weight:bold; color:{accentGreen}; text-transform:uppercase; letter-spacing:1px;'>Assigned Role</span><br/>
                        <span style='font-size:16px; color:#111827;'>{role}</span>
                    </div>
                    <div>
                        <span style='font-size:12px; font-weight:bold; color:{accentGreen}; text-transform:uppercase; letter-spacing:1px;'>Temporary Password</span><br/>
                        <span style='font-size:24px; font-weight:700; color:#111827; font-family: monospace;'>{tempPassword}</span>
                    </div>
                </td>
            </tr>
        </table>

        <p style='font-size:15px; color:#4A4A4A; line-height:1.6;'>
            For security purposes, please log in to the [Staff Portal](https://admin.moorehotelandsuites.com) immediately to change your password.
        </p>

        <p style='font-size:13px; color:#94A3B8; font-style:italic; margin-top:25px; border-top:1px solid #F1F5F9; padding-top:15px;'>
            If you experience any issues accessing your account, please contact the system administrator.
        </p>";

        await SendEmailAsync(
            email,
            subject,
            BuildTemplate("Welcome to the Team", content, accentGreen));
    }

    // Admin & security
    public async Task SendAdminNewBookingAlertAsync(string adminEmail, string guestName, string bookingCode, string roomName, decimal amount)
    {
        var content = $@"
        <p style='margin:0 0 20px 0;'>A new reservation has been confirmed in the system.</p>
        <table role='presentation' width='100%' style='background-color:#F8F9FA; border-radius:8px;'>
            <tr>
                <td style='padding:20px;'>
                    <div style='font-size:12px; color:#64748B; text-transform:uppercase; letter-spacing:1px;'>Guest Details</div>
                    <div style='font-size:16px; font-weight:bold; color:#1E293B; margin-bottom:15px;'>{guestName}</div>
                    
                    <div style='font-size:12px; color:#64748B; text-transform:uppercase; letter-spacing:1px;'>Room Type</div>
                    <div style='font-size:16px; font-weight:bold; color:#1E293B; margin-bottom:15px;'>{roomName}</div>
                    
                    <div style='font-size:12px; color:#64748B; text-transform:uppercase; letter-spacing:1px;'>Total Revenue</div>
                    <div style='font-size:20px; font-weight:bold; color:#16A34A;'>NGN {amount:N2}</div>
                </td>
            </tr>
        </table>";
        await SendEmailAsync(adminEmail, "New Booking Alert", BuildTemplate("New Booking Received", content, "#C94B11"));
    }


    public async Task SendAccountSuspendedAsync(string email, string name)
    {
        var content = $@"
        <p>Dear <strong>{name}</strong>,</p>
        <p>This is to inform you that your account access has been suspended by the administration.</p>

        <p style='font-size:14px;'>Please contact support if you believe this is an error.</p>";
        await SendEmailAsync(email, "Account Suspended", BuildTemplate("Security Notification", content, "#E11D48"));
    }

    public async Task SendAccountActivatedAsync(string email, string name)
    {
        var content = $@"
        <p>Dear <strong>{name}</strong>,</p>
        <p>We are pleased to inform you that your account access at <strong>Moore Hotels & Suites</strong> has been successfully restored.</p>
        <p>You may now log in to the portal using your existing credentials.</p>";
        await SendEmailAsync(email, "Account Activated", BuildTemplate("Access Restored", content, "#C94B11"));
    }

    // Refunds
    public async Task SendAdminRefundAlertAsync(string adminEmail, string guestName, string bookingCode, string roomName, decimal amount)
    {
        var content = $@"
        <p style='margin:0 0 20px 0;'>A manual refund requires your immediate attention.</p>
        <table role='presentation' width='100%' style='border:1px solid #FECACA; background-color:#FEF2F2; border-radius:8px;'>
            <tr>
                <td style='padding:20px;'>
                    <div style='margin-bottom:10px;'><strong>Booking Code:</strong> {bookingCode}</div>
                    <div style='margin-bottom:10px;'><strong>Guest:</strong> {guestName}</div>
                    <div style='margin-bottom:10px;'><strong>Room:</strong> {roomName}</div>
                    <div style='font-size:18px; color:#E11D48; font-weight:bold;'>Amount: NGN {amount:N2}</div>
                </td>
            </tr>
        </table>";
        await SendEmailAsync(adminEmail, "Refund Action Required", BuildTemplate("Refund Action Required", content, "#E11D48"));
    }

    public async Task SendRefundCompletionNoticeAsync(string email, string guestName, string bookingCode, decimal amount, string reference)
    {
        var content = $@"
        <p>Dear <strong>{guestName}</strong>,</p>
        <p>Your refund has been successfully processed and should reflect in your account shortly.</p>
        <table role='presentation' width='100%' style='margin:25px 0; border-top:1px solid #EEE; border-bottom:1px solid #EEE; padding:20px 0;'>
            <tr>
                <td>
                    <div style='font-size:13px; color:#666;'>Amount Refunded</div>
                    <div style='font-size:22px; font-weight:bold; color:#111;'>NGN {amount:N2}</div>
                </td>
                <td align='right'>
                    <div style='font-size:13px; color:#666;'>Reference ID</div>
                    <div style='font-size:14px; font-family:monospace; font-weight:bold;'>{reference}</div>
                </td>
            </tr>
        </table>
        <p style='font-size:14px; color:#666;'>Thank you for your patience.</p>";
        await SendEmailAsync(email, "Refund Completed", BuildTemplate("Refund Successful", content, "#16A34A"));
    }
}
