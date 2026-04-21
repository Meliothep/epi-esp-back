using DnDiscordAPI.Models;

namespace DnDiscordAPI.Auth.Services;

public interface IUserStore
{
    User GetOrCreateUser(DiscordUserData discordUser);
    User? TryGetUser(string userId);
    bool RemoveUser(string userId);
}

public class UserStoreService : IUserStore
{
    private static readonly Dictionary<string, User> Users = new();

    public User GetOrCreateUser(DiscordUserData discordUser)
    {
        if (Users.TryGetValue(discordUser.Id, out var existingUser))
        {
            existingUser.Username = discordUser.Username;
            existingUser.Email = discordUser.Email ?? existingUser.Email;
            existingUser.Avatar = discordUser.Avatar;
            return existingUser;
        }

        var newUser = new User
        {
            Id = discordUser.Id,
            Username = discordUser.Username,
            Email = discordUser.Email ?? $"{discordUser.Username}@discord.local",
            DiscordId = discordUser.Id,
            Avatar = discordUser.Avatar,
            CreatedAt = DateTime.UtcNow,
        };

        Users[newUser.Id] = newUser;
        return newUser;
    }

    public User? TryGetUser(string userId)
    {
        return Users.TryGetValue(userId, out var user) ? user : null;
    }

    public bool RemoveUser(string userId)
    {
        return Users.Remove(userId);
    }
}
