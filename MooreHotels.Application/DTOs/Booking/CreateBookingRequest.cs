using MooreHotels.Domain.Enums;

namespace MooreHotels.Application.DTOs;

public record CreateBookingRequest(
    Guid RoomId, 
    string GuestFirstName, 
    string GuestLastName, 
    string GuestEmail, 
    string GuestPhone, 
    DateTime CheckIn, 
    DateTime CheckOut, 
    PaymentMethod PaymentMethod,
    string? Notes);