namespace Multiplayer.Models.Messages;

/// <summary>
/// Requête de déplacement autorisée par le serveur (validée sur le serveur).
/// </summary>
public class MoveRequest
{
    public string UnitId { get; set; } = string.Empty;
    public int TargetX { get; set; }
    public int TargetY { get; set; }
    /// <summary>Optional path from client (if server does not compute path yet).</summary>
    public List<GridPosition>? Path { get; set; }
}

/// <summary>
/// Résultat d'un déplacement validé.
/// </summary>
public class MoveResult
{
    public string UnitId { get; set; } = string.Empty;
    public List<GridPosition> Path { get; set; } = new();
    public int ApCost { get; set; }
    public bool Success { get; set; }
    public string? Error { get; set; }
}

/// <summary>
/// Requête d'attaque autorisée par le serveur (validée sur le serveur).
/// </summary>
public class AttackRequest
{
    public string AttackerId { get; set; } = string.Empty;
    public string TargetId { get; set; } = string.Empty;
    public string AbilityId { get; set; } = string.Empty;
}

/// <summary>
/// Résultat d'une attaque validée.
/// </summary>
public class AttackResult
{
    public string AttackerId { get; set; } = string.Empty;
    public string TargetId { get; set; } = string.Empty;
    public string AbilityId { get; set; } = string.Empty;
    public int DiceRoll { get; set; }
    public int Modifier { get; set; }
    public int Total => DiceRoll + Modifier;
    public bool Hit { get; set; }
    public int? Damage { get; set; }
    public List<StatusEffectInfo>? Effects { get; set; }
    public bool Success { get; set; }
    public string? Error { get; set; }
}

public class StatusEffectInfo
{
    public string Type { get; set; } = string.Empty;
    public string TargetId { get; set; } = string.Empty;
    public int Value { get; set; }
}

/// <summary>
/// Requête d'utilisation d'une capacité autorisée par le serveur (validée sur le serveur).
/// </summary>
public class UseAbilityRequest
{
    public string UnitId { get; set; } = string.Empty;
    public string AbilityId { get; set; } = string.Empty;
    public AbilityTargetInfo Target { get; set; } = new();
}

public class AbilityTargetInfo
{
    public string? TargetUnitId { get; set; }
    public int? TargetX { get; set; }
    public int? TargetY { get; set; }
}

/// <summary>
/// Résultat d'une utilisation de capacité validée.
/// </summary>
public class UseAbilityResult
{
    public string UnitId { get; set; } = string.Empty;
    public string AbilityId { get; set; } = string.Empty;
    public bool Success { get; set; }
    public AbilityEffect? Effect { get; set; }
    public string? Error { get; set; }
}
