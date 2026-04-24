using Multiplayer.Models;
using Multiplayer.Models.Messages;

namespace Multiplayer.Services;

/// <summary>
/// Validations serveur minimales. Pour l'instant on se contente de vérifications
/// simples (session/ownership) — la validation "map/path" est gérée côté client
/// et pourra être renforcée plus tard.
/// </summary>
public sealed class GameActionValidator : IGameActionValidator
{
    public ValidationResult ValidateMove(GameSession session, UnitMovedPayload request, Guid userId)
    {
        if (session == null) return ValidationResult.Fail("SESSION_NULL");
        if (request == null) return ValidationResult.Fail("PAYLOAD_NULL");
        if (string.IsNullOrWhiteSpace(request.UnitId)) return ValidationResult.Fail("UNIT_ID_REQUIRED");

        if (!session.Combat.Units.TryGetValue(request.UnitId, out var unit))
            return ValidationResult.Fail("UNIT_NOT_FOUND");

        var isDm = session.DmUserId == userId;
        if (!isDm && unit.OwnerUserId != userId)
            return ValidationResult.Fail("FORBIDDEN", "You do not control that unit");

        return ValidationResult.Ok();
    }
}

