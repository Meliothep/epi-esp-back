using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Multiplayer.Models
{
    public class JoinResult
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public GameSession? Session { get; set; }
        public SessionPlayer? PlayerInfo { get; set; }

        public static JoinResult Ok(GameSession session, SessionPlayer player)
            => new() { Success = true, Session = session, PlayerInfo = player };

        public static JoinResult Fail(string message)
            => new() { Success = false, Message = message };
    }
}
