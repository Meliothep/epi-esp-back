using Multiplayer.Models;
using Multiplayer.Models.Messages;

namespace Multiplayer.Services;

/// <summary>
/// Valide les actions de jeu (autorisées par le serveur). Implémentation de base jusqu'à ce que le state complet soit sur le serveur.
/// </summary>
public record ValidationResult(bool IsValid, string? ErrorCode, string? ErrorMessage);

public interface IGameActionValidator
{
    ValidationResult ValidateMove(GameSession session, MoveRequest request, Guid userId);
    ValidationResult ValidateAttack(GameSession session, AttackRequest request, Guid userId);
    ValidationResult ValidateAbility(GameSession session, UseAbilityRequest request, Guid userId);
    ValidationResult ValidateTurnEnd(GameSession session, Guid userId);
}
