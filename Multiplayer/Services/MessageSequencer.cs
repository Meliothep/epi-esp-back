using System.Collections.Concurrent;
using Multiplayer.Models.Messages;

namespace Multiplayer.Services;

/// <summary>
/// Gère les numéros de séquence pour les messages de jeu
/// </summary>
public class MessageSequencer
{
    private readonly ConcurrentDictionary<string, long> _sessionSequences = new();

    #region [== Gestion des séquences ==]

    /// <summary>
    /// Obtenir le prochain numéro de séquence pour une session
    /// </summary>
    public long GetNextSequence(string sessionId)
    {
        return _sessionSequences.AddOrUpdate(
            sessionId,
            1, // Valeur initiale
            (_, current) => current + 1
        );
    }

    /// <summary>
    /// Créer un message avec métadonnées
    /// </summary>
    public GameMessage<T> CreateMessage<T>(string sessionId, string messageType, T payload) where T : class
    {
        return new GameMessage<T>
        {
            Type = messageType,
            SequenceNumber = GetNextSequence(sessionId),
            Timestamp = DateTime.UtcNow,
            SessionId = sessionId,
            Payload = payload
        };
    }

    /// <summary>
    /// Réinitialiser la séquence d'une session (quand elle se termine)
    /// </summary>
    public void ResetSequence(string sessionId)
    {
        _sessionSequences.TryRemove(sessionId, out _);
    }

    #endregion
}