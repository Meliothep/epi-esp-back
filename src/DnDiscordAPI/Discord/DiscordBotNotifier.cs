using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace DnDiscordAPI.Discord;

public interface IDiscordBotNotifier
{
    Task TryAnnounceSessionCreatedAsync(string voiceChannelId, string sessionId, string createdBy, CancellationToken ct = default);
}

/// <summary>
/// Sends simple messages to Discord channels using a bot token.
/// Used to announce newly created sessions in the related voice channel chat.
/// </summary>
public sealed class DiscordBotNotifier : IDiscordBotNotifier
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<DiscordBotNotifier> _logger;

    public DiscordBotNotifier(IHttpClientFactory httpClientFactory, IConfiguration configuration, ILogger<DiscordBotNotifier> logger)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task TryAnnounceSessionCreatedAsync(string voiceChannelId, string sessionId, string createdBy, CancellationToken ct = default)
    {
        var botToken = _configuration["DiscordBot:BotToken"];
        if (string.IsNullOrWhiteSpace(botToken))
        {
            _logger.LogDebug("[DiscordBotNotifier] DiscordBot:BotToken not configured, skipping announce.");
            return;
        }

        if (string.IsNullOrWhiteSpace(voiceChannelId))
        {
            _logger.LogDebug("[DiscordBotNotifier] voiceChannelId empty, skipping announce.");
            return;
        }

        var content =
            $"**{createdBy}** a créé une session **{sessionId}**. " +
            $"Ouvre l’app DnDiscord et rejoins avec l’ID **{sessionId}**.";

        var client = _httpClientFactory.CreateClient("discord-bot");
        using var req = new HttpRequestMessage(HttpMethod.Post, $"channels/{voiceChannelId}/messages");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bot", botToken);
        req.Content = new StringContent(
            JsonSerializer.Serialize(new { content }),
            Encoding.UTF8,
            "application/json");

        try
        {
            using var res = await client.SendAsync(req, ct);
            if (!res.IsSuccessStatusCode)
            {
                var body = await res.Content.ReadAsStringAsync(ct);
                _logger.LogWarning("[DiscordBotNotifier] Failed to post announce to channel {ChannelId}. Status {Status}. Body: {Body}",
                    voiceChannelId, (int)res.StatusCode, body);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[DiscordBotNotifier] Exception while announcing session created.");
        }
    }
}

