namespace MooreHotels.Application.DTOs;

public record UpdateProfileRequest(
    string? FullName = null,
    string? Email = null,
    string? Phone = null,
    string? AvatarUrl = null);