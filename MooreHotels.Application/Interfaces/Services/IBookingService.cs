using MooreHotels.Application.DTOs;
using MooreHotels.Domain.Enums;

namespace MooreHotels.Application.Interfaces.Services;

public interface IBookingService
{
    Task<BookingDto> CreateBookingAsync(CreateBookingRequest request);
    Task<BookingDto?> GetBookingByCodeAsync(string code);
    Task<IEnumerable<BookingDto>> GetAllBookingsAsync();
    Task<BookingDto> UpdateStatusAsync(Guid bookingId, BookingStatus status, Guid userId);
    Task<BookingDto> ProcessPaymentSuccessAsync(string bookingCode, string reference, Guid? actingUserId = null);
}