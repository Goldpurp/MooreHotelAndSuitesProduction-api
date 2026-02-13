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
    {
        var subject = $"Booking Confirmed: {bookingCode}";

        var body = $@"
    <div style='background:#f5f5f5; padding:40px 0; font-family:Segoe UI, Arial;'>

    <table align='center' width='600' style='background:#fff;border-radius:10px;box-shadow:0 4px 12px rgba(0,0,0,0.08);'>
         <tr>
                            <td align='center' style='padding:30px 20px; background:#ffffff;'>
                                <img src='https://res.cloudinary.com/diovckpyb/image/upload/v1770752301/d6qqrpcxf1cqnkm9mzm5.jpg'
                                     alt='Moore Hotels Logo'
                                     style='display:block; max-width:200px;' />
                            </td>
                        </tr>

        <tr><td style='padding:10px 40px;text-align:center;'>
            <h2 style='color:#C94B11;'>Booking Confirmed</h2>
        </td></tr>

        <tr><td style='padding:20px 40px;color:#333;text-align:center;line-height:1.6;'>
            Dear <strong>{guestName}</strong>,<br/><br/>
            Your reservation <strong>{bookingCode}</strong> for 
            <strong>{roomName}</strong> on <strong>{checkIn:MMM dd, yyyy}</strong>
            has been successfully confirmed.
        </td></tr>

        <tr><td style='background:#fafafa;padding:20px;text-align:center;font-size:12px;color:#777;'>
            Moore Hotels & Suites • Luxury Comfort
        </td></tr>
    </table>
    </div>";

        await SendEmailAsync(email, subject, body);
    }


    public async Task SendCancellationNoticeAsync(string email, string guestName, string bookingCode, string roomName, DateTime checkIn, string? reason = null)
    {
        var subject = "Booking Cancelled";

        var body = $@"
<div style='background:#f5f5f5;padding:40px 0;font-family:Segoe UI, Arial;'>
<table align='center' width='600' style='background:#fff;border-radius:10px;box-shadow:0 4px 12px rgba(0,0,0,0.08);'>
<tr>
    <td align='center' style='padding:30px 20px; background:#ffffff;'>
        <img src='https://res.cloudinary.com/diovckpyb/image/upload/v1770752301/d6qqrpcxf1cqnkm9mzm5.jpg'
             alt='Moore Hotels Logo'
             style='display:block; max-width:200px;' />
    </td>
</tr>

<tr>
<td style='padding:20px 40px;text-align:center;'>
<h2 style='color:#e11d48;'>Booking Cancelled</h2>

Dear <strong>{guestName}</strong>,<br/><br/>

Your booking <strong>{bookingCode}</strong> for <strong>{roomName}</strong><br/>
scheduled on <strong>{checkIn:MMM dd, yyyy}</strong> has been successfully cancelled.
<br/><br/>

{(string.IsNullOrWhiteSpace(reason) ? "" : $"<strong>Reason:</strong> {reason}<br/><br/>")}

If payment was completed, refund processing will follow according to our policy.
</td>
</tr>

<tr>
<td style='background:#fafafa;padding:20px;text-align:center;font-size:12px;color:#777;'>
Moore Hotels & Suites • Luxury Comfort
</td>
</tr>
</table>
</div>";

        await SendEmailAsync(email, subject, body);
    }

    public async Task SendCheckInReminderAsync(string email, string guestName, string bookingCode, DateTime checkIn)
    {
        var subject = "Check-in Reminder";

        var body = $@"<div style='background:#f5f5f5;padding:40px 0;font-family:Segoe UI, Arial;'>
    <table align='center' width='600' style='background:#fff;border-radius:10px;box-shadow:0 4px 12px rgba(0,0,0,0.08);'>
          <tr>
                            <td align='center' style='padding:30px 20px; background:#ffffff;'>
                                <img src='https://res.cloudinary.com/diovckpyb/image/upload/v1770752301/d6qqrpcxf1cqnkm9mzm5.jpg'
                                     alt='Moore Hotels Logo'
                                     style='display:block; max-width:200px;' />
                            </td>
                        </tr>
        <tr><td style='padding:20px 40px;text-align:center;'>
            <h2 style='color:#C94B11;'>We Look Forward To Hosting You</h2>
            Dear <strong>{guestName}</strong>,<br/><br/>
            This is a gentle reminder that your stay ({bookingCode}) begins 
            on <strong>{checkIn:MMM dd}</strong>.
        </td></tr>
        <tr><td style='background:#fafafa;padding:20px;text-align:center;font-size:12px;color:#777;'>Moore Hotels & Suites</td></tr>
    </table></div>";

        await SendEmailAsync(email, subject, body);
    }


    public async Task SendTemporaryPasswordAsync(string email, string guestName, string tempPassword)
    {
        var subject = "Temporary Password Issued";

        var body = $@"<div style='background:#f5f5f5;padding:40px 0;font-family:Segoe UI, Arial;'>
    <table align='center' width='600' style='background:#fff;border-radius:10px;box-shadow:0 4px 12px rgba(0,0,0,0.08);'>
          <tr>
                            <td align='center' style='padding:30px 20px; background:#ffffff;'>
                                <img src='https://res.cloudinary.com/diovckpyb/image/upload/v1770752301/d6qqrpcxf1cqnkm9mzm5.jpg'
                                     alt='Moore Hotels Logo'
                                     style='display:block; max-width:200px;' />
                            </td>
                        </tr>
        <tr><td style='padding:20px 40px;text-align:center;'>
            Dear <strong>{guestName}</strong>,<br/><br/>
            Your temporary password is:
            <div style='font-size:20px;font-weight:bold;margin-top:15px;color:#C94B11'>{tempPassword}</div>
        </td></tr>

        <tr><td style='background:#fafafa;padding:20px;text-align:center;font-size:12px;color:#777;'>Moore Hotels & Suites Security Notice</td></tr>
    </table></div>";

        await SendEmailAsync(email, subject, body);
    }


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
                            <td align='center' style='padding:30px 20px; background:#ffffff;'>
                                <img src='https://res.cloudinary.com/diovckpyb/image/upload/v1770752301/d6qqrpcxf1cqnkm9mzm5.jpg'
                                     alt='Moore Hotels Logo'
                                     style='display:block; max-width:200px;' />
                            </td>
                        </tr>

                        <!-- Header -->
                        <tr>
                            <td style='padding:35px 40px 10px 40px; text-align:center;'>
                                <h2 style='margin:0; color:#C94B11; font-weight:600; letter-spacing:1px;'>
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
                                   style='background:#C94B11; color:#ffffff; text-decoration:none; 
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
    {
        var subject = "Payment Confirmed";

        var body = $@"<div style='background:#f5f5f5;padding:40px 0;font-family:Segoe UI, Arial;'>
    <table align='center' width='600' style='background:#fff;border-radius:10px;box-shadow:0 4px 12px rgba(0,0,0,0.08);'>
     <tr>
                            <td align='center' style='padding:30px 20px; background:#ffffff;'>
                                <img src='https://res.cloudinary.com/diovckpyb/image/upload/v1770752301/d6qqrpcxf1cqnkm9mzm5.jpg'
                                     alt='Moore Hotels Logo'
                                     style='display:block; max-width:200px;' />
                            </td>
                        </tr>
        <tr><td style='padding:20px 40px;text-align:center;'>
            Dear <strong>{guestName}</strong>,<br/><br/>
            Payment of <strong>NGN {amount:N2}</strong> for booking 
            <strong>{bookingCode}</strong> has been received.<br/>
            Reference: {reference}
        </td></tr>

        <tr><td style='background:#fafafa;padding:20px;text-align:center;font-size:12px;color:#777;'>Moore Hotels & Suites</td></tr>
    </table></div>";

        await SendEmailAsync(email, subject, body);
    }


    public async Task SendCheckOutThankYouAsync(string email, string guestName, string bookingCode)
    {
        var subject = "Thank You For Staying With Us";

        var body = $@"<div style='background:#f5f5f5;padding:40px 0;font-family:Segoe UI, Arial;'>
    <table align='center' width='600' style='background:#fff;border-radius:10px;box-shadow:0 4px 12px rgba(0,0,0,0.08);'>
    <tr>
                            <td align='center' style='padding:30px 20px; background:#ffffff;'>
                                <img src='https://res.cloudinary.com/diovckpyb/image/upload/v1770752301/d6qqrpcxf1cqnkm9mzm5.jpg'
                                     alt='Moore Hotels Logo'
                                     style='display:block; max-width:200px;' />
                            </td>
                        </tr>
        <tr><td style='padding:20px 40px;text-align:center;'>
            Dear <strong>{guestName}</strong>,<br/><br/>
            Thank you for choosing Moore Hotels & Suites.  
            We hope your stay ({bookingCode}) was exceptional and we look forward to welcoming you again.
        </td></tr>

        <tr><td style='background:#fafafa;padding:20px;text-align:center;font-size:12px;color:#777;'>Luxury Comfort • Exceptional Experience</td></tr>
    </table></div>";

        await SendEmailAsync(email, subject, body);
    }


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
    
    public async Task SendAdminRefundAlertAsync(string adminEmail, string guestName, string bookingCode, decimal amount)
{
    var subject = $"ACTION REQUIRED: Manual Refund Pending - {bookingCode}";
    var body = $@"
        <h2 style='color: #d9534f;'>Manual Refund Required</h2>
        <p>A paid booking has been cancelled and requires manual processing.</p>
        <ul>
            <li><strong>Booking Code:</strong> {bookingCode}</li>
            <li><strong>Guest Name:</strong> {guestName}</li>
            <li><strong>Amount to Refund:</strong> NGN {amount:N2}</li>
        </ul>
        <p>Please process this via your bank or payment gateway and update the status in the Admin Dashboard once completed.</p>";

    await SendEmailAsync(adminEmail, subject, body);
}

public async Task SendRefundCompletionNoticeAsync(string email, string guestName, string bookingCode, decimal amount, string reference)
{
    var subject = $"Refund Processed: {bookingCode}";
    var body = $@"
        <h2>Your Refund has been Processed</h2>
        <p>Hi {guestName},</p>
        <p>We've successfully processed a manual refund of <strong>NGN {amount:N2}</strong> for your cancelled booking <strong>{bookingCode}</strong>.</p>
        <p><strong>Transaction Reference:</strong> {reference}</p>
        <p>Depending on your bank, it may take 3-5 business days for the funds to reflect in your account.</p>
        <p>Thank you for your patience,<br/>Moore Hotels Team</p>";

    await SendEmailAsync(email, subject, body);
}

public async Task SendAdminRefundAlertAsync(string adminEmail, string guestName, string bookingCode, string roomName, decimal amount)
{
    var subject = $"ACTION REQUIRED: Manual Refund Pending - {bookingCode}";
    
    // Create a high-priority body for the staff
    var body = $@"
        <div style='font-family: Arial, sans-serif; border: 1px solid #d9534f; padding: 20px;'>
            <h2 style='color: #d9534f;'>Manual Refund Required</h2>
            <p>A paid booking has been cancelled and requires manual processing to return funds to the guest.</p>
            <table style='width: 100%; border-collapse: collapse;'>
                <tr><td style='padding: 8px; border-bottom: 1px solid #eee;'><strong>Booking Code:</strong></td><td style='padding: 8px; border-bottom: 1px solid #eee;'>{bookingCode}</td></tr>
                <tr><td style='padding: 8px; border-bottom: 1px solid #eee;'><strong>Guest Name:</strong></td><td style='padding: 8px; border-bottom: 1px solid #eee;'>{guestName}</td></tr>
                <tr><td style='padding: 8px; border-bottom: 1px solid #eee;'><strong>Room:</strong></td><td style='padding: 8px; border-bottom: 1px solid #eee;'>{roomName}</td></tr>
                <tr><td style='padding: 8px; border-bottom: 1px solid #eee;'><strong>Refund Amount:</strong></td><td style='padding: 8px; border-bottom: 1px solid #eee;'>NGN {amount:N2}</td></tr>
            </table>
            <p style='margin-top: 20px;'><strong>Next Steps:</strong></p>
            <ol>
                <li>Verify the original transaction in your payment gateway (e.g., Paystack).</li>
                <li>Process the manual refund via your bank/gateway.</li>
                <li>Log the transaction reference in the Admin Dashboard to mark as <strong>Refunded</strong>.</li>
            </ol>
            <p><em>This is an automated alert from Moore Hotels System.</em></p>
        </div>";

    await SendEmailAsync(adminEmail, subject, body);
}

}
