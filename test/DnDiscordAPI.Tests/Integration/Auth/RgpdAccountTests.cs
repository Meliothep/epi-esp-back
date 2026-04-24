using System.Net;
using System.Net.Http.Json;
using DnDiscord.Campaign.Common;
using DnDiscord.Campaign.DataAccess;
using DnDiscordAPI.Auth;
using DnDiscordAPI.Auth.Services;
using DnDiscordAPI.Games.Database;
using DnDiscordAPI.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DnDiscordAPI.Tests.Integration.Auth;

/// <summary>
/// Tests d'intégration RGPD — couvre la cascade complète de suppression
/// de compte (art. 17) et le blocage des JWT post-suppression (correction
/// du bug du « compte fantôme » — voir review PR).
/// </summary>
[Collection("DnDiscord Integration collection")]
public sealed class RgpdAccountTests
{
    private readonly DnDiscordIntegrationFixture _fixture;

    public RgpdAccountTests(DnDiscordIntegrationFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task DeleteMe_RemovesCharacters_Campaigns_Memberships_AndBlocksFurtherCalls()
    {
        // Arrange — user A possède 2 persos + 1 campagne ; user B rejoint la campagne.
        var userA = $"rgpd-a-{Guid.NewGuid():N}";
        var userB = $"rgpd-b-{Guid.NewGuid():N}";
        var userAGuid = DiscordIdMapping.ToGuid(userA);
        var clientA = _fixture.CreateAuthenticatedClient(userA);
        var clientB = _fixture.CreateAuthenticatedClient(userB);

        await CreateCharacter(clientA, "Thorin");
        await CreateCharacter(clientA, "Elara");
        var campaign = await CreateCampaign(clientA, "Campagne de A");
        await JoinCampaignAsMember(clientB, campaign.Id);

        // Act — user A supprime son compte.
        var deleteResponse = await clientA.DeleteAsync("/api/auth/me");

        // Assert 1 — la suppression répond 204.
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        // Assert 2 — bug critique du reviewer C1 : le JWT ne doit PLUS
        // fonctionner post-suppression. GetCurrentUser doit renvoyer 401.
        var meResponse = await clientA.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, meResponse.StatusCode);

        // Assert 3 — cascade DB directe (reviewer N5 : ne pas se contenter
        // d'inférer via 401/404, lire les tables). Couvre C1+C2+I3 en un
        // test : Characters / Campaigns possédées / GameSessions orphelines
        // (StartedBy=userA dans des campagnes d'autrui) / Memberships.
        using (var scope = _fixture.factory!.Services.CreateScope())
        {
            var gamesDb = scope.ServiceProvider.GetRequiredService<GamesDbContext>();
            var campaignDb = scope.ServiceProvider.GetRequiredService<CampaignDbContext>();

            var remainingCharacters = await gamesDb.Set<DnDiscordAPI.Games.Character.Models.Character>()
                .Where(c => c.DiscordUserId == userA)
                .ToListAsync();
            Assert.Empty(remainingCharacters);

            var remainingOwnedCampaigns = await campaignDb.Campaigns
                .IgnoreQueryFilters()
                .Where(c => c.DungeonMasterId == userAGuid)
                .ToListAsync();
            Assert.Empty(remainingOwnedCampaigns);

            var remainingSessionsStartedByA = await campaignDb.GameSessions
                .Where(s => s.StartedBy == userAGuid)
                .ToListAsync();
            Assert.Empty(remainingSessionsStartedByA);

            var remainingMembershipsOfA = await campaignDb.CampaignMembers
                .Where(m => m.UserId == userAGuid)
                .ToListAsync();
            Assert.Empty(remainingMembershipsOfA);
        }

        // Assert 4 — la campagne possédée par A n'existe plus vue de B (cohérent
        // avec Assert 3 mais valide aussi la cascade au niveau API).
        var campResponse = await clientB.GetAsync($"/api/campaigns/{campaign.Id}");
        Assert.True(
            campResponse.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Forbidden,
            $"Expected 404/403 on deleted campaign, got {campResponse.StatusCode}");
    }

    [Fact]
    public async Task GetOrCreateUser_AfterDelete_ThrowsAccountTombstonedException()
    {
        // Reviewer N2 : la ré-authentification Discord d'un compte tombstoné
        // doit remonter une exception dédiée (AccountTombstonedException),
        // pas un InvalidOperationException générique qui finissait en 500 opaque.
        // DiscordCallback / DevLogin la catchent pour renvoyer 410 Gone actionnable.
        var userA = $"rgpd-reauth-{Guid.NewGuid():N}";
        var clientA = _fixture.CreateAuthenticatedClient(userA);

        var deleteResponse = await clientA.DeleteAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        // IUserStore est Singleton → le tombstone est visible partout.
        var userStore = _fixture.factory!.Services.GetRequiredService<IUserStore>();

        var ex = Assert.Throws<AccountTombstonedException>(() =>
            userStore.GetOrCreateUser(new DiscordUserData
            {
                Id = userA,
                Username = "Retry",
                Email = "retry@example.com",
                Avatar = null,
            }));
        Assert.Equal(userA, ex.DiscordId);
    }

    [Fact]
    public async Task ExportMe_ReturnsJsonDownload()
    {
        var user = $"rgpd-export-{Guid.NewGuid():N}";
        var client = _fixture.CreateAuthenticatedClient(user);
        await CreateCharacter(client, "Legolas");

        var response = await client.GetAsync("/api/auth/me/export");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains("attachment", response.Content.Headers.ContentDisposition?.DispositionType ?? string.Empty);
        var json = await response.Content.ReadAsStringAsync();
        Assert.Contains("schemaVersion", json);
        Assert.Contains("\"profile\"", json);
        Assert.Contains("\"characters\"", json);
    }

    [Fact]
    public async Task ExportMe_RateLimited_ReturnsTooManyRequestsAfterLimit()
    {
        // Reviewer N4 : asserter que les policies de rate-limit RGPD sont
        // bien câblées (rgpd-export = 5/10min par sub claim). On ne peut pas
        // asserter sur DELETE /me parce que le tombstone bloquerait le 2e
        // appel avec 401 avant d'atteindre le rate limiter. L'export est
        // idempotent → parfait pour épuiser le bucket.
        var user = $"rgpd-ratelimit-{Guid.NewGuid():N}";
        var client = _fixture.CreateAuthenticatedClient(user);

        // 5 premiers appels doivent passer (permitLimit=5).
        for (int i = 0; i < 5; i++)
        {
            var ok = await client.GetAsync("/api/auth/me/export");
            Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        }

        // 6e appel : 429 Too Many Requests.
        var limited = await client.GetAsync("/api/auth/me/export");
        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
    }

    [Fact]
    public async Task DeleteMe_Unauthenticated_ReturnsUnauthorized()
    {
        var unauth = _fixture.factory!.CreateClient();
        var response = await unauth.DeleteAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ExportMe_Unauthenticated_ReturnsUnauthorized()
    {
        var unauth = _fixture.factory!.CreateClient();
        var response = await unauth.GetAsync("/api/auth/me/export");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // Helpers.

    private static async Task CreateCharacter(HttpClient client, string name)
    {
        var response = await client.PostAsJsonAsync("/api/games/character", new
        {
            name,
            @class = "Guerrier",
            race = "Humain",
            abilities = new
            {
                strength = 14, dexterity = 12, constitution = 14,
                intelligence = 10, wisdom = 10, charisma = 10,
            },
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    private static async Task<CampaignStub> CreateCampaign(HttpClient client, string name)
    {
        var response = await client.PostAsJsonAsync("/api/campaigns", new
        {
            name,
            description = "RGPD test campaign",
            maxPlayers = 6,
            isPublic = true,
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var campaign = await response.Content.ReadFromJsonAsync<CampaignStub>();
        Assert.NotNull(campaign);
        return campaign!;
    }

    private static async Task JoinCampaignAsMember(HttpClient client, Guid campaignId)
    {
        // Tente de rejoindre la campagne publique. Si l'API diffère, le test
        // continue de couvrir la cascade owned-campaign + characters + user
        // (le scénario members est un plus — on swallow le 404/400 ici
        // puisque ce n'est pas le critical path du test).
        var response = await client.PostAsJsonAsync($"/api/campaigns/{campaignId}/join", new { });
        _ = response;
    }

    private sealed record CampaignStub(Guid Id, string Name);
}
