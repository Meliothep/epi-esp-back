using System.Security.Cryptography;
using System.Text;

namespace DnDiscord.Campaign.Common;

/// <summary>
/// Conversion déterministe d'un identifiant Discord (snowflake string)
/// vers un <see cref="Guid"/> stable, utilisée en clé primaire /
/// foreign key dans les modules qui préfèrent un type Guid (module
/// Campaign, et les endpoints RGPD du module API qui veulent dériver
/// le même Guid pour le lookup cross-module).
///
/// Ce helper vit dans un namespace <c>Common</c> dédié (pas dans
/// <c>Campaign.Services</c>) pour que les consommateurs externes
/// (DnDiscordAPI.Controllers, etc.) n'aient pas à réaliser une
/// back-référence vers un service métier — ils importent simplement
/// une utility statique clairement partagée.
///
/// ⚠️ Limitations connues (à relire avant toute migration) :
///  - <b>FIPS</b> : <see cref="MD5"/> lève <see cref="PlatformNotSupportedException"/>
///    en mode FIPS (certains Azure SKU durcis, tenants gov). Pas
///    bloquant sur Hostinger Allemagne. Passer à SHA-256 si migration
///    vers FIPS (implique migration des Guids en base).
///  - <b>Pseudonymisation faible</b> : MD5(snowflake) est déterministe,
///    petit espace, donc un attaquant qui connaît un Discord ID
///    retrouve le Guid. Pour une vraie pseudonymisation (art. 4.5 RGPD),
///    passer à HMAC-SHA-256 avec secret serveur — migration de base.
/// </summary>
public static class DiscordIdMapping
{
    public static Guid ToGuid(string discordId)
    {
        using var md5 = MD5.Create();
        var hash = md5.ComputeHash(Encoding.UTF8.GetBytes(discordId));
        return new Guid(hash);
    }
}
