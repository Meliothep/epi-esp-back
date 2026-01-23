using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Multiplayer.Services
{
    public class SessionManager
    {
        private readonly ConcurrentDictionary<string, object> _seesion = new();

        public SessionManager()
        {
        }
    }
}
