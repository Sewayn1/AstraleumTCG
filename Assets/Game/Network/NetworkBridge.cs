namespace Astraleum
{
    /// <summary>
    /// Pont entre la logique de jeu et la couche réseau Mirror.
    /// Pas de dépendance Mirror ici — accessible depuis tous les scripts du projet.
    /// NetworkGameController (dans l'assembly Mirror) populate les délégués au démarrage.
    /// </summary>
    public static class NetworkBridge
    {
        /// <summary>-1 = hors-ligne, 0 = J1 (hôte), 1 = J2 (client).</summary>
        public static int LocalPlayerID = -1;

        /// <summary>Vrai si une session réseau est active.</summary>
        public static bool IsActive => LocalPlayerID >= 0;

        /// <summary>Vrai si on est le serveur/hôte.</summary>
        public static bool IsServer { get; set; }

        // ── Délégués mis en place par NetworkGameController ──────────────

        /// <summary>Appelé quand le joueur local veut finir son tour.</summary>
        public static System.Action OnEndTurnRequested;

        /// <summary>Appelé quand le joueur local veut exécuter un skill.</summary>
        public static System.Action<CardInstance, int, CardInstance> OnExecuteSkillRequested;

        /// <summary>Appelé quand le joueur local commence à cibler (attackerPlayerID, attackerSlot).</summary>
        public static System.Action<int, int> OnArrowShowRequested;

        /// <summary>Appelé quand le joueur local annule ou confirme le ciblage.</summary>
        public static System.Action OnArrowHideRequested;

        /// <summary>Appelé quand le joueur local survole une cible valide (attackerP, attackerSlot, targetP, targetSlot).</summary>
        public static System.Action<int, int, int, int> OnArrowTargetRequested;

        /// <summary>Appelé quand le joueur local quitte une cible survolée.</summary>
        public static System.Action OnArrowTargetHideRequested;

        /// <summary>Appelé quand le joueur local sélectionne une de ses cartes (playerID, slotIndex).</summary>
        public static System.Action<int, int> OnCardSelectedRequested;

        /// <summary>Appelé quand le joueur local désélectionne sa carte.</summary>
        public static System.Action OnCardDeselectedRequested;

        /// <summary>Appelé quand le joueur local abandonne la partie (loserPlayerID).</summary>
        public static System.Action<int> OnGiveUpRequested;

        // ── Reset lors de la déconnexion ──────────────────────────────────

        public static void Reset()
        {
            LocalPlayerID              = -1;
            IsServer                   = false;
            OnEndTurnRequested         = null;
            OnExecuteSkillRequested    = null;
            OnArrowShowRequested       = null;
            OnArrowHideRequested       = null;
            OnArrowTargetRequested     = null;
            OnArrowTargetHideRequested = null;
            OnCardSelectedRequested    = null;
            OnCardDeselectedRequested  = null;
            OnGiveUpRequested          = null;
        }
    }
}
