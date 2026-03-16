using MooreHotels.Domain.Enums;

namespace MooreHotels.Application.Interfaces.Services;

public interface IPaymentService
{
    Task<string> InitializePaymentAsync(string email, decimal amount, string bookingCode, string callbackUrl);
    Task<bool> VerifyPaystackPaymentAsync(string reference);
    string GetTransferInstructions();
}