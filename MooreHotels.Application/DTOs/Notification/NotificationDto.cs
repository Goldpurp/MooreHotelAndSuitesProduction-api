namespace MooreHotels.Application.DTOs;

public record NotificationDto(
    Guid Id,
    string Title,
    string Message,
    string? BookingCode,
    bool IsRead,
    DateTime CreatedAt);