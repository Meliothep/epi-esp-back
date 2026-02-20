using DnDiscordAPI.Messages.Services;
using DnDiscordAPI.Messages.Hubs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DnDiscordAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class MessageController : ControllerBase
    {
        private readonly SignalRService _signalRService;

        public MessageController(SignalRService signalRService)
        {
            _signalRService = signalRService;
        }

        [HttpGet]
        public IActionResult GetMessage()
        {
            return Ok("Hello from MessageController!");
        }

        /// <summary>
        /// Reçoit un message du bot Discord et le diffuse au front via SignalR (ReceiveMessage).
        /// Appelé par le bot sur POST /api/message avec le body MessageDto.
        /// </summary>
        [AllowAnonymous]
        [HttpPost]
        public async Task<IActionResult> Post([FromBody] MessageDto message)
        {
            try
            {
                if (message == null || string.IsNullOrEmpty(message.Content))
                {
                    return BadRequest(new { error = "Le message ne peut pas être vide" });
                }

                await _signalRService.SendMessageToFront(message);

                return Ok(new { status = "Message reçu et publié", message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }
    }
}

