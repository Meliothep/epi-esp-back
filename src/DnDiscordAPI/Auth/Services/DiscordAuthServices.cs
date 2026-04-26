using System.Text.Json.Serialization;
using DnDiscordAPI.Models;

namespace DnDiscordAPI.Auth.Services;

public interface IDiscordAuthService
{
    Task<DiscordUserData?> GetUserDataAsync(string accessToken);
    Task<string?> ExchangeCodeForTokenAsync(string code, string? redirectUri = null);

    /// <summary>
    /// Révoque un token d'accès Discord via POST /oauth2/token/revoke.
    /// Fire-and-forget : le back ne persiste pas ce token, donc une fois
    /// les user data récupérées il n'a plus aucune utilité. Révoquer
    /// côté Discord ferme proprement l'autorisation et évite de laisser
    /// traîner un token inutile. Les erreurs sont avalées (best-effort).
    /// Référence : https://discord.com/developers/docs/topics/oauth2#revoke-tokens
    /// </summary>
    Task RevokeAccessTokenAsync(string accessToken);
}

public class DiscordAuthService : IDiscordAuthService
{
    private readonly HttpClient _httpClient;
    private readonly string _clientId;
    private readonly string _clientSecret;
    private readonly string _redirectUri;
    private readonly ILogger<DiscordAuthService> _logger;

    public DiscordAuthService(HttpClient httpClient, IConfiguration configuration, ILogger<DiscordAuthService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _clientId = configuration["Discord:ClientId"];
        _clientSecret = configuration["Discord:ClientSecret"];
        _redirectUri = configuration["Discord:RedirectUri"];
        if (string.IsNullOrEmpty(_clientId)) throw new InvalidOperationException("Discord:ClientId is not configured");
        if (string.IsNullOrEmpty(_clientSecret)) throw new InvalidOperationException("Discord:ClientSecret is not configured");
        if (string.IsNullOrEmpty(_redirectUri)) throw new InvalidOperationException("Discord:RedirectUri is not configured");
    }

    public async Task<string?> ExchangeCodeForTokenAsync(string code, string? redirectUri = null)
    {
        try
        {
            var effectiveRedirectUri = !string.IsNullOrEmpty(redirectUri) ? redirectUri : _redirectUri;
            _logger.LogInformation("[DISCORD_AUTH] Starting code exchange (redirect_uri: {RedirectUri})...", effectiveRedirectUri);

            var request = new HttpRequestMessage(HttpMethod.Post, "https://discord.com/api/oauth2/token");
            
            var content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                { "client_id", _clientId },
                { "client_secret", _clientSecret },
                { "grant_type", "authorization_code" },
                { "code", code },
                { "redirect_uri", effectiveRedirectUri },
            });

            request.Content = content;
            _logger.LogInformation($"[DISCORD_AUTH] Sending request to Discord OAuth token endpoint");
            
            var response = await _httpClient.SendAsync(request);

            _logger.LogInformation($"[DISCORD_AUTH] Discord responded with status: {response.StatusCode}");

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("[DISCORD_AUTH] Token exchange failed: {StatusCode} - {Error}", response.StatusCode, errorContent);
                throw new InvalidOperationException($"Discord token exchange failed ({response.StatusCode}): {errorContent}");
            }

            var responseString = await response.Content.ReadAsStringAsync();
            
            var oauthResponse = System.Text.Json.JsonSerializer.Deserialize<DiscordOAuthTokenResponse>(responseString);

            if (oauthResponse?.AccessToken == null)
            {
                _logger.LogError($"[DISCORD_AUTH] Failed to extract access token from response");
                return null;
            }

            _logger.LogInformation($"[DISCORD_AUTH] Successfully extracted access token: {MaskToken(oauthResponse.AccessToken)}");
            return oauthResponse.AccessToken;
        }
        catch (Exception ex)
        {
            _logger.LogError($"[DISCORD_AUTH] Exception in ExchangeCodeForTokenAsync: {ex.Message}\n{ex.StackTrace}");
            return null;
        }
    }

    public async Task<DiscordUserData?> GetUserDataAsync(string accessToken)
    {
        try
        {
            _logger.LogInformation($"[DISCORD_AUTH] Fetching user data with token: {MaskToken(accessToken)}...");

            var request = new HttpRequestMessage(HttpMethod.Get, "https://discord.com/api/users/@me");
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

            _logger.LogInformation($"[DISCORD_AUTH] Sending request to Discord /users/@me endpoint");
            var response = await _httpClient.SendAsync(request);

            _logger.LogInformation($"[DISCORD_AUTH] Discord responded with status: {response.StatusCode}");

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError($"[DISCORD_AUTH] User data fetch failed: {response.StatusCode} - {errorContent}");
                return null;
            }

            var responseString = await response.Content.ReadAsStringAsync();
            
            var userData = System.Text.Json.JsonSerializer.Deserialize<DiscordUserData>(responseString);

            if (userData == null)
            {
                _logger.LogError($"[DISCORD_AUTH] Failed to deserialize user data from response");
                return null;
            }

            _logger.LogInformation($"[DISCORD_AUTH] Successfully deserialized user: Id={userData.Id}, Username={userData.Username}");
            return userData;
        }
        catch (Exception ex)
        {
            _logger.LogError($"[DISCORD_AUTH] Exception in GetUserDataAsync: {ex.Message}\n{ex.StackTrace}");
            return null;
        }
    }

    public async Task RevokeAccessTokenAsync(string accessToken)
    {
        if (string.IsNullOrEmpty(accessToken)) return;
        try
        {
            var request = new HttpRequestMessage(HttpMethod.Post, "https://discord.com/api/oauth2/token/revoke");
            request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                { "client_id", _clientId },
                { "client_secret", _clientSecret },
                { "token", accessToken },
                { "token_type_hint", "access_token" },
            });
            var response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("[DISCORD_AUTH] Token revoke returned {Status}", response.StatusCode);
                return;
            }
            _logger.LogInformation("[DISCORD_AUTH] Token revoked (opportunistic)");
        }
        catch (Exception ex)
        {
            // Best-effort : on ne doit jamais casser le callback à cause
            // de cette révocation opportuniste.
            _logger.LogWarning(ex, "[DISCORD_AUTH] Opportunistic token revoke failed");
        }
    }

    private static string MaskToken(string token)
    {
        if (string.IsNullOrEmpty(token)) return string.Empty;
        if (token.Length <= 8) return "***";
        return $"{token[..4]}...{token[^4..]}";
    }
}

public class DiscordOAuthTokenResponse
{
    [JsonPropertyName("access_token")]
    public string AccessToken { get; set; } = string.Empty;

    [JsonPropertyName("token_type")]
    public string TokenType { get; set; } = string.Empty;

    [JsonPropertyName("expires_in")]
    public int ExpiresIn { get; set; }

    [JsonPropertyName("refresh_token")]
    public string? RefreshToken { get; set; }

    [JsonPropertyName("scope")]
    public string Scope { get; set; } = string.Empty;
}