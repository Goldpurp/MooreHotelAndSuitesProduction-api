using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using MooreHotels.Domain.Entities;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace MooreHotels.Infrastructure.Identity;

public interface IJwtService
{
    string GenerateToken(ApplicationUser user);
}

public class JwtService : IJwtService
{
    private readonly IConfiguration _config;

    public JwtService(IConfiguration config)
    {
        _config = config;
    }

    public string GenerateToken(ApplicationUser user)
    {
        var secretKey = _config["Jwt:Key"] ?? "MooreHotelsSuperSecretKey2024!AtLeast32CharactersForSecurityPurpose";
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        // Using standard ClaimTypes for reliable mapping in Identity middleware
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Email, user.Email ?? ""),
            new Claim(ClaimTypes.Name, user.Name),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim(ClaimTypes.Role, user.Role.ToString()), // Standard Role Claim
            new Claim("role", user.Role.ToString()) // Redundant short-form for client-side ease
        };

        var token = new JwtSecurityToken(
            issuer: "MooreHotels",
            audience: "MooreHotels_Clients",
            claims: claims,
            expires: DateTime.UtcNow.AddDays(1),
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}