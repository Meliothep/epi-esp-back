using DnDiscordAPI.Games.Character.DTOs;
using DnDiscordAPI.Games.Character.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DnDiscordAPI.Games.Controllers
{
    [ApiController]
    [Route("api/games/[controller]")]
    // [Authorize] // Temporairement désactivé pour les tests
    public class CharacterController : ControllerBase
    {
        private readonly ICharacterService _characterService;

        public CharacterController(ICharacterService characterService)
        {
            _characterService = characterService;
        }

        [HttpPost]
        public async Task<ActionResult<CharacterDto>> CreateCharacter([FromBody] CreateCharacterRequest request)
        {
            // Pour les tests, on utilise un userId par défaut si pas authentifié
            var discordUserId = User.FindFirst("discord_id")?.Value ?? "test-user-123";

            var character = await _characterService.CreateCharacterAsync(discordUserId, request);
            return CreatedAtAction(nameof(GetCharacter), new { id = character.Id }, character);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<CharacterDto>> GetCharacter(Guid id)
        {
            var character = await _characterService.GetCharacterAsync(id);
            return Ok(character);
        }

        [HttpGet("my-characters")]
        public async Task<ActionResult<List<CharacterDto>>> GetMyCharacters()
        {
            // Pour les tests, on utilise un userId par défaut si pas authentifié
            var discordUserId = User.FindFirst("discord_id")?.Value ?? "test-user-123";
            var characters = await _characterService.GetUserCharactersAsync(discordUserId);
            return Ok(characters);
        }

        [HttpPatch("{id}/hit-points")]
        public async Task<ActionResult<CharacterDto>> UpdateHitPoints(
            Guid id,
            [FromBody] UpdateHitPointsRequest request)
        {
            var character = await _characterService.UpdateHitPointsAsync(id, request.HitPoints);
            return Ok(character);
        }

        [HttpPost("{id}/level-up")]
        public async Task<ActionResult<CharacterDto>> LevelUp(Guid id)
        {
            var character = await _characterService.LevelUpAsync(id);
            return Ok(character);
        }
    }
}
