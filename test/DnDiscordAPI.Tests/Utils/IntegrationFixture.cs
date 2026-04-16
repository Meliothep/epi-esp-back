
using System.Security.Claims;
using System.Text.Encodings.Web;
using DnDiscord.Campaign.DataAccess;
using DnDiscordAPI.Games.Database;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Testcontainers.PostgreSql;

namespace DnDiscordAPI.Tests.Utils;

public abstract class IntegrationFixture<TEntryPoint> : IAsyncLifetime where TEntryPoint : class
{
    public virtual string ProjectName { get { return "DnDiscord"; } }

    private readonly PostgreSqlContainer _dbContainer;

    public IntegrationFixture()
    {
        _dbContainer = new PostgreSqlBuilder()
            .WithImage("postgres:16")
            .WithDatabase(ProjectName + "DB")
            .WithUsername(ProjectName)
            .WithPassword(ProjectName + "Secured")
            .Build();
    }

    public WebApplicationFactory<TEntryPoint>? factory;

    public async Task InitializeAsync()
    {
        await _dbContainer.StartAsync();

        var connectionString = _dbContainer.GetConnectionString();

        factory = new WebApplicationFactory<TEntryPoint>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseSetting("ConnectionStrings:DefaultConnection", connectionString);
                builder.UseSetting("ConnectionStrings:gamesdb", connectionString);

                builder.ConfigureTestServices(services =>
                {
                    // Replace DbContext registrations to ensure Testcontainer connection
                    services.RemoveAll<DbContextOptions<CampaignDbContext>>();
                    services.RemoveAll<DbContextOptions<GamesDbContext>>();
                    services.AddDbContext<CampaignDbContext>(options =>
                        options.UseNpgsql(connectionString, npgsql =>
                            npgsql.MigrationsHistoryTable("__EFMigrationsHistory_Campaign")));
                    services.AddDbContext<GamesDbContext>(options =>
                        options.UseNpgsql(connectionString));

                    services.AddAuthentication("Test")
                        .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>("Test", _ => { });

                    services.PostConfigure<AuthenticationOptions>(options =>
                    {
                        options.DefaultAuthenticateScheme = "Test";
                        options.DefaultChallengeScheme = "Test";
                    });
                });
            });
    }

    public async Task DisposeAsync()
    {
        if (factory is not null)
            await factory.DisposeAsync();
        await _dbContainer.DisposeAsync();
    }

    /// <summary>
    /// Creates an HttpClient with a fake authenticated user (discord ID claim).
    /// </summary>
    public HttpClient CreateAuthenticatedClient(string discordUserId = "123456789012345678")
    {
        var client = factory!.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-UserId", discordUserId);
        return client;
    }
}

/// <summary>
/// Test auth handler that authenticates every request with a fake discord user.
/// If the request has X-Test-UserId header, that value is used as the sub claim.
/// If no header, the request is treated as unauthenticated.
/// </summary>
public class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public TestAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder) { }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        // Check for the test user header -- if absent, treat as unauthenticated
        if (!Request.Headers.TryGetValue("X-Test-UserId", out var userIdValues))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var userId = userIdValues.FirstOrDefault();
        if (string.IsNullOrEmpty(userId))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var claims = new[]
        {
            new Claim("sub", userId),
            new Claim(ClaimTypes.NameIdentifier, userId),
            new Claim(ClaimTypes.Name, "TestUser"),
        };
        var identity = new ClaimsIdentity(claims, "Test");
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, "Test");

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
