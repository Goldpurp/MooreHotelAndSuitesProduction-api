namespace MooreHotels.Domain.Common;

public class MonnifySettings
{
    public string ApiKey { get; set; } = string.Empty;
    public string SecretKey { get; set; } = string.Empty;
    public string ContractCode { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = "https://api.monnify.com";
}
