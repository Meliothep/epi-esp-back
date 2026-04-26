using System.Collections.Concurrent;

namespace DnDiscordAPI.PartyChat;

public record VoiceSessionBinding(string GuildId, string VoiceChannelId, string SessionId, DateTime BoundAtUtc);

/// <summary>
/// In-memory mapping between a Discord voice channel and a multiplayer session.
/// This is used to route Discord "voice channel chat" messages into the right SignalR group.
/// </summary>
public class VoiceSessionRegistry
{
    private readonly ConcurrentDictionary<string, VoiceSessionBinding> _bindings = new();

    private static string Key(string guildId, string voiceChannelId) => $"{guildId}:{voiceChannelId}";

    public VoiceSessionBinding Bind(string guildId, string voiceChannelId, string sessionId)
    {
        var binding = new VoiceSessionBinding(guildId, voiceChannelId, sessionId, DateTime.UtcNow);
        _bindings[Key(guildId, voiceChannelId)] = binding;
        return binding;
    }

    public VoiceSessionBinding? TryGet(string guildId, string voiceChannelId)
    {
        _bindings.TryGetValue(Key(guildId, voiceChannelId), out var binding);
        return binding;
    }
}

