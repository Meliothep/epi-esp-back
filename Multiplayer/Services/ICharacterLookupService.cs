namespace Multiplayer.Services;

/// <summary>
/// Interface minimale pour récupérer les données d'un personnage depuis le hub multijoueur.
/// Implémenté dans le projet principal (DnDiscordAPI) pour éviter une dépendance circulaire.
/// </summary>
public interface ICharacterLookupService
{
    Task<CharacterLookupResult?> GetCharacterAsync(Guid characterId);
}

public class CharacterLookupResult
{
    public string Name { get; set; } = string.Empty;
    public string CharacterClass { get; set; } = string.Empty;
    public int MaxHitPoints { get; set; }
    public int CurrentHitPoints { get; set; }
    public int ArmorClass { get; set; }
    public int Speed { get; set; }
    public int Initiative { get; set; }
}
