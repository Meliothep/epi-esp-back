using Multiplayer.Services;

namespace DnDiscordAPI.Games.Character.Services;

/// <summary>
/// Adapts ICharacterService to ICharacterLookupService for use in the Multiplayer hub.
/// </summary>
public class CharacterLookupAdapter : ICharacterLookupService
{
    private readonly ICharacterService _characterService;

    public CharacterLookupAdapter(ICharacterService characterService)
    {
        _characterService = characterService;
    }

    public async Task<CharacterLookupResult?> GetCharacterAsync(Guid characterId)
    {
        try
        {
            var dto = await _characterService.GetCharacterAsync(characterId);
            return new CharacterLookupResult
            {
                Name = dto.Name,
                CharacterClass = dto.Class.ToString(),
                MaxHitPoints = dto.MaxHitPoints,
                CurrentHitPoints = dto.CurrentHitPoints,
                ArmorClass = dto.ArmorClass,
                Speed = dto.Speed,
                Initiative = dto.Initiative,
            };
        }
        catch
        {
            return null;
        }
    }
}
