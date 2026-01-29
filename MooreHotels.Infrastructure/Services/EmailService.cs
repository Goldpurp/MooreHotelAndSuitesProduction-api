using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MooreHotels.Application.Interfaces;
using MailKit.Net.Smtp;
using MimeKit;

namespace MooreHotels.Infrastructure.Services;

public class EmailService : IEmailService
{
    private readonly IConfiguration _config;
    private readonly ILogger<EmailService> _logger;

    public EmailService(IConfiguration config, ILogger<EmailService> logger)
    {
        _config = config;
        _logger = logger;
    }

    private async Task SendEmailAsync(string toEmail, string subject, string body)
    {
        try
        {
            var email = new MimeMessage();
            email.From.Add(new MailboxAddress(_config["EmailSettings:SenderName"], _config["EmailSettings:SenderEmail"]));
            email.To.Add(MailboxAddress.Parse(toEmail));
            email.Subject = subject;

            var builder = new BodyBuilder { HtmlBody = body };
            email.Body = builder.ToMessageBody();

            using var smtp = new SmtpClient();
            await smtp.ConnectAsync(_config["EmailSettings:SmtpServer"], int.Parse(_config["EmailSettings:SmtpPort"] ?? "587"), MailKit.Security.SecureSocketOptions.StartTls);
            await smtp.AuthenticateAsync(_config["EmailSettings:SmtpUser"], _config["EmailSettings:SmtpPass"]);
            await smtp.SendAsync(email);
            await smtp.DisconnectAsync(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email to {Email}", toEmail);
        }
    }

    public async Task SendBookingConfirmationAsync(string email, string guestName, string bookingCode, string roomName, DateTime checkIn)
    {
        var subject = $"Booking Confirmation - {bookingCode}";
        var body = $@"
            <div style='font-family: Arial, sans-serif; color: #333;'>
                <h1>Hello {guestName},</h1>
                <p>Your booking at Moore Hotels & Suites is confirmed!</p>
                <p><strong>Booking Code:</strong> {bookingCode}</p>
                <p><strong>Room:</strong> {roomName}</p>
                <p><strong>Check-in Date:</strong> {checkIn:MMM dd, yyyy}</p>
                <p>We look forward to hosting you.</p>
            </div>";
        await SendEmailAsync(email, subject, body);
    }

    public async Task SendCancellationNoticeAsync(string email, string guestName, string bookingCode)
    {
        var subject = $"Booking Cancellation - {bookingCode}";
        var body = $@"
            <div style='font-family: Arial, sans-serif; color: #333;'>
                <h1>Hello {guestName},</h1>
                <p>Your booking with code <strong>{bookingCode}</strong> has been cancelled.</p>
                <p>If this was a mistake, please contact our support team immediately.</p>
            </div>";
        await SendEmailAsync(email, subject, body);
    }

    public async Task SendCheckInReminderAsync(string email, string guestName, string bookingCode, DateTime checkIn)
    {
        var subject = $"Reminder: Your Stay Starts Tomorrow! - {bookingCode}";
        var body = $@"
            <div style='font-family: Arial, sans-serif; color: #333;'>
                <h1>Hello {guestName},</h1>
                <p>This is a friendly reminder of your upcoming stay with us starting tomorrow, {checkIn:MMM dd, yyyy}.</p>
                <p>Safe travels!</p>
            </div>";
        await SendEmailAsync(email, subject, body);
    }

    public async Task SendTemporaryPasswordAsync(string email, string guestName, string tempPassword)
    {
        var subject = "Temporary Access Password - Moore Hotels & Suites";
        var body = $@"
            <div style='font-family: Arial, sans-serif; color: #333;'>
                <h1>Hello {guestName},</h1>
                <p>You have requested a password reset for your account at Moore Hotels & Suites.</p>
                <p>Please use the following temporary password to log in:</p>
                <div style='background: #f4f4f4; padding: 15px; border-radius: 8px; font-size: 20px; font-weight: bold; text-align: center; margin: 20px 0;'>
                    {tempPassword}
                </div>
                <p><strong>Important:</strong> After logging in, we strongly recommend changing this to your preferred password immediately via your profile settings.</p>
                <p>If you did not request this reset, please contact support.</p>
            </div>";
        await SendEmailAsync(email, subject, body);
    }
}