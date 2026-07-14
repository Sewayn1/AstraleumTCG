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

            // Corrosif : plafonne les HP de toutes les cartes après changement de stacks
            if (BoardManager.Instance != null)
            {
                foreach (var card in BoardManager.Instance.GetAliveCards(0))
                    card.ClampCurrentHP();
                foreach (var card in BoardManager.Instance.GetAliveCards(1))
                    card.ClampCurrentHP();
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
            ApplyMajorBonuses(playerID);
        }

        // ── Bonus MINEURS (toutes les cartes alliées) ─────────────────

        // No-op — Terre mineur est désormais un passif de réduction de dégâts (GetEarthDamageReduction)
        public void ApplyEarthMinorBonusOnGameStart(int playerID) { }

        // 🌱 Terre mineur : -2% DGT subis/stack → appliqué passivement dans DamageCalculator
        public float GetEarthDamageReduction(int playerID)
        {
            return GetStacks(playerID, Element.Terre) * 0.02f;
        }

        // 🌱 Terre majeur 3/5 → armure permanente
        public int GetEarthArmorRegen(int playerID)
        {
            int s = GetStacks(playerID, Element.Terre);
            if (s >= 5) return 5;
            if (s >= 3) return 3;
            return 0;
        }

        // 🔥 Feu mineur : +3% dégâts/stack → appliqué passivement dans CalculateDamage
        // 💧 Eau mineur : -2% dégâts subis/stack → appliqué passivement dans CalculateDamage
        // 🌱 Terre mineur : -2% DGT subis/stack → appliqué passivement dans CalculateDamage
        // 🌪️ Air mineur : +2% crit/stack, max 10% → CardInstance.EffectiveCritChance
        // ✨ Lumière mineur : +2% efficacité soins/stack → appliqué passivement dans CardInstance.Heal
        // 🌑 Ténèbres mineur : -1 armure/stack, max -5 pour cartes adverses → CardInstance.TotalArmor

        // ── Bonus MAJEURS (cartes du même élément uniquement) ─────────

        private void ApplyMajorBonuses(int playerID)
        {
            if (BoardManager.Instance == null) return;
            var allies = BoardManager.Instance.GetAliveCards(playerID);

            // 🌱 Terre majeur 3/5 : armure permanente → TOUS les alliés
            int earthArmor = GetEarthArmorRegen(playerID);
            if (earthArmor > 0)
            {
                foreach (var a in allies)
                {
                    a.ApplyEffect(new ActiveEffect
                    {
                        type            = EffectType.GiveArmor,
                        value           = earthArmor,
                        remainingTurns  = -1,
                        sourceName      = "Terre",
                        sourceSkillName = "majeur",
                    });
                }
            }
            else
            {
                foreach (var a in allies)
                    a.activeEffects.RemoveAll(e =>
                        e.type == EffectType.GiveArmor &&
                        e.sourceName == "Terre" &&
                        e.sourceSkillName == "majeur");
            }

            // ✨ Lumière → RETIRÉ ICI → géré dans ProcessActiveEffects

            // 🌪️ Air majeur 3/5 → crit → passif dans CardInstance.EffectiveCritChance/EffectiveCritDamageBonus
            // 🌑 Ténèbres majeur 3/5 → LifeSteal → passif dans SkillExecutor.ApplyLifeSteal
            // 💧 Eau majeur 3/5 → réduction ennemis → passif dans CalculateDamage
            // 🔥 Feu majeur 3/5 → splash → passif dans SkillExecutor
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

        // 🔥 Feu majeur 5 → +10% DGT critiques (cartes Feu uniquement)
        public float GetFireMajorCritDamageBonus(int playerID)
            => GetStacks(playerID, Element.Feu) >= 5 ? 0.10f : 0f;

        // 🔥 Feu majeur 5 → +5% chance critique (cartes Feu uniquement)
        public float GetFireMajorCritChanceBonus(int playerID)
            => GetStacks(playerID, Element.Feu) >= 5 ? 0.05f : 0f;

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

        // 🌪️ Air mineur → +2% crit/stack, max 10% — toutes les cartes alliées
        public float GetAirCritChanceBonus(int playerID)
            => Mathf.Min(GetStacks(playerID, Element.Air) * 0.02f, 0.10f);

        // 🌪️ Air majeur 3/5 → bonus DGT critique — toutes les cartes alliées
        public float GetAirMajorCritDamageBonus(int playerID)
        {
            int s = GetStacks(playerID, Element.Air);
            if (s >= 5) return 0.10f;
            if (s >= 3) return 0.05f;
            return 0f;
        }

        // 🌪️ Air majeur 5 → +5% crit supplémentaire — toutes les cartes alliées
        public float GetAirMajorCritChanceBonus(int playerID)
            => GetStacks(playerID, Element.Air) >= 5 ? 0.05f : 0f;

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

        // 🌑 Ténèbres mineur → +2% DGT par carte adverse en vie (toutes cartes alliées)
        public float GetDarkDamageBonus(int playerID)
        {
            if (GetStacks(playerID, Element.Tenebres) == 0) return 0f;
            int aliveEnemies = BoardManager.Instance?.GetAliveCards(1 - playerID).Count ?? 0;
            return aliveEnemies * 0.02f;
        }

        // 🟢 Corrosif mineur → -1 armure/stack, dynamique (toutes cartes adverses)
        public int GetCorrosifArmorReduction(int playerID)
            => GetStacks(playerID, Element.Corrosif);

        // 🟢 Corrosif majeur 3/5 → -5%/-10% PV Max (toutes cartes adverses, dynamique)
        public float GetCorrosifMaxHPReduction(int playerID)
        {
            int s = GetStacks(playerID, Element.Corrosif);
            if (s >= 5) return 0.10f;
            if (s >= 3) return 0.05f;
            return 0f;
        }

        // 🌑 Ténèbres majeur 3/5 → bonus Vol de Vie cartes Ténèbres uniquement
        public float GetDarkLifeStealBonus(int playerID)
        {
            int s = GetStacks(playerID, Element.Tenebres);
            if (s >= 5) return 0.10f;
            if (s >= 3) return 0.05f;
            return 0f;
        }

        // ⚫ Nécrotique mineur → +1 DGT aux ticks Nécrose par carte Nécrotique EN JEU
        // (les deux joueurs confondus) — toujours actif, non gated par un seuil de stack
        public int GetNecrotiqueBoardCount()
        {
            if (BoardManager.Instance == null) return 0;
            int count = 0;
            for (int p = 0; p < 2; p++)
                foreach (var c in BoardManager.Instance.GetAliveCards(p))
                    if (c.data.element == Element.Necrotique) count++;
            return count;
        }

        // ⚫ Nécrotique majeur 3/5 → chance de résurrection (cartes Nécrotique alliées uniquement)
        public float GetNecroticReviveChance(int playerID)
        {
            int s = GetStacks(playerID, Element.Necrotique);
            if (s >= 5) return 0.10f;
            if (s >= 3) return 0.05f;
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