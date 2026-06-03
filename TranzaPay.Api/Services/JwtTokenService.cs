using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using TranzaPay.Application.Dtos;
using TranzaPay.Application.Interfaces;
using TranzaPay.Application.Options;
using TranzaPay.Domain.Entities;

namespace TranzaPay.Api.Services;

public sealed class JwtTokenService(IOptions<JwtSettings> options) : ITokenService
{
    private readonly JwtSettings _settings = options.Value;

    public AuthResponse CreateToken(User user)
    {
        var expiresAt = DateTime.UtcNow.AddMinutes(_settings.ExpirationMinutes);
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.Name, user.FullName),
            new(ClaimTypes.Role, user.Role.ToString())
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_settings.Secret));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: _settings.Issuer,
            audience: _settings.Audience,
            claims: claims,
            expires: expiresAt,
            signingCredentials: credentials);

        return new AuthResponse(
            new JwtSecurityTokenHandler().WriteToken(token),
            expiresAt,
            new UserDto(user.Id, user.FullName, user.Email, user.Role));
    }
}
