using AutoMapper;
using DnDiscordAPI.Games.Inventory.DTOs;
using DnDiscordAPI.Games.Inventory.Models;

namespace DnDiscordAPI.Games.Inventory.Mappings
{
    public class InventoryMappingProfile : Profile
    {
        public InventoryMappingProfile()
        {
            CreateMap<Item, ItemDto>();
            CreateMap<InventoryEntry, InventoryEntryDto>();
        }
    }
}
