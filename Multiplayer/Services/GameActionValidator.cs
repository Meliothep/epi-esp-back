using Multiplayer.Models;
using Multiplayer.Models.Messages;

namespace Multiplayer.Services;

/// <summary>
/// Validateur de base. Accepte les actions lorsque la session existe et l'utilisateur est dans la session; les règles complètes plus tard.
/// </summary>
public class GameActionValidator : IGameActionValidator
{
    public ValidationResult ValidateMove(GameSession session, MoveRequest request, Guid userId)
    {
        if (string.IsNullOrEmpty(request.UnitId))
            return new ValidationResult(false, "INVALID_UNIT", "UnitId is required");
        if (session.State == Define.SessionState.Ended)
            return new ValidationResult(false, "SESSION_ENDED", "Session has ended");
        return new ValidationResult(true, null, null);
    }

    public ValidationResult ValidateAttack(GameSession session, AttackRequest request, Guid userId)
    {
        if (string.IsNullOrEmpty(request.AttackerId) || string.IsNullOrEmpty(request.TargetId))
            return new ValidationResult(false, "INVALID_TARGET", "AttackerId and TargetId are required");
        if (session.State == Define.SessionState.Ended)
            return new ValidationResult(false, "SESSION_ENDED", "Session has ended");
        return new ValidationResult(true, null, null);
    }

    public ValidationResult ValidateAbility(GameSession session, UseAbilityRequest request, Guid userId)
    {
        if (string.IsNullOrEmpty(request.UnitId) || string.IsNullOrEmpty(request.AbilityId))
            return new ValidationResult(false, "INVALID_ABILITY", "UnitId and AbilityId are required");
        if (session.State == Define.SessionState.Ended)
            return new ValidationResult(false, "SESSION_ENDED", "Session has ended");
        return new ValidationResult(true, null, null);
    }

    public ValidationResult ValidateTurnEnd(GameSession session, Guid userId)
    {
        if (session.State == Define.SessionState.Ended)
            return new ValidationResult(false, "SESSION_ENDED", "Session has ended");
        return new ValidationResult(true, null, null);
    }
}
