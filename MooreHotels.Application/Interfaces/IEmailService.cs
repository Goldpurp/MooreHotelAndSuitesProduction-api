namespace MooreHotels.Application.Interfaces;

public interface IEmailService
{
    // Guest Communications
    Task SendBookingConfirmationAsync(string email, string guestName, string bookingCode, string roomName, DateTime checkIn);
    Task SendCancellationNoticeAsync(string email, string guestName, string bookingCode);
    Task SendCheckInReminderAsync(string email, string guestName, string bookingCode, DateTime checkIn);
    Task SendTemporaryPasswordAsync(string email, string guestName, string tempPassword);
    Task SendEmailVerificationAsync(string email, string name, string link);
    Task SendPaymentSuccessAsync(string email, string guestName, string bookingCode, decimal amount, string reference);
    Task SendCheckOutThankYouAsync(string email, string guestName, string bookingCode);

    // Administrative / Staff Communications
    Task SendAdminNewBookingAlertAsync(string adminEmail, string guestName, string bookingCode, string roomName, decimal amount);
    Task SendStaffWelcomeEmailAsync(string email, string name, string tempPassword, string role);
    
    // Security / Lifecycle Communications
    Task SendAccountSuspendedAsync(string email, string name, string? reason = null);
    Task SendAccountActivatedAsync(string email, string name);
}