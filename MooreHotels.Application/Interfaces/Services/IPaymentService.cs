using MooreHotels.Domain.Enums;

namespace MooreHotels.Application.Interfaces.Services;

public interface IPaymentService
{
    string GeneratePaystackLink(string bookingCode, decimal amount, string email);
    Task<bool> VerifyPaystackPaymentAsync(string reference);
    string GetTransferInstructions();
}