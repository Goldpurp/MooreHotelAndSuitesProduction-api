namespace MooreHotels.Application.DTOs;

public record RotateCredentialsRequest(
    string OldPassword,
    string NewPassword,
    string ConfirmNewPassword);