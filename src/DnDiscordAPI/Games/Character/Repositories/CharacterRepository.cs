using DnDiscordAPI.Games.Database;
using Microsoft.EntityFrameworkCore;

namespace DnDiscordAPI.Games.Character.Repositories
{
    public interface ICharacterRepository
    {
        Task<Models.Character> GetByIdAsync(Guid id);
        Task<List<Models.Character>> GetByUserIdAsync(string discordUserId);
        Task<Models.Character> CreateAsync(Models.Character character);
        Task UpdateAsync(Models.Character character);
        Task DeleteAsync(Guid id);
    }


    public class CharacterRepository : ICharacterRepository
    {
        private readonly GamesDbContext _context;

        public CharacterRepository(GamesDbContext context)
        {
            _context = context;
        }

        public async Task<Models.Character> GetByIdAsync(Guid id)
        {
            return await _context.Characters.FindAsync(id);
        }

        public async Task<List<Models.Character>> GetByUserIdAsync(string discordUserId)
        {
            return await _context.Characters
                .Where(c => c.DiscordUserId == discordUserId)
                .ToListAsync();
        }

        public async Task<Models.Character> CreateAsync(Models.Character character)
        {
            _context.Characters.Add(character);
            await _context.SaveChangesAsync();
            return character;
        }

        public async Task UpdateAsync(Models.Character character)
        {
            _context.Characters.Update(character);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteAsync(Guid id)
        {
            var character = await _context.Characters.FindAsync(id);
            if (character != null)
            {
                _context.Characters.Remove(character);
                await _context.SaveChangesAsync();
            }
        }
    }
}
