using Microsoft.EntityFrameworkCore;
using MooreHotels.Application.Interfaces.Repositories;
using MooreHotels.Domain.Entities;
using MooreHotels.Infrastructure.Persistence;

namespace MooreHotels.Infrastructure.Repositories;

public class GuestRepository : IGuestRepository
{
    private readonly MooreHotelsDbContext _db;
    public GuestRepository(MooreHotelsDbContext db) => _db = db;

    public async Task<Guest?> GetByIdAsync(string id) => await _db.Guests.FindAsync(id);
    
    public async Task<Guest?> GetByEmailAsync(string email) => await _db.Guests.FirstOrDefaultAsync(g => g.Email == email);

    public async Task<IEnumerable<Guest>> GetAllAsync() => await _db.Guests.ToListAsync();

    public async Task<IEnumerable<Guest>> SearchAsync(string term)
    {
        if (string.IsNullOrWhiteSpace(term)) return await GetAllAsync();

        var t = term.ToLower();
        return await _db.Guests
            .Where(g => g.Id.ToLower().Contains(t) || 
                        g.FirstName.ToLower().Contains(t) || 
                        g.LastName.ToLower().Contains(t) || 
                        g.Email.ToLower().Contains(t) || 
                        g.Phone.Contains(t))
            .ToListAsync();
    }

    public async Task AddAsync(Guest guest)
    {
        await _db.Guests.AddAsync(guest);
        await _db.SaveChangesAsync();
    }

    public async Task UpdateAsync(Guest guest)
    {
        _db.Guests.Update(guest);
        await _db.SaveChangesAsync();
    }
}