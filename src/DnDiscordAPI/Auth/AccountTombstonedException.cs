namespace DnDiscordAPI.Auth;

/// <summary>
/// Levée quand une tentative d'authentification (ré-authentification Discord)
/// porte sur un DiscordId qui a été tombstoné au titre du droit à l'effacement
/// (RGPD art. 17). Permet aux controllers d'attraper spécifiquement ce cas
/// pour retourner un 410 Gone actionnable au lieu d'un 500 opaque, afin que
/// le front puisse afficher un message « votre compte a été supprimé, révoquez
/// l'accès côté Discord avant de vous réinscrire ».
/// </summary>
public class AccountTombstonedException : Exception
{
    public string DiscordId { get; }

    public AccountTombstonedException(string discordId)
        : base($"Account {discordId} has been deleted (RGPD erasure). Cannot recreate.")
    {
        DiscordId = discordId;
    }
}
