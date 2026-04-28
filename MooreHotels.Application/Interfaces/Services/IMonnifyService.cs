namespace MooreHotels.Application.Interfaces.Services;

public interface IMonnifyService
{
    Task<string> InitializeMonnifyPaymentAsync(string email, string name, decimal amount, string bookingCode, string callbackUrl);
    Task<bool> VerifyTransactionAsync(string transactionReference);
    Task<string> GetAccessTokenAsync();
}
