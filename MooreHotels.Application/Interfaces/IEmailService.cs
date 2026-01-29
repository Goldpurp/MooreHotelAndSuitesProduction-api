namespace MooreHotels.Application.Interfaces;

public interface IEmailService
{
    Task SendBookingConfirmationAsync(string email, string guestName, string bookingCode, string roomName, DateTime checkIn);
    Task SendCancellationNoticeAsync(string email, string guestName, string bookingCode);
    Task SendCheckInReminderAsync(string email, string guestName, string bookingCode, DateTime checkIn);
    Task SendTemporaryPasswordAsync(string email, string guestName, string tempPassword);
}