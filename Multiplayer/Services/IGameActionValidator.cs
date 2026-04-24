using Multiplayer.Models;
using Multiplayer.Models.Messages;

namespace Multiplayer.Services;

public interface IGameActionValidator
{
    ValidationResult ValidateMove(GameSession session, UnitMovedPayload request, Guid userId);
}

public sealed class ValidationResult
{
    public bool IsValid { get; init; }
    public string? ErrorCode { get; init; }
    public string? ErrorMessage { get; init; }

    public static ValidationResult Ok() => new() { IsValid = true };

    public static ValidationResult Fail(string errorCode, string? errorMessage = null) =>
        new() { IsValid = false, ErrorCode = errorCode, ErrorMessage = errorMessage };
}

