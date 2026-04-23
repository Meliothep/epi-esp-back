using System.Collections.Concurrent;
using DnDiscordAPI.Models;

namespace DnDiscordAPI.Auth.Services;

public interface IUserStore
{
    User GetOrCreateUser(DiscordUserData discordUser);
    User? TryGetUser(string userId);

    /// <summary>
    /// Marque un compte comme supprimé (RGPD art. 17) et retire l'entrée
    /// in-memory. Les JWT émis avant la suppression restent signés valides,
    /// mais <see cref="IsDeleted"/> permet aux endpoints d'authentification
    /// de les rejeter — sinon un appel authentifié recrée silencieusement
    /// le compte via les claims.
    /// </summary>
    bool RemoveUser(string userId);

    /// <summary>
    /// Indique si un DiscordId a été supprimé au titre du droit à
    /// l'effacement. Consulté par <c>GetCurrentUser</c> et la pipeline
    /// d'auth pour bloquer le rejeu des JWT émis avant la suppression.
    /// </summary>
    bool IsDeleted(string userId);
}

public class UserStoreService : IUserStore
{
    // `static` pour survivre aux scopes DI — le store est in-memory et
    // doit persister entre les requêtes.
    private static readonly ConcurrentDictionary<string, User> Users = new();

    // Tombstone set : DiscordIds dont le compte a été supprimé au titre du
    // droit à l'effacement. In-memory = reset au redémarrage serveur (POC).
    // Production : persister dans une table AuditEvents / DeletedAccounts
    // consultée par un authorization filter.
    private static readonly ConcurrentDictionary<string, DateTime> Tombstones = new();

    public User GetOrCreateUser(DiscordUserData discordUser)
    {
        // Refuse de re-créer un compte supprimé : l'utilisateur doit
        // re-consentir explicitement après une révocation Discord.
        // Type dédié pour que DiscordCallback puisse renvoyer un 410 Gone
        // actionnable au lieu d'un 500 opaque (reviewer N2).
        if (Tombstones.ContainsKey(discordUser.Id))
        {
            throw new DnDiscordAPI.Auth.AccountTombstonedException(discordUser.Id);
        }

        return Users.AddOrUpdate(
            discordUser.Id,
            _ => new User
            {
                Id = discordUser.Id,
                Username = discordUser.Username,
                Email = discordUser.Email ?? $"{discordUser.Username}@discord.local",
                DiscordId = discordUser.Id,
                Avatar = discordUser.Avatar,
                CreatedAt = DateTime.UtcNow,
            },
            (_, existing) =>
            {
                existing.Username = discordUser.Username;
                existing.Email = discordUser.Email ?? existing.Email;
                existing.Avatar = discordUser.Avatar;
                return existing;
            });
    }

    public User? TryGetUser(string userId)
    {
        if (Tombstones.ContainsKey(userId)) return null;
        return Users.TryGetValue(userId, out var user) ? user : null;
    }

    public bool RemoveUser(string userId)
    {
        Tombstones[userId] = DateTime.UtcNow;
        return Users.TryRemove(userId, out _);
    }

    public bool IsDeleted(string userId) => Tombstones.ContainsKey(userId);
}
