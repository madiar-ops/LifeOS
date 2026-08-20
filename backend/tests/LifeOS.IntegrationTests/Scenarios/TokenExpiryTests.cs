using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using FluentAssertions;
using LifeOS.IntegrationTests.Infrastructure;
using Microsoft.IdentityModel.Tokens;

namespace LifeOS.IntegrationTests.Scenarios;

/// <summary>
/// Поведение API при истёкшем access-токене.
///
/// Фронтенд различает два случая по заголовку <c>X-Token-Expired</c>:
/// «токен протух» — надо тихо обновить пару токенов и повторить запрос;
/// «токен неверен» — надо разлогинить пользователя. Без этого заголовка
/// любой 401 приводил бы к выбросу пользователя из приложения раз в 15 минут.
/// Ждать реального истечения токена не нужно: он выписывается тем же ключом
/// сразу «задним числом».
/// </summary>
[Collection(ApiCollection.Name)]
public class TokenExpiryTests
{
    private readonly ApiFixture _api;

    public TokenExpiryTests(ApiFixture api) => _api = api;

    [Fact]
    public async Task Истёкший_токен_помечается_заголовком_X_Token_Expired()
    {
        var user = await _api.CreateAuthenticatedUserAsync();
        var client = _api.CreateClient();

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", CreateExpiredToken(user.Id, user.Email));

        var response = await client.GetAsync("/api/auth/me");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        response.Headers.Contains("X-Token-Expired").Should().BeTrue();
        response.Headers.GetValues("X-Token-Expired").Should().Contain("true");
    }

    [Fact]
    public async Task Токен_подписанный_чужим_ключом_не_помечается_как_истёкший()
    {
        var client = _api.CreateClient();
        var foreignKey = "полностью-другой-ключ-подписи-32-символа";

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", CreateToken(Guid.NewGuid(), "чужой@lifeos.test", DateTime.UtcNow.AddMinutes(15), foreignKey));

        var response = await client.GetAsync("/api/auth/me");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        // Здесь фронтенд обязан разлогинить пользователя, а не пытаться
        // обновить токен: подделку обновление не исправит.
        response.Headers.Contains("X-Token-Expired").Should().BeFalse();
    }

    [Fact]
    public async Task Запас_на_рассинхрон_часов_обнулён()
    {
        var user = await _api.CreateAuthenticatedUserAsync();
        var client = _api.CreateClient();

        // Токен просрочен всего на минуту. ASP.NET по умолчанию прощает
        // расхождение часов в 5 минут — для 15-минутного токена это треть
        // срока жизни, поэтому ClockSkew выставлен в ноль.
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            CreateToken(user.Id, user.Email, DateTime.UtcNow.AddMinutes(-1), ApiFixture.SigningKey));

        var response = await client.GetAsync("/api/auth/me");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private static string CreateExpiredToken(Guid userId, string email)
        => CreateToken(userId, email, DateTime.UtcNow.AddMinutes(-30), ApiFixture.SigningKey);

    /// <summary>
    /// Собирает токен теми же издателем, аудиторией и набором claim'ов,
    /// что и <c>JwtTokenGenerator</c> — иначе тест провалился бы по причине,
    /// не имеющей отношения к сроку действия.
    /// </summary>
    private static string CreateToken(Guid userId, string email, DateTime expiresAt, string signingKey)
    {
        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: "LifeOS.API",
            audience: "LifeOS.Client",
            claims: new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
                new Claim(JwtRegisteredClaimNames.Email, email),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new Claim(ClaimTypes.Role, "User"),
                new Claim(ClaimTypes.NameIdentifier, userId.ToString())
            },
            notBefore: expiresAt.AddMinutes(-15),
            expires: expiresAt,
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
