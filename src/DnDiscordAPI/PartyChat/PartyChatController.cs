using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Multiplayer.Hubs;
using Multiplayer.Services;
using System.Security.Claims;
using DnDiscordAPI.Discord;

namespace DnDiscordAPI.PartyChat;

[ApiController]
[Route("api/party-chat")]
public class PartyChatController : ControllerBase
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<PartyChatController> _logger;
    private readonly VoiceSessionRegistry _registry;
    private readonly SessionManager _sessionManager;
    private readonly IHubContext<GameHub> _gameHub;
    private readonly DnDiscord.Campaign.Services.IUserContextService _userContext;
    private readonly IDiscordBotNotifier _discordBotNotifier;

    public PartyChatController(
        IConfiguration configuration,
        ILogger<PartyChatController> logger,
        VoiceSessionRegistry registry,
        SessionManager sessionManager,
        IHubContext<GameHub> gameHub,
        DnDiscord.Campaign.Services.IUserContextService userContext,
        IDiscordBotNotifier discordBotNotifier)
    {
        _configuration = configuration;
        _logger = logger;
        _registry = registry;
        _sessionManager = sessionManager;
        _gameHub = gameHub;
        _userContext = userContext;
        _discordBotNotifier = discordBotNotifier;
    }

    public record BindRequest(string SessionId, string GuildId, string VoiceChannelId);

    /// <summary>
    /// Bind the current Discord voice channel (guild + channel) to a multiplayer session.
    /// Requires the caller to be the DM of the session (prevents hijacking).
    /// </summary>
    [Authorize]
    [HttpPost("bind")]
    public async Task<IActionResult> Bind([FromBody] BindRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.SessionId) ||
            string.IsNullOrWhiteSpace(request.GuildId) ||
            string.IsNullOrWhiteSpace(request.VoiceChannelId))
        {
            return BadRequest(new { error = "SessionId, GuildId and VoiceChannelId are required" });
        }

        var session = _sessionManager.GetSession(request.SessionId);
        if (session == null)
        {
            return NotFound(new { error = "Session not found" });
        }

        var me = _userContext.GetCurrentUserId();
        if (session.DmUserId != me)
        {
            return Forbid();
        }

        var binding = _registry.Bind(request.GuildId, request.VoiceChannelId, request.SessionId);
        _logger.LogInformation("[PartyChat] Bound guild {GuildId} voice {VoiceChannelId} to session {SessionId}",
            request.GuildId, request.VoiceChannelId, request.SessionId);

        var createdBy =
            User.FindFirst("username")?.Value
            ?? User.FindFirst("name")?.Value
            ?? User.FindFirst(ClaimTypes.Name)?.Value
            ?? "Quelqu’un";
        await _discordBotNotifier.TryAnnounceSessionCreatedAsync(
            voiceChannelId: request.VoiceChannelId,
            sessionId: request.SessionId,
            createdBy: createdBy,
            ct: ct);

        return Ok(binding);
    }

    public record IngestRequest(
        string Content,
        string Author,
        string AuthorDiscordId,
        string? AuthorAvatar,
        string GuildId,
        string VoiceChannelId,
        long Timestamp,
        string MessageId);

    public record PartyChatMessageDto(
        string SessionId,
        string Content,
        string AuthorName,
        string AuthorUserId,
        string AuthorDiscordId,
        string? AuthorAvatar,
        string AuthorRole,
        long Timestamp,
        string MessageId);

    /// <summary>
    /// Ingest a Discord message and broadcast it to the correct multiplayer session via GameHub.
    /// Called by the Discord bot. Authentication is done with a shared secret header.
    /// </summary>
    [AllowAnonymous]
    [HttpPost("ingest")]
    public async Task<IActionResult> Ingest([FromBody] IngestRequest request)
    {
        var expectedToken = _configuration["DiscordBot:IngestToken"];
        var token = Request.Headers["X-Bot-Token"].ToString();
        if (string.IsNullOrEmpty(expectedToken) || token != expectedToken)
        {
            return Unauthorized(new { error = "Invalid bot token" });
        }

        if (string.IsNullOrWhiteSpace(request.Content))
        {
            return BadRequest(new { error = "Content is required" });
        }

        var binding = _registry.TryGet(request.GuildId, request.VoiceChannelId);
        if (binding == null)
        {
            _logger.LogDebug("[PartyChat] Drop message {MessageId}: no binding for guild {GuildId} voice {VoiceChannelId}",
                request.MessageId, request.GuildId, request.VoiceChannelId);
            return Ok(new { status = "dropped", reason = "no_binding" });
        }

        var session = _sessionManager.GetSession(binding.SessionId);
        if (session == null)
        {
            _logger.LogDebug("[PartyChat] Drop message {MessageId}: bound session {SessionId} not found",
                request.MessageId, binding.SessionId);
            return Ok(new { status = "dropped", reason = "session_not_found" });
        }

        var authorGuid = ConvertDiscordIdToGuid(request.AuthorDiscordId);
        var isInSession = session.Players.Any(p => p.UserId == authorGuid);
        if (!isInSession)
        {
            _logger.LogDebug("[PartyChat] Drop message {MessageId}: author {AuthorDiscordId} not in session {SessionId}",
                request.MessageId, request.AuthorDiscordId, binding.SessionId);
            return Ok(new { status = "dropped", reason = "author_not_in_session" });
        }

        var authorRole = session.DmUserId == authorGuid ? "DM" : "Player";

        var dto = new PartyChatMessageDto(
            SessionId: binding.SessionId,
            Content: request.Content,
            AuthorName: request.Author,
            AuthorUserId: authorGuid.ToString(),
            AuthorDiscordId: request.AuthorDiscordId,
            AuthorAvatar: request.AuthorAvatar,
            AuthorRole: authorRole,
            Timestamp: request.Timestamp,
            MessageId: request.MessageId
        );

        await _gameHub.Clients.Group(binding.SessionId).SendAsync("PartyChatMessage", dto);
        return Ok(new { status = "published", sessionId = binding.SessionId });
    }

    private static Guid ConvertDiscordIdToGuid(string discordId)
    {
        using var md5 = MD5.Create();
        var hash = md5.ComputeHash(Encoding.UTF8.GetBytes(discordId));
        return new Guid(hash);
    }
}

