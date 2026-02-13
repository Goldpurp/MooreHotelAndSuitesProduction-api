using MooreHotels.Application.DTOs;
using MooreHotels.Domain.Enums;

namespace MooreHotels.Application.Interfaces.Services;

public interface IBookingService
{
    Task<BookingDto> CreateBookingAsync(CreateBookingRequest request);
    Task<BookingDto?> GetBookingByCodeAsync(string code);
    Task<BookingDto?> GetBookingByCodeAndEmailAsync(string code, string email);
    Task<IEnumerable<BookingDto>> GetAllBookingsAsync();
    Task<BookingDto> UpdateStatusAsync(Guid bookingId, BookingStatus status, Guid userId);
    Task<BookingDto> ProcessPaymentSuccessAsync(string bookingCode, string reference, Guid? actingUserId = null);
     Task<BookingDto> CancelBookingAsync(Guid bookingId, Guid userId, string? reason = null);
     Task<BookingDto> CancelBookingByGuestAsync(string bookingCode, string email, string? reason = null);
     Task<BookingDto> CompleteRefundAsync(Guid bookingId, string transactionRef, Guid adminId);
     Task<IEnumerable<BookingDto>> GetPendingRefundsAsync();
}