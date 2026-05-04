using System.Collections.Generic;
using UnityEngine;

namespace Astraleum
{
    public class StackManager : MonoBehaviour
    {
        public static StackManager Instance;

        private Dictionary<int, Dictionary<Element, int>> permanentStacks
            = new Dictionary<int, Dictionary<Element, int>>();

        private Dictionary<int, Dictionary<Element, TemporaryStack>> temporaryStacks
            = new Dictionary<int, Dictionary<Element, TemporaryStack>>();

        private const int MAX_STACKS = 10;

        private void Awake()
        {
            Instance = this;
            InitPlayer(0);
            InitPlayer(1);
        }

        private void InitPlayer(int playerID)
        {
            permanentStacks[playerID] = new Dictionary<Element, int>();
            temporaryStacks[playerID] = new Dictionary<Element, TemporaryStack>();
            foreach (Element e in System.Enum.GetValues(typeof(Element)))
            {
                permanentStacks[playerID][e] = 0;
                temporaryStacks[playerID][e] = new TemporaryStack();
            }
        }

        // ── Stacks permanents ─────────────────────────────────────────

        public void RefreshPermanentStacks()
        {
            for (int p = 0; p < 2; p++)
            {
                var previousStacks = new Dictionary<Element, int>();
                foreach (Element e in System.Enum.GetValues(typeof(Element)))
                {
                    previousStacks[e] = GetStacks(p, e);
                    permanentStacks[p][e] = 0;
                }

                if (BoardManager.Instance == null) continue;
                var cards = BoardManager.Instance.GetAliveCards(p);

                // ── Étape 1 : cartes normales d'abord ────────────────
                foreach (var card in cards)
                {
                    if (card.data.element == Element.Astral) continue;
                    permanentStacks[p][card.data.element]
                        = Mathf.Min(permanentStacks[p][card.data.element] + 1, MAX_STACKS);
                }

                // ── Étape 2 : cartes Astral après ────────────────────
                // La liste des vivants est déjà à jour — GetCardToTheLeft ne trouvera pas les cartes mortes
                foreach (var card in cards)
                {
                    if (card.data.element != Element.Astral) continue;

                    var leftCard = BoardManager.Instance.GetCardToTheLeft(card);
                    if (leftCard != null && leftCard.data.element != Element.Astral)
                    {
                        permanentStacks[p][leftCard.data.element]
                            = Mathf.Min(permanentStacks[p][leftCard.data.element] + 1, MAX_STACKS);
                    }
                }

                // ── Détection changements de seuil ────────────────────
                foreach (Element e in System.Enum.GetValues(typeof(Element)))
                {
                    int oldCount = previousStacks.ContainsKey(e) ? previousStacks[e] : 0;
                    int newCount = GetStacks(p, e);

                    if (oldCount < 3 && newCount >= 3)
                        PassiveManager.Instance?.OnStacksChanged(p, e, 3);
                    if (oldCount < 5 && newCount >= 5)
                        PassiveManager.Instance?.OnStacksChanged(p, e, 5);
                    if (oldCount >= 5 && newCount < 5)
                        PassiveManager.Instance?.OnStackThresholdLost(p, e, 5);
                    if (oldCount >= 3 && newCount < 3)
                        PassiveManager.Instance?.OnStackThresholdLost(p, e, 3);
                }
            }
        }



        // ── Stacks temporaires ────────────────────────────────────────

        public void AddTemporaryStack(int playerID, Element element, int amount, int duration)
        {
            var ts = temporaryStacks[playerID][element];
            ts.count = Mathf.Min(ts.count + amount, MAX_STACKS);
            ts.duration = Mathf.Max(ts.duration, duration);
        }

        public void OnTurnEnd(int playerID)
        {
            foreach (Element e in System.Enum.GetValues(typeof(Element)))
            {
                var ts = temporaryStacks[playerID][e];
                if (ts.count <= 0) continue;
                ts.duration--;
                if (ts.duration <= 0) { ts.count = 0; ts.duration = 0; }
            }

        }

        // ── Total stacks ──────────────────────────────────────────────

        public int GetStacks(int playerID, Element element)
        {
            int perm = permanentStacks[playerID][element];
            int temp = temporaryStacks[playerID][element].count;
            return Mathf.Min(perm + temp, MAX_STACKS);
        }

        /// <summary>Applique directement la valeur totale d'un stack (utilisé par la sync réseau côté client).</summary>
        public void SetStacks(int playerID, Element element, int totalValue)
        {
            permanentStacks[playerID][element] = Mathf.Clamp(totalValue, 0, MAX_STACKS);
            temporaryStacks[playerID][element] = new TemporaryStack(); // reset temp — total via permanent
        }

        // ─────────────────────────────────────────────────────────────
        // APPLICATION DES BONUS
        // Mineur → TOUTES les cartes alliées
        // Majeur → UNIQUEMENT les cartes du même élément
        // ─────────────────────────────────────────────────────────────

        public void ApplyTurnBonuses(int playerID)
        {
            if (BoardManager.Instance == null) return;
            // Mineur Terre → appliqué une seule fois au démarrage (ApplyEarthMinorBonusOnGameStart)
            // Autres mineurs → passifs dans CalculateDamage / Heal
            ApplyMajorBonuses(playerID);
        }

        // ── Bonus MINEURS (toutes les cartes alliées) ─────────────────

        public void ApplyEarthMinorBonusOnGameStart(int playerID)
        {
            int earthStacks = GetStacks(playerID, Element.Terre);
            if (earthStacks <= 0) return;

            int armorGain = earthStacks * 5;
            var allies = BoardManager.Instance.GetAliveCards(playerID);

            foreach (var ally in allies)
            {
                ally.currentArmor = Mathf.Min(ally.currentArmor + armorGain, 100);
                CombatLogManager.Instance?.AddEntry(
                    $"{ally.data.cardName} +{armorGain} armure (Terre mineur, début de combat)");
            }
            Debug.Log($"[StackManager] Terre mineur appliqué une fois P{playerID} : +{armorGain} armure");
        }

        // 🌱 Terre majeur 3/5 → armure par tour aux alliés
        public int GetEarthArmorRegen(int playerID)
        {
            int s = GetStacks(playerID, Element.Terre);
            if (s >= 5) return 5; // ← 5 armure/tour
            if (s >= 3) return 3; // ← 3 armure/tour
            return 0;
        }

        // 🔥 Feu mineur : +3% dégâts/stack → appliqué passivement dans CalculateDamage
        // 💧 Eau mineur : -2% dégâts subis/stack → appliqué passivement dans CalculateDamage
        // 🌪️ Air mineur : +1% chance relance/stack → appliqué passivement dans CombatManager
        // ✨ Lumière mineur : +2% efficacité soins/stack → appliqué passivement dans CardInstance.Heal
        // 🌑 Ténèbres mineur : +3% dégâts indirects/stack → appliqué passivement dans SkillExecutor

        // ── Bonus MAJEURS (cartes du même élément uniquement) ─────────

        private void ApplyMajorBonuses(int playerID)
        {
            if (BoardManager.Instance == null) return;
            var allies = BoardManager.Instance.GetAliveCards(playerID);

            // 🌱 Terre majeur 3/5 : régénération armure → TOUS les alliés sans exception
            int earthRegen = GetEarthArmorRegen(playerID);
            if (earthRegen > 0)
            {
                foreach (var a in allies)
                {
                    a.RestoreArmor(earthRegen);
                    CombatLogManager.Instance?.AddEntry(
                        $"{a.data.cardName} +{earthRegen} armure/tour (Terre majeur)");
                }
            }

            // ✨ Lumière → RETIRÉ ICI → géré dans ProcessActiveEffects

            // 🌑 Ténèbres majeur 3/5 : poison aux ennemis
            ApplyPoisonToEnemies(playerID);

            // 🌑 Ténèbres majeur 5 : soin 3% PV max → cartes Ténèbres uniquement
            if (GetStacks(playerID, Element.Tenebres) >= 5)
            {
                foreach (var a in allies)
                {
                    if (a.data.element != Element.Tenebres) continue;
                    int darkHeal = Mathf.RoundToInt(a.data.maxHP * 0.03f);
                    a.Heal(darkHeal, false);
                    CombatLogManager.Instance?.AddEntry(
                        $"{a.data.cardName} soigné de {darkHeal} PV (Ténèbres 5 stacks)");
                }
            }

            // 🌪️ Air majeur 5 → +1 action → géré dans TurnManager
            // 💧 Eau majeur 3/5 → réduction ennemis → passif dans CalculateDamage
            // 🔥 Feu majeur 3/5 → splash → passif dans SkillExecutor
        }

        // ── Poison (Ténèbres majeur 3/5) ─────────────────────────────

        public void ApplyPoisonToEnemies(int attackerPlayerID)
        {
            float poisonPct = GetPoisonPercent(attackerPlayerID);
            if (poisonPct <= 0f) return;

            int enemyID = 1 - attackerPlayerID;
            var enemies = BoardManager.Instance.GetAliveCards(enemyID);

            foreach (var enemy in enemies)
            {
                enemy.ApplyEffect(new ActiveEffect
                {
                    type           = EffectType.Poison,
                    value          = poisonPct,
                    remainingTurns = 2,
                    sourceName     = "Ténèbres majeur"
                });
                CombatLogManager.Instance?.AddEntry(
                    $"{enemy.data.cardName} empoisonné ({poisonPct * 100:0}% PV/tour, Ténèbres majeur)");
            }
        }

        // ── Calculs passifs (utilisés dans CalculateDamage) ───────────

        // 🔥 Feu mineur → bonus dégâts TOUTES les cartes alliées
        public float GetFireDamageBonus(int playerID)
        {
            return GetStacks(playerID, Element.Feu) * 0.03f;
        }

        // 🔥 Feu majeur 3 → splash adjacents
        public bool FireSplashAdjacent(int playerID)
            => GetStacks(playerID, Element.Feu) >= 3;

        // 🔥 Feu majeur 5 → splash toutes cibles
        public bool FireSplashAll(int playerID)
            => GetStacks(playerID, Element.Feu) >= 5;

        // 💧 Eau mineur → réduction dégâts subis TOUTES les cartes alliées
        public float GetWaterDamageReduction(int playerID)
        {
            int s = GetStacks(playerID, Element.Eau);
            float red = s * 0.02f;
            // Majeur : bonus supplémentaire aux cartes Eau uniquement
            // (géré séparément dans CalculateDamage si target.data.element == Eau)
            return red;
        }

        // 💧 Eau majeur 3/5 → réduction dégâts ennemis (cartes Eau uniquement)
        public float GetWaterMajorEnemyReduction(int playerID)
        {
            int s = GetStacks(playerID, Element.Eau);
            if (s >= 5) return 0.10f;
            if (s >= 3) return 0.05f;
            return 0f;
        }

        // 🌱 Terre mineur → armure fixe TOUTES les cartes
        public int GetEarthArmorBonus(int playerID)
        {
            return GetStacks(playerID, Element.Terre) * 2;
        }

        public int GetTotalArmorBonus(int playerID)
        {
            return GetEarthArmorBonus(playerID);
        }

        // 🌱 Terre majeur 5 → -3% dégâts subis cartes Terre uniquement
        public float GetEarthMajorDamageReduction(int playerID)
        {
            return GetStacks(playerID, Element.Terre) >= 5 ? 0.03f : 0f;
        }

        // 🌪️ Air mineur → chance relance TOUTES les cartes
        public float GetAirReplayChance(int playerID)
        {
            int s = GetStacks(playerID, Element.Air);
            float base_ = s * 0.01f;
            // Majeur 3 → +2% supplémentaire cartes Air (géré dans CombatManager)
            return base_;
        }

        // 🌪️ Air majeur 3 → +2% chance relance cartes Air uniquement
        public float GetAirMajorReplayBonus(int playerID)
        {
            return GetStacks(playerID, Element.Air) >= 3 ? 0.02f : 0f;
        }

        // 🌪️ Air majeur 5 → +1 action cartes Air uniquement
        public bool AirGrantsExtraAction(int playerID)
            => GetStacks(playerID, Element.Air) >= 5;

        // ✨ Lumière mineur → efficacité soins TOUTES les cartes
        public float GetHealBonus(int playerID)
        {
            return GetStacks(playerID, Element.Lumiere) * 0.02f;
        }

        // ✨ Lumière majeur 3/5 → HoT cartes Lumière uniquement
        public float GetLightHoTPercent(int playerID)
        {
            int s = GetStacks(playerID, Element.Lumiere);
            if (s >= 5) return 0.07f;
            if (s >= 3) return 0.03f;
            return 0f;
        }

        // 🌑 Ténèbres mineur → dégâts indirects TOUTES les cartes
        public float GetDarkIndirectBonus(int playerID)
        {
            return GetStacks(playerID, Element.Tenebres) * 0.03f;
        }

        // 🌑 Ténèbres majeur 3/5 → poison cartes Ténèbres uniquement
        public float GetPoisonPercent(int playerID)
        {
            int s = GetStacks(playerID, Element.Tenebres);
            if (s >= 5) return 0.06f;
            if (s >= 3) return 0.03f;
            return 0f;
        }

        // 🌌 Astral → copie élément carte à gauche
        public Element? GetAstralElement(CardInstance astralCard)
        {
            if (astralCard == null || astralCard.data.element != Element.Astral)
                return null;

            var leftCard = BoardManager.Instance?.GetCardToTheLeft(astralCard);
            if (leftCard == null || leftCard.data.element == Element.Astral)
                return null;

            return leftCard.data.element;
        }

        public int GetAstralStackBonus(CardInstance astralCard)
        {
            return GetAstralElement(astralCard).HasValue ? 1 : 0;
        }
    }

    public class TemporaryStack
    {
        public int count;
        public int duration;
    }
}