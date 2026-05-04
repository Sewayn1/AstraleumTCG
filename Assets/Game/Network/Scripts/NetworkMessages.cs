using System.Collections.Generic;
#if MIRROR
using Mirror;
#endif

namespace Astraleum
{
    // ── Structures de snapshot (toujours compilées) ──────────────────────────

    [System.Serializable]
    public class GameStateSnapshot
    {
        public int currentPlayerID;
        public int actionsRemaining;
        public float timerRemaining;
        public List<CardStateSnapshot> cards = new List<CardStateSnapshot>();
        public int[] stacksP0 = new int[7];
        public int[] stacksP1 = new int[7];
    }

    [System.Serializable]
    public class CardStateSnapshot
    {
        public int playerID;
        public int slotIndex;
        public int currentHP;
        public int currentArmor;
        public bool isAlive;
        public bool hasActedThisTurn;
        public List<ActiveEffect> effects = new List<ActiveEffect>();
    }

#if MIRROR
    // ── Messages client → serveur ────────────────────────────────────────────

    public struct NetMsgExecuteSkill : NetworkMessage
    {
        public int attackerPlayerID;
        public int attackerSlot;
        public int skillIndex;
        public int targetPlayerID;
        public int targetSlot;
    }

    public struct NetMsgEndTurn : NetworkMessage { }

    public struct NetMsgGiveUp : NetworkMessage
    {
        public int loserPlayerID;
    }

    public struct NetMsgArrowUpdate : NetworkMessage
    {
        public bool isShowing;
        public int  attackerPlayerID;
        public int  attackerSlot;
    }

    // ── Messages serveur → clients ───────────────────────────────────────────

    public struct NetMsgGameState : NetworkMessage
    {
        public string stateJson;
    }

    // ── Échange de deck au chargement de Combat ──────────────────────────────

    /// <summary>Chaque machine envoie ses numéros de cartes au serveur.</summary>
    public struct NetMsgDeckInfo : NetworkMessage
    {
        public int    playerID;
        public string cardNumbersCsv; // ex. "1,2,3,4,5"
    }

    /// <summary>Diffuse la carte survolée comme cible de la flèche de ciblage.</summary>
    public struct NetMsgArrowTarget : NetworkMessage
    {
        public bool isShowing;
        public int  attackerPlayerID;
        public int  attackerSlot;
        public int  targetPlayerID;
        public int  targetSlot;
    }

    /// <summary>Diffuse la sélection d'une carte (ouverture SkillPanel).</summary>
    public struct NetMsgCardSelected : NetworkMessage
    {
        public bool isSelected;
        public int  playerID;
        public int  slotIndex;
    }

#endif
}
