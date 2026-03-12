using System.Security.Claims;
using DnDiscordAPI.Games.Character.DTOs;
using DnDiscordAPI.Games.Character.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace DnDiscordAPI.Games.Controllers
{
    [ApiController]
    [Route("api/games/[controller]")]
    [Authorize] // JWT obligatoire
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
            var discordUserId = User.FindFirst("sub")?.Value
                ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (string.IsNullOrEmpty(discordUserId))
                return Unauthorized(new { error = "User id missing in token" });

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
            var discordUserId = User.FindFirst("sub")?.Value
                ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (string.IsNullOrEmpty(discordUserId))
                throw new UnauthorizedAccessException("User id missing in token");

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
