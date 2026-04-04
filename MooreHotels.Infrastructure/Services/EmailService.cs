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
    
    private string GetBookingSummaryHtml(string bookingCode, string roomName, string roomCategory, int? capacity, DateTime? checkIn, DateTime? checkOut, int? nights, decimal? amount)
    {
        var html = $@"
        <div style='margin-top:25px; background-color:#F8FAFC; border:1px solid #E2E8F0; border-radius:12px; overflow:hidden;'>
            <div style='background-color:#F1F5F9; padding:12px 20px; border-bottom:1px solid #E2E8F0;'>
                <span style='font-size:12px; font-weight:700; color:#64748B; text-transform:uppercase; letter-spacing:1px;'>Booking Details</span>
            </div>
            <div style='padding:20px;'>
                <table role='presentation' width='100%' border='0' cellspacing='0' cellpadding='0'>
                    <tr>
                        <td style='padding-bottom:15px;'>
                            <div style='font-size:11px; color:#94A3B8; text-transform:uppercase; margin-bottom:4px;'>Booking Reference</div>
                            <div style='font-size:16px; font-weight:700; color:#1E293B; font-family:monospace;'>{bookingCode}</div>
                        </td>
                        <td style='padding-bottom:15px;' align='right'>
                            <div style='font-size:11px; color:#94A3B8; text-transform:uppercase; margin-bottom:4px;'>Room Name</div>
                            <div style='font-size:16px; font-weight:700; color:#1E293B;'>{roomName}</div>
                        </td>
                    </tr>
                    <tr>
                        <td style='padding-bottom:15px;'>
                            <div style='font-size:11px; color:#94A3B8; text-transform:uppercase; margin-bottom:4px;'>Room Type</div>
                            <div style='font-size:14px; color:#475569;'>{roomCategory}</div>
                        </td>
                        <td style='padding-bottom:15px;' align='right'>
                            <div style='font-size:11px; color:#94A3B8; text-transform:uppercase; margin-bottom:4px;'>Max Occupancy</div>
                            <div style='font-size:14px; color:#475569;'>{capacity} Guest(s)</div>
                        </td>
                    </tr>";

        if (checkIn.HasValue && checkOut.HasValue)
        {
            html += $@"
                    <tr style='border-top: 1px dashed #E2E8F0;'>
                        <td style='padding:15px 0;'>
                            <div style='font-size:11px; color:#94A3B8; text-transform:uppercase; margin-bottom:4px;'>Check-In</div>
                            <div style='font-size:14px; color:#1E293B; font-weight:600;'>{checkIn.Value:MMM dd, yyyy}</div>
                            <div style='font-size:12px; color:#64748B;'>After 2:00 PM</div>
                        </td>
                        <td style='padding:15px 0;' align='right'>
                            <div style='font-size:11px; color:#94A3B8; text-transform:uppercase; margin-bottom:4px;'>Check-Out</div>
                            <div style='font-size:14px; color:#1E293B; font-weight:600;'>{checkOut.Value:MMM dd, yyyy}</div>
                            <div style='font-size:12px; color:#64748B;'>Before 12:00 PM</div>
                        </td>
                    </tr>";
        }

        if (nights.HasValue && amount.HasValue)
        {
            html += $@"
                    <tr style='background-color:#F1F5F9;'>
                        <td style='padding:15px;'>
                            <div style='font-size:11px; color:#64748B; text-transform:uppercase; margin-bottom:4px;'>Stay Duration</div>
                            <div style='font-size:14px; color:#1E293B; font-weight:600;'>{nights} Night(s)</div>
                        </td>
                        <td style='padding:15px;' align='right'>
                            <div style='font-size:11px; color:#64748B; text-transform:uppercase; margin-bottom:4px;'>Total Paid</div>
                            <div style='font-size:18px; color:#16A34A; font-weight:700;'>NGN {amount.Value:N2}</div>
                        </td>
                    </tr>";
        }

        html += @"
                </table>
            </div>
        </div>";

        return html;
    }




    // Guest emails
    public async Task SendBookingConfirmationAsync(string email, string guestName, string bookingCode, string roomName, string roomCategory, int capacity, DateTime checkIn, DateTime checkOut, int nights, decimal totalAmount)
    {
        var content = $@"
        <p style='margin-top:0;'>Dear <strong>{guestName}</strong>,</p>
        <p>Your reservation at <strong>Moore Hotels & Suites</strong> has been successfully confirmed. We are thrilled to host you and are committed to ensuring your stay is exceptional.</p>
        
        {GetBookingSummaryHtml(bookingCode, roomName, roomCategory, capacity, checkIn, checkOut, nights, totalAmount)}

        <div style='margin-top:25px; padding:15px; background-color:#FEF9C3; border: 1px solid #FEF08A; border-radius:8px; font-size:14px; color:#854D0E;'>
            <strong>Important Note:</strong> Please present a valid government-issued ID upon check-in. Our check-in time starts from 2:00 PM.
        </div>
        <p style='margin-top:20px;'>If you have any special requests prior to your arrival, simply reply to this email.</p>";

        await SendEmailAsync(email, $"Booking Confirmed: {bookingCode}", BuildTemplate("Room Confirmed", content));
    }

    public async Task SendCancellationNoticeAsync(string email, string guestName, string bookingCode, string roomName, string roomCategory, DateTime checkIn, string? reason = null)
    {
        var content = $@"
        <p style='margin-top:0;'>Dear <strong>{guestName}</strong>,</p>
        <p>This email confirms that your reservation for <strong>{roomName}</strong> starting on <strong>{checkIn:MMM dd, yyyy}</strong> has been cancelled.</p>
        
        <div style='margin:20px 0; padding:15px; background-color:#FFF1F2; border-left:4px solid #E11D48; color:#9F1239;'>
            <strong>Reason for Cancellation:</strong><br/>
            {(!string.IsNullOrWhiteSpace(reason) ? reason : "Requested by guest or management.")}
        </div>

        {GetBookingSummaryHtml(bookingCode, roomName, roomCategory, 0, checkIn, checkIn, 0, 0)}

        <p style='margin-top:20px; font-size:14px; color:#64748B;'>If this was an error or you wish to re-book, please visit our website or contact our support team.</p>";

        await SendEmailAsync(email, $"Booking Cancelled: {bookingCode}", BuildTemplate("Booking Cancelled", content, "#e11d48"));
    }

    public async Task SendCheckInReminderAsync(string email, string guestName, string bookingCode, string roomName, DateTime checkIn)
    {
        var content = $@"
        <p style='margin-top:0;'>Dear <strong>{guestName}</strong>,</p>
        <p>We are looking forward to your arrival! This is a friendly reminder that your stay in our <strong>{roomName}</strong> begins tomorrow, <strong>{checkIn:MMM dd, yyyy}</strong>.</p>
        
        <div style='margin:25px 0; text-align:center;'>
             <a href='https://moorehotelandsuites.com/my-booking' style='display:inline-block; background-color:#C94B11; color:#FFFFFF; padding:14px 30px; border-radius:8px; text-decoration:none; font-weight:700;'>View Stay Details</a>
        </div>

        <p style='font-size:14px; line-height:1.6;'>Standard check-in starts at <strong>2:00 PM</strong>. Safe travels!</p>";

        await SendEmailAsync(email, "Check-In Reminder", BuildTemplate("Your Stay Begins Tomorrow", content));
    }    public async Task SendTemporaryPasswordAsync(string email, string guestName, string tempPassword)
    {
        var content = $@"
        <p style='margin-top:0;'>Dear <strong>{guestName}</strong>,</p>
        <p>A temporary access code has been generated for your account. For your security, please use this to log in and change your password immediately.</p>
        
        <div style='margin:30px 0; text-align:center; padding:30px; background-color:#F8FAFC; border:1px solid #E2E8F0; border-radius:12px;'>
            <div style='font-size:12px; color:#64748B; text-transform:uppercase; letter-spacing:2px; margin-bottom:10px;'>Your Temporary Code</div>
            <div style='font-size:32px; font-weight:800; color:#C94B11; letter-spacing:4px; font-family:monospace;'>{tempPassword}</div>
        </div>

        <p style='font-size:14px; color:#64748B; font-style:italic;'>If you did not request this change, please contact our security team immediately.</p>";

        await SendEmailAsync(email, "Temporary Access Code", BuildTemplate("Security Notification", content));
    }

    public async Task SendEmailVerificationAsync(string email, string name, string link)
    {
        var content = $@"
        <p style='margin-top:0;'>Dear <strong>{name}</strong>,</p>
        <p>Thank you for registering with <strong>Moore Hotels & Suites</strong>. To complete your account setup and access our full range of services, please verify your email address below.</p>
        
        <div style='margin:35px 0; text-align:center;'>
             <a href='{link}' style='display:inline-block; background-color:#C94B11; color:#FFFFFF; padding:16px 35px; border-radius:8px; text-decoration:none; font-weight:700; font-size:16px; box-shadow:0 4px 6px rgba(201,75,17,0.2);'>Verify My Email</a>
        </div>

        <p style='font-size:13px; color:#94A3B8;'>This link will expire in 24 hours. If the button above doesn't work, copy and paste this link into your browser:<br/>{link}</p>";

        await SendEmailAsync(email, "Verify Your Email", BuildTemplate("Confirm Your Registration", content));
    }

    public async Task SendPaymentSuccessAsync(string email, string guestName, string bookingCode, string roomName, decimal amount, string reference)
    {
        var content = $@"
        <p style='margin-top:0;'>Dear <strong>{guestName}</strong>,</p>
        <p>This is to confirm that we have successfully received your payment for booking <strong>{bookingCode}</strong>.</p>
        
        <table role='presentation' width='100%' style='margin:25px 0; background-color:#F0FDF4; border: 1px solid #BBF7D0; border-radius:12px; padding:20px;'>
            <tr>
                <td>
                    <div style='font-size:12px; color:#166534; text-transform:uppercase; letter-spacing:1px; margin-bottom:5px;'>Amount Received</div>
                    <div style='font-size:24px; font-weight:800; color:#14532D;'>NGN {amount:N2}</div>
                </td>
                <td align='right'>
                    <div style='font-size:11px; color:#166534; text-transform:uppercase; letter-spacing:1px; margin-bottom:5px;'>Transaction Reference</div>
                    <div style='font-size:14px; font-family:monospace; color:#14532D;'>{reference}</div>
                </td>
            </tr>
        </table>

        <p style='font-size:14px; color:#4B5563;'>Your stay in our <strong>{roomName}</strong> is now fully secured. We look forward to seeing you soon.</p>";

        await SendEmailAsync(email, "Payment Confirmed", BuildTemplate("Payment Successful", content, "#16A34A"));
    }

    public async Task SendCheckOutThankYouAsync(string email, string guestName, string bookingCode, string roomName)
    {
        var content = $@"
        <p style='margin-top:0;'>Dear <strong>{guestName}</strong>,</p>
        <p>Thank you for choosing <strong>Moore Hotels & Suites</strong>. We hope you enjoyed your stay in our <strong>{roomName}</strong> and that everything was to your satisfaction.</p>
        
        <table role='presentation' width='100%' style='margin:25px 0; border:1px solid #E2E8F0; border-radius:12px; padding:20px; background-color:#F8FAFC;'>
            <tr>
                <td>
                    <div style='font-size:11px; color:#64748B; text-transform:uppercase; letter-spacing:1px; margin-bottom:5px;'>Booking Reference</div>
                    <div style='font-size:16px; font-weight:700; color:#1E293B; font-family:monospace;'>{bookingCode}</div>
                </td>
                <td align='right'>
                    <div style='font-size:11px; color:#64748B; text-transform:uppercase; letter-spacing:1px; margin-bottom:5px;'>Stay Location</div>
                    <div style='font-size:16px; font-weight:700; color:#1E293B;'>{roomName}</div>
                </td>
            </tr>
        </table>

        <p style='font-size:14px; color:#475569;'>We would love to hear about your experience. Feel free to leave us a review or contact us directly with any feedback.</p>
        <p style='margin-top:20px; font-weight:600;'>We hope to welcome you back soon!</p>";

        await SendEmailAsync(email, "Thank You", BuildTemplate("We Appreciate Your Visit", content));
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
                    <div style='margin-bottom:20px;'>
                        <span style='font-size:11px; font-weight:bold; color:{accentGreen}; text-transform:uppercase; letter-spacing:1px;'>Assigned Role</span><br/>
                        <span style='font-size:18px; font-weight:600; color:#111827;'>{role}</span>
                    </div>
                    <div>
                        <span style='font-size:11px; font-weight:bold; color:{accentGreen}; text-transform:uppercase; letter-spacing:1px;'>Initial Password</span><br/>
                        <span style='font-size:26px; font-weight:800; color:#111827; font-family: monospace; letter-spacing:2px;'>{tempPassword}</span>
                    </div>
                </td>
            </tr>
        </table>

        <p style='font-size:15px; color:#4A4A4A; line-height:1.6;'>
            For security purposes, please log in to the <a href='https://admin.moorehotelandsuites.com' style='color:{accentGreen}; font-weight:600; text-decoration:none;'>Staff Portal</a> immediately to change your password.
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
    public async Task SendAdminNewBookingAlertAsync(string adminEmail, string guestName, string bookingCode, string roomName, string roomCategory, int capacity, DateTime checkIn, DateTime checkOut, int nights, decimal totalAmount, string guestEmail, string guestPhone)
    {
        var content = $@"
        <p style='margin:0 0 20px 0;'>A new reservation has been confirmed and requires attention from the operations team.</p>
        
        <table role='presentation' width='100%' style='background-color:#F8F9FA; border-radius:12px; border:1px solid #E5E7EB;'>
            <tr>
                <td style='padding:25px;'>
                    <div style='font-size:12px; color:#6B7280; text-transform:uppercase; letter-spacing:1px; margin-bottom:8px;'>Guest Information</div>
                    <div style='font-size:18px; font-weight:700; color:#111827;'>{guestName}</div>
                    <div style='font-size:14px; color:#4B5563; margin-top:4px;'>{guestEmail} | {guestPhone}</div>
                </td>
            </tr>
        </table>

        {GetBookingSummaryHtml(bookingCode, roomName, roomCategory, capacity, checkIn, checkOut, nights, totalAmount)}

        <div style='margin-top:25px; text-align:center;'>
            <a href='https://admin.moorehotelandsuites.com/bookings/{bookingCode}' style='display:inline-block; background-color:#111111; color:#FFFFFF; padding:12px 25px; border-radius:6px; text-decoration:none; font-size:14px; font-weight:600;'>Manage in Portal</a>
        </div>";

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
        <p style='margin:0 0 20px 0;'>A manual refund request has been triggered and requires administrative confirmation.</p>
        
        <table role='presentation' width='100%' style='border:1px solid #FECACA; background-color:#FEF2F2; border-radius:12px; padding:20px;'>
            <tr>
                <td>
                    <div style='font-size:11px; color:#B91C1C; text-transform:uppercase; letter-spacing:1px; margin-bottom:5px;'>Refund Amount</div>
                    <div style='font-size:24px; font-weight:800; color:#991B1B;'>NGN {amount:N2}</div>
                </td>
                <td align='right'>
                    <div style='font-size:11px; color:#B91C1C; text-transform:uppercase; letter-spacing:1px; margin-bottom:5px;'>Booking Code</div>
                    <div style='font-size:16px; font-weight:700; color:#991B1B; font-family:monospace;'>{bookingCode}</div>
                </td>
            </tr>
        </table>

        <div style='margin-top:20px; padding:15px; background-color:#FFFFFF; border:1px solid #FEE2E2; border-radius:8px;'>
            <div style='font-size:12px; color:#6B7280; margin-bottom:4px;'>Guest Details</div>
            <div style='font-size:14px; font-weight:600; color:#111827;'>{guestName}</div>
            <div style='font-size:12px; color:#6B7280; margin-top:10px; margin-bottom:4px;'>Room</div>
            <div style='font-size:14px; font-weight:600; color:#111827;'>{roomName}</div>
        </div>";

        await SendEmailAsync(adminEmail, "Refund Action Required", BuildTemplate("Refund Alert", content, "#E11D48"));
    }

    public async Task SendRefundCompletionNoticeAsync(string email, string guestName, string bookingCode, string roomName, decimal amount, string reference)
    {
        var content = $@"
        <p style='margin-top:0;'>Dear <strong>{guestName}</strong>,</p>
        <p>Your refund for booking <strong>{bookingCode}</strong> (<strong>{roomName}</strong>) has been successfully processed.</p>
        
        <table role='presentation' width='100%' style='margin:25px 0; background-color:#F0FDF4; border:1px solid #BBF7D0; border-radius:12px; padding:20px;'>
            <tr>
                <td>
                    <div style='font-size:12px; color:#166534; text-transform:uppercase; letter-spacing:1px; margin-bottom:5px;'>Amount Refunded</div>
                    <div style='font-size:24px; font-weight:800; color:#14532D;'>NGN {amount:N2}</div>
                </td>
                <td align='right'>
                    <div style='font-size:11px; color:#166534; text-transform:uppercase; letter-spacing:1px; margin-bottom:5px;'>Reference ID</div>
                    <div style='font-size:14px; font-family:monospace; color:#14532D;'>{reference}</div>
                </td>
            </tr>
        </table>

        <p style='font-size:14px; color:#4B5563;'>The funds should reflect in your account within 3-5 business days. We apologize for any inconvenience caused and hope to serve you again in the future.</p>";

        await SendEmailAsync(email, "Refund Completed", BuildTemplate("Refund Successful", content, "#16A34A"));
    }
}
