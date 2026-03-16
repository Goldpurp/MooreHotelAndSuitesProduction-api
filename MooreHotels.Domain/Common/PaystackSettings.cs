namespace MooreHotels.Domain.Common;

public class PaystackSettings
{
    public string SecretKey { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = "https://api.paystack.co";
}
