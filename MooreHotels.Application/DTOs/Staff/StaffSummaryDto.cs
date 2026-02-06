using MooreHotels.Domain.Enums;

namespace MooreHotels.Application.DTOs;

public record StaffSummaryDto(
    Guid Id,
    string Name,
    string Email,
    string? Phone,
    string? AvatarUrl,
    UserRole Role,
    string? Department,
    DateTime OnboardingDate,
    ProfileStatus Status);