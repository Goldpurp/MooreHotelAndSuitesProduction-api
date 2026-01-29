namespace MooreHotels.Application.DTOs;

public record PaymentCardDto(
    Guid Id,
    string CardholderName,
    string MaskedNumber,
    string Cvv,
    string Expiry,
    bool IsDefault);