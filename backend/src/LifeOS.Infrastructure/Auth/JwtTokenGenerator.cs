using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using LifeOS.Application.Common;
using LifeOS.Application.Interfaces.Auth;
using LifeOS.Application.Interfaces.Infrastructure;
using LifeOS.Domain.Entities;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace LifeOS.Infrastructure.Auth;

public class JwtTokenGenerator : IJwtTokenGenerator
{
    private readonly JwtSettings _settings;
    private readonly IDateTimeProvider _dateTime;

    public JwtTokenGenerator(IOptions<JwtSettings> settings, IDateTimeProvider dateTime)
    {
        _settings = settings.Value;
        _dateTime = dateTime;
    }

    public (string Token, DateTime ExpiresAt) GenerateAccessToken(User user)
    {
        var expiresAt = _dateTime.UtcNow.AddMinutes(_settings.AccessTokenMinutes);

        var claims = new List<Claim>
        {
            // Sub — стандартный claim идентификатора субъекта.
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email),

            // Jti делает каждый токен уникальным — пригодится, если позже
            // понадобится чёрный список отозванных access-токенов.
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),

            // ClaimTypes.Role — именно это имя читает [Authorize(Roles = "...")].
            new(ClaimTypes.Role, user.Role.ToString()),

            // Дублируем идентификатор в NameIdentifier: часть библиотек ASP.NET
            // ищет пользователя именно там.
            new(ClaimTypes.NameIdentifier, user.Id.ToString())
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_settings.Key));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _settings.Issuer,
            audience: _settings.Audience,
            claims: claims,
            notBefore: _dateTime.UtcNow,
            expires: expiresAt,
            signingCredentials: credentials);

        return (new JwtSecurityTokenHandler().WriteToken(token), expiresAt);
    }

    /// <summary>
    /// 64 байта из криптографического генератора. Base64Url — чтобы токен
    /// безопасно передавался в JSON и URL без экранирования.
    /// </summary>
    public string GenerateRefreshToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(64);
        return Base64UrlEncoder.Encode(bytes);
    }
}
