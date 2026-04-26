using System.Net;
using System.Net.Http.Json;

namespace DnDiscordAPI.Tests.Integration.Character;

[Collection("DnDiscord Integration collection")]
public sealed class CharacterCrudTests
{
    private readonly DnDiscordIntegrationFixture _fixture;
    private readonly HttpClient _client;

    public CharacterCrudTests(DnDiscordIntegrationFixture fixture)
    {
        _fixture = fixture;
        _client = fixture.CreateAuthenticatedClient();
    }

    [Fact]
    public async Task CreateCharacter_WithValidData_ReturnsCreated()
    {
        var request = new
        {
            name = "Thorin",
            @class = "Guerrier",
            race = "Nain",
            abilities = new
            {
                strength = 16,
                dexterity = 12,
                constitution = 14,
                intelligence = 10,
                wisdom = 10,
                charisma = 8
            }
        };

        var response = await _client.PostAsJsonAsync("/api/games/character", request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<CharacterTestResponse>();
        Assert.NotNull(result);
        Assert.Equal("Thorin", result.Name);
        Assert.Equal("Guerrier", result.Class);
        Assert.Equal("Nain", result.Race);
        Assert.True(result.MaxHitPoints > 0);
    }

    [Fact]
    public async Task CreateCharacter_ThenGetById_ReturnsCharacter()
    {
        var request = new
        {
            name = "Elara",
            @class = "Magicien",
            race = "Elfe",
            abilities = new
            {
                strength = 8,
                dexterity = 14,
                constitution = 12,
                intelligence = 16,
                wisdom = 12,
                charisma = 10
            }
        };

        var createResponse = await _client.PostAsJsonAsync("/api/games/character", request);
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<CharacterTestResponse>();
        Assert.NotNull(created);

        var getResponse = await _client.GetAsync($"/api/games/character/{created.Id}");

        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        var fetched = await getResponse.Content.ReadFromJsonAsync<CharacterTestResponse>();
        Assert.NotNull(fetched);
        Assert.Equal(created.Id, fetched.Id);
        Assert.Equal("Elara", fetched.Name);
    }

    [Fact]
    public async Task GetMyCharacters_ReturnsOkWithList()
    {
        var response = await _client.GetAsync("/api/games/character/my-characters");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<List<CharacterTestResponse>>();
        Assert.NotNull(result);
    }

    [Fact]
    public async Task CreateCharacter_WithoutAuth_ReturnsUnauthorized()
    {
        var unauthClient = _fixture.factory!.CreateClient();
        var request = new
        {
            name = "Sneaky",
            @class = "Voleur",
            race = "Halfelin"
        };

        var response = await unauthClient.PostAsJsonAsync("/api/games/character", request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("Barde")]
    [InlineData("Clerc")]
    [InlineData("Druide")]
    [InlineData("Moine")]
    [InlineData("Paladin")]
    [InlineData("Ensorceleur")]
    [InlineData("Sorcier")]
    public async Task CreateCharacter_WithNonPlayableClass_ReturnsBadRequest(string nonPlayableClass)
    {
        var request = new
        {
            name = "Ghost",
            @class = nonPlayableClass,
            race = "Humain",
            abilities = new
            {
                strength = 10, dexterity = 10, constitution = 10,
                intelligence = 10, wisdom = 10, charisma = 10
            }
        };

        var response = await _client.PostAsJsonAsync("/api/games/character", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("Barbare")]
    [InlineData("Guerrier")]
    [InlineData("Magicien")]
    [InlineData("Rodeur")]
    [InlineData("Voleur")]
    public async Task CreateCharacter_WithPlayableClass_ReturnsCreated(string playableClass)
    {
        var request = new
        {
            name = $"Hero_{playableClass}",
            @class = playableClass,
            race = "Humain",
            abilities = new
            {
                strength = 10, dexterity = 10, constitution = 10,
                intelligence = 10, wisdom = 10, charisma = 10
            }
        };

        var response = await _client.PostAsJsonAsync("/api/games/character", request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<CharacterTestResponse>();
        Assert.NotNull(result);
        Assert.Equal(playableClass, result.Class);
    }
}

public record CharacterTestResponse(
    Guid Id,
    string Name,
    int Level,
    int ExperiencePoints,
    string Class,
    string Race,
    int CurrentHitPoints,
    int MaxHitPoints,
    int ArmorClass,
    int Initiative,
    int Speed
);
