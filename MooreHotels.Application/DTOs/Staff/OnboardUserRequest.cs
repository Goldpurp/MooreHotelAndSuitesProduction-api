using MooreHotels.Domain.Enums;

namespace MooreHotels.Application.DTOs;

public record OnboardUserRequest(
    string FullName,
    string Email,
    string TemporaryPassword,
    UserRole AssignedRole,
    ProfileStatus Status,
    string? Department = null,
    string? Phone = null);