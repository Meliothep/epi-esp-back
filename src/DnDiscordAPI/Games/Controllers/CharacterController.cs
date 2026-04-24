using System.Security.Claims;
using DnDiscord.Campaign.Services;
using DnDiscordAPI.Games.Character.DTOs;
using DnDiscordAPI.Games.Character.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DnDiscordAPI.Games.Controllers
{
    [ApiController]
    [Route("api/games/[controller]")]
    [Authorize] // JWT obligatoire
    public class CharacterController : ControllerBase
    {
        private readonly ICharacterService _characterService;
        private readonly IUserContextService _userContext;

        public CharacterController(ICharacterService characterService, IUserContextService userContext)
        {
            _characterService = characterService;
            _userContext = userContext;
        }

        [HttpPost]
        public async Task<ActionResult<CharacterDto>> CreateCharacter([FromBody] CreateCharacterRequest request)
        {
            var discordUserId = User.FindFirst("sub")?.Value
                ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (string.IsNullOrEmpty(discordUserId))
                return Unauthorized(new { error = "User id missing in token" });

            try
            {
                var character = await _characterService.CreateCharacterAsync(discordUserId, request);
                return CreatedAtAction(nameof(GetCharacter), new { id = character.Id }, character);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<CharacterDto>> GetCharacter(Guid id)
        {
            try
            {
                var character = await _characterService.GetCharacterAsync(id);
                return Ok(character);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { error = ex.Message });
            }
        }

        [HttpGet("my-characters")]
        public async Task<ActionResult<List<CharacterDto>>> GetMyCharacters()
        {
            var discordUserId = User.FindFirst("sub")?.Value
                ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (string.IsNullOrEmpty(discordUserId))
                return Unauthorized(new { error = "User id missing in token" });

            var characters = await _characterService.GetUserCharactersAsync(discordUserId);
            return Ok(characters);
        }

        [HttpPatch("{id}/hit-points")]
        public async Task<ActionResult<CharacterDto>> UpdateHitPoints(
            Guid id,
            [FromBody] UpdateHitPointsRequest request)
        {
            try
            {
                var character = await _characterService.UpdateHitPointsAsync(id, request.HitPoints);
                return Ok(character);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { error = ex.Message });
            }
        }

        [HttpPost("{id}/level-up")]
        public async Task<ActionResult<CharacterDto>> LevelUp(Guid id)
        {
            try
            {
                var character = await _characterService.LevelUpAsync(id);
                return Ok(character);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { error = ex.Message });
            }
        }

        [HttpGet("{id}/wallet")]
        public async Task<ActionResult<WalletDto>> GetWallet(Guid id)
        {
            if (!await IsOwnerAsync(id))
                return Forbid();

            try
            {
                var wallet = await _characterService.GetWalletAsync(id);
                return Ok(wallet);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { error = ex.Message });
            }
        }

        [HttpPatch("{id}/wallet")]
        public async Task<ActionResult<WalletDto>> ModifyWallet(Guid id, [FromBody] ModifyWalletRequest request)
        {
            if (!await IsOwnerAsync(id))
                return Forbid();

            try
            {
                var wallet = await _characterService.ModifyWalletAsync(id, request);
                return Ok(wallet);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { error = ex.Message });
            }
        }

        /// <summary>
        /// Wallet and inventory self-management is restricted to the character's owner.
        /// DM coin grants should go through a dedicated DM endpoint (out of scope for POC).
        /// </summary>
        private async Task<bool> IsOwnerAsync(Guid characterId)
        {
            var ownerDiscordId = await _characterService.GetOwnerDiscordIdAsync(characterId);
            return ownerDiscordId != null && ownerDiscordId == _userContext.GetCurrentDiscordUserId();
        }
    }
}
