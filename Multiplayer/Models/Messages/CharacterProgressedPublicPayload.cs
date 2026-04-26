namespace Multiplayer.Models.Messages;

public class CharacterProgressedPublicPayload
{
    public Guid TargetUserId { get; set; }
    public string TargetCharacterName { get; set; } = string.Empty;
    public int NewLevel { get; set; }
    public int LevelUps { get; set; }
}
