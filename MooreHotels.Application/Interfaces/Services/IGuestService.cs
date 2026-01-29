using MooreHotels.Application.DTOs;

namespace MooreHotels.Application.Interfaces.Services;

public interface IGuestService
{
    Task<IEnumerable<GuestDto>> GetAllGuestsAsync();
    Task<IEnumerable<GuestDto>> SearchGuestsAsync(string term);
    Task<GuestDto?> GetGuestByIdAsync(string id);
    Task<GuestDto?> GetGuestByEmailAsync(string email);
    Task SetVipStatusAsync(string id, bool isVip);
}