using DnDiscordAPI.Games.Inventory.DTOs;
using DnDiscordAPI.Games.Inventory.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DnDiscordAPI.Games.Controllers
{
    [ApiController]
    [Route("api/games/[controller]")]
    [Authorize]
    public class InventoryController : ControllerBase
    {
        private readonly IInventoryService _inventoryService;

        public InventoryController(IInventoryService inventoryService)
        {
            _inventoryService = inventoryService;
        }

        /// <summary>
        /// Catalogue des objets disponibles (POC, 10 objets seedés).
        /// </summary>
        [HttpGet("catalog")]
        public async Task<ActionResult<List<ItemDto>>> GetCatalog()
        {
            var items = await _inventoryService.GetCatalogAsync();
            return Ok(items);
        }

        /// <summary>
        /// Liste les entrées d'inventaire d'un personnage.
        /// </summary>
        [HttpGet("{characterId:guid}")]
        public async Task<ActionResult<List<InventoryEntryDto>>> GetCharacterInventory(Guid characterId)
        {
            var entries = await _inventoryService.GetCharacterInventoryAsync(characterId);
            return Ok(entries);
        }

        /// <summary>
        /// Ajoute un objet à l'inventaire d'un personnage (MJ → joueur).
        /// </summary>
        [HttpPost("{characterId:guid}")]
        public async Task<ActionResult<InventoryEntryDto>> GiveItem(
            Guid characterId,
            [FromBody] GiveItemRequest request)
        {
            try
            {
                var entry = await _inventoryService.GiveItemAsync(characterId, request);
                return Ok(entry);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { error = ex.Message });
            }
        }

        /// <summary>
        /// Supprime une entrée d'inventaire (jeter l'objet).
        /// </summary>
        [HttpDelete("{characterId:guid}/entry/{entryId:guid}")]
        public async Task<IActionResult> RemoveEntry(Guid characterId, Guid entryId)
        {
            try
            {
                await _inventoryService.RemoveEntryAsync(characterId, entryId);
                return NoContent();
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { error = ex.Message });
            }
        }
    }
}
