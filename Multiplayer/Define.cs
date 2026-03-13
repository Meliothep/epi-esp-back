using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Multiplayer
{
    public class Define
    {
        public enum SessionState
        {
            Lobby,      // En attente de joueurs
            InProgress, // Partie en cours
            Paused,     // Partie en pause
            Ended       // Partie terminée
        }

        public enum PlayerRole
        {
            Player, // Joueurs
            DungeonMaster // DM
        }

        public enum ConnectionStatus
        {
            Connected, // Connecté
            Disconnected, // Déconnecté
            Reconnecting // Reconnexion en cours
        }

        public enum CombatResult
        {
            Victory, // Victoire
            Defeat, // Défaite
            Fled // Fuite
        }

        public enum NarrationStyle
        {
            Regular,   // Narration normale
            NpcVoice,  // Voix d'un PNJ
            Divine,    // Pop-over divin/important
            Whisper    // Message privé à un joueur
        }
    }
}
