namespace Multiplayer.Models.Messages;

/// <summary>Hub inbound from DM. Hub rolls values, then fans out.</summary>
public record DmRollRequestPayload(
    string DiceType,
    List<Guid> TargetUserIds,
    string? Label);

/// <summary>Outbound to each target via Clients.User. Carries that target's forced value only.</summary>
public record RollRequestedPayload(
    Guid RequestId,
    string DiceType,
    string? Label,
    int ForcedValue);

/// <summary>Outbound to DM only via Clients.User. No values — DM gets progress via RollResultBroadcast.</summary>
public record RollRequestedDmEchoPayload(
    Guid RequestId,
    string DiceType,
    string? Label,
    List<Guid> TargetUserIds,
    int ExpectedCount);

/// <summary>Outbound to session group for spectator awareness. No values.</summary>
public record RollRequestedPublicPayload(
    Guid RequestId,
    string DiceType,
    string? Label,
    List<Guid> TargetUserIds,
    int ExpectedCount);

/// <summary>Inbound from player. No userId (derived from GetUserId()) and no value (server-owned).</summary>
public record SubmitRollResultPayload(Guid RequestId);

/// <summary>Outbound to full group once a player submits.</summary>
public record RollResultBroadcastPayload(
    Guid RequestId,
    Guid UserId,
    string? UserName,
    string DiceType,
    int Value,
    string? Label,
    bool RequestComplete);

/// <summary>Outbound to full group on cancel.</summary>
public record RollCanceledPayload(
    Guid RequestId,
    string? Label,
    List<Guid> StillPendingUserIds);
