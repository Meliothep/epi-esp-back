using DnDiscordAPI.Auth.Services;

namespace DnDiscordAPI.Auth;

/// <summary>
/// Middleware RGPD : rejette les JWT appartenant à un compte supprimé
/// au titre du droit à l'effacement (art. 17).
///
/// Raison d'être : <c>RemoveUser</c> vide l'entrée in-memory du
/// <see cref="IUserStore"/>, mais les JWT signés avant la suppression
/// restent valides jusqu'à 7 jours. Sans ce garde, n'importe quel
/// endpoint <c>[Authorize]</c> autre que <c>GET /api/auth/me</c>
/// accepterait le token et recréerait silencieusement des rows avec
/// le DiscordId / userGuid du compte effacé (POST /api/characters,
/// /api/campaigns, etc.).
///
/// Ce middleware doit être inséré APRÈS <c>UseAuthentication</c> et
/// AVANT <c>UseAuthorization</c> pour disposer des claims tout en
/// coupant la requête avant l'action.
/// </summary>
public class TombstonedAccountMiddleware
{
    private readonly RequestDelegate _next;

    public TombstonedAccountMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, IUserStore userStore)
    {
        if (context.User?.Identity?.IsAuthenticated == true)
        {
            var sub = context.User.FindFirst("sub")?.Value;
            if (!string.IsNullOrEmpty(sub) && userStore.IsDeleted(sub))
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsync("{\"error\":\"account_deleted\"}");
                return;
            }
        }

        await _next(context);
    }
}
