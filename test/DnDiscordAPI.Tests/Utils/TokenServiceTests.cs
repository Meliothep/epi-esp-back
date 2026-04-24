using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using DnDiscordAPI.Auth.Services;
using Microsoft.Extensions.Configuration;

namespace DnDiscordAPI.Tests.Utils;

public sealed class TokenServiceTests
{
    private static IConfiguration CreateConfig(
        string secretKey = "test-secret-key-that-is-at-least-32-bytes-long!!",
        string expirationMinutes = "60",
        string issuer = "test-issuer",
        string audience = "test-audience")
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:SecretKey"] = secretKey,
                ["Jwt:ExpirationMinutes"] = expirationMinutes,
                ["Jwt:Issuer"] = issuer,
                ["Jwt:Audience"] = audience
            })
            .Build();
    }

    private static TokenService CreateService(string expirationMinutes = "60")
        => new(CreateConfig(expirationMinutes: expirationMinutes));

    [Fact]
    public void GenerateToken_ReturnsValidJwt()
    {
        var service = CreateService();
        var token = service.GenerateToken("user1", "TestUser", "test@example.com");

        Assert.False(string.IsNullOrEmpty(token));
        var handler = new JwtSecurityTokenHandler();
        Assert.True(handler.CanReadToken(token));
    }

    [Fact]
    public void GenerateToken_ContainsExpectedClaims()
    {
        var service = CreateService();
        var token = service.GenerateToken("user1", "TestUser", "test@example.com");

        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(token);

        Assert.Equal("user1", jwt.Claims.First(c => c.Type == "sub").Value);
        Assert.Equal("TestUser", jwt.Claims.First(c => c.Type == "username").Value);
        Assert.Equal("test@example.com", jwt.Claims.First(c => c.Type == "email").Value);
        Assert.Contains(jwt.Claims, c =>
            c.Type == ClaimTypes.NameIdentifier && c.Value == "user1");
    }

    [Fact]
    public void GenerateToken_WithAvatar_ContainsAvatarClaim()
    {
        var service = CreateService();
        var token = service.GenerateToken("user1", "TestUser", "test@example.com", "avatar_hash");

        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(token);

        Assert.Equal("avatar_hash", jwt.Claims.First(c => c.Type == "avatar").Value);
    }

    [Fact]
    public void GenerateToken_WithoutAvatar_NoAvatarClaim()
    {
        var service = CreateService();
        var token = service.GenerateToken("user1", "TestUser", "test@example.com");

        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(token);

        Assert.DoesNotContain(jwt.Claims, c => c.Type == "avatar");
    }

    [Fact]
    public void ValidateToken_ValidToken_ReturnsPrincipal()
    {
        var service = CreateService();
        var token = service.GenerateToken("user1", "TestUser", "test@example.com");

        var principal = service.ValidateToken(token);

        Assert.NotNull(principal);
        // After validation, "sub" is mapped to NameIdentifier by the JWT handler
        Assert.Contains(principal.Claims,
            c => c.Type == ClaimTypes.NameIdentifier && c.Value == "user1");
    }

    [Fact]
    public void ValidateToken_InvalidToken_ReturnsNull()
    {
        var service = CreateService();
        var result = service.ValidateToken("this-is-not-a-valid-jwt-token");
        Assert.Null(result);
    }

    [Fact]
    public void ValidateToken_ExpiredToken_ReturnsNull()
    {
        // Use a large negative value to exceed the default 5-minute clock skew tolerance
        var expiredService = CreateService(expirationMinutes: "-10");
        var token = expiredService.GenerateToken("user1", "TestUser", "test@example.com");

        // Validate with a normal service (same key)
        var normalService = CreateService();
        var result = normalService.ValidateToken(token);

        Assert.Null(result);
    }

    [Fact]
    public void ValidateToken_WrongKey_ReturnsNull()
    {
        var service1 = new TokenService(CreateConfig(
            secretKey: "test-secret-key-that-is-at-least-32-bytes-long!!"));
        var service2 = new TokenService(CreateConfig(
            secretKey: "different-secret-key-also-at-least-32-bytes-long!"));

        var token = service1.GenerateToken("user1", "TestUser", "test@example.com");
        var result = service2.ValidateToken(token);

        Assert.Null(result);
    }

    [Fact]
    public void Constructor_MissingSecretKey_Throws()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();

        Assert.Throws<InvalidOperationException>(() => new TokenService(config));
    }
}
