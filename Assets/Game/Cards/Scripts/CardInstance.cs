using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Astraleum
{
    public class CardInstance : MonoBehaviour
    {
        [Header("Données")]
        public CardData data;

        [Header("État en jeu")]
        public int currentHP;
        public int currentArmor;   // ← Pool HP secondaire absorbé en premier
        public int slotIndex;
        public int ownerPlayerID;
        public bool hasActedThisTurn;
        public int bonusActionsRemaining;
        public int skill1Cooldown;
        public int skill2Cooldown;

        public List<ActiveEffect> activeEffects = new List<ActiveEffect>();

        // Compteur pour les passifs stacksPerTrigger (ex. "pour chaque allié détruit")
        public int passiveStackCount = 0;

        public bool IsAlive      => currentHP > 0;
        public bool IsInvisible  => activeEffects.Any(e => e.type == EffectType.Invisible);
        public bool IsReady => (!hasActedThisTurn || bonusActionsRemaining > 0)
                            && (skill1Cooldown == 0 || skill2Cooldown == 0)
                            && !activeEffects.Any(e => e.type == EffectType.Stun);

        // ── Initialisation ────────────────────────────────────────────


        private IEnumerator ShowPopupNextFrame(CombatPopupHandler popup,
                                                int amount, bool isHeal)
        {
            yield return null; // attend 1 frame
            if (isHeal) popup.ShowHealPopup(amount);
            else popup.ShowDamagePopup(amount);
        }

        public void Initialize(CardData cardData, int slot, int playerID)
        {
            data = cardData;
            slotIndex = slot;
            ownerPlayerID = playerID;
            currentHP = cardData.maxHP;
            currentArmor = cardData.armorPoints;
            hasActedThisTurn = false;
            bonusActionsRemaining = 0;
            skill1Cooldown = 0;
            skill2Cooldown = 0;
            activeEffects.Clear();
            passiveStackCount = 0;
        }

        // ── Tour ──────────────────────────────────────────────────────

        public void OnTurnStart()
        {
            hasActedThisTurn = false;
            if (skill1Cooldown > 0) skill1Cooldown--;
            if (skill2Cooldown > 0) skill2Cooldown--;
            ProcessActiveEffects();

            // Force la mise à jour visuelle immédiate
            GetComponent<CardVisualUpdater>()?.UpdateVisuals();
        }

        public void UseSkill(int skillIndex)
        {
            if (hasActedThisTurn && bonusActionsRemaining > 0)
                bonusActionsRemaining--;
            hasActedThisTurn = true;
            int cd = skillIndex == 0
                ? data.skillOne?.cooldownTurns ?? 0
                : data.skillTwo?.cooldownTurns ?? 0;
            if (skillIndex == 0) skill1Cooldown = cd;
            else skill2Cooldown = cd;

            // Utiliser une compétence brise l'invisibilité jusqu'au prochain tour
            activeEffects.RemoveAll(e => e.type == EffectType.Invisible);
        }

        // ── Dégâts ────────────────────────────────────────────────────

        public int TakeDamage(int damage, bool ignoreArmor = false)
        {
            if (damage <= 0) return 0;

            int remaining = damage;

            if (!ignoreArmor && currentArmor > 0)
            {
                int absorbed = Mathf.Min(currentArmor, remaining);
                currentArmor -= absorbed;
                remaining -= absorbed;
            }

            if (remaining > 0)
            {
                currentHP -= remaining;
                currentHP = Mathf.Max(0, currentHP);
            }

            return damage; // dégâts totaux infligés
        }

        // ── Soins ─────────────────────────────────────────────────────

        public int Heal(int amount, bool showPopup = true)
        {
            bool healBlocked = activeEffects.Any(e => e.type == EffectType.HealBlock);
            if (healBlocked || amount <= 0) return 0;

            float healBonusMultiplier = 1f;
            if (StackManager.Instance != null)
                healBonusMultiplier = 1f + StackManager.Instance.GetHealBonus(ownerPlayerID);

            int boostedAmount = Mathf.RoundToInt(amount * healBonusMultiplier);
            int before = currentHP;
            currentHP = Mathf.Min(currentHP + boostedAmount, data.maxHP);
            int actual = currentHP - before;

            if (actual > 0 && showPopup)
                GetComponent<CombatPopupHandler>()?.ShowHealPopup(actual);

            return actual;
        }

        private const int MAX_ARMOR = 100;

        public void RestoreArmor(int amount)
        {
            currentArmor = Mathf.Min(currentArmor + amount, MAX_ARMOR);
        }

        // Donne de l'armure sans tenir compte de la valeur initiale — plafond 100
        public void AddArmor(int amount)
        {
            currentArmor = Mathf.Min(currentArmor + amount, 100);
            GetComponent<CombatPopupHandler>()?.ShowHealPopup(amount);
        }

        // ── Effets actifs ─────────────────────────────────────────────

        private const float MAX_DAMAGE_REDUCTION = 0.5f;

        public void ApplyEffect(ActiveEffect newEffect)
        {
            // ── Burn — même source → rafraîchit, source différente → empile ──
            if (newEffect.type == EffectType.Burn)
            {
                var sameSrc = activeEffects.Find(e =>
                    e.type == EffectType.Burn &&
                    e.sourceName == newEffect.sourceName &&
                    e.sourceSkillName == newEffect.sourceSkillName);
                if (sameSrc != null)
                {
                    sameSrc.value          = Mathf.Max(sameSrc.value, newEffect.value);
                    sameSrc.remainingTurns = Mathf.Max(sameSrc.remainingTurns, newEffect.remainingTurns);
                }
                else
                    activeEffects.Add(newEffect);
                return;
            }

            // ── Poison — valeur max, durée max ───────────────────────────
            if (newEffect.type == EffectType.Poison)
            {
                var existing = activeEffects.Find(e => e.type == EffectType.Poison);
                if (existing != null)
                {
                    existing.value          = Mathf.Max(existing.value, newEffect.value);
                    existing.remainingTurns = Mathf.Max(existing.remainingTurns, newEffect.remainingTurns);
                    if (newEffect.sourcePassiveTrigger.HasValue)
                    {
                        existing.sourcePassiveTrigger = newEffect.sourcePassiveTrigger;
                        existing.sourceElement        = newEffect.sourceElement;
                    }
                    if (!string.IsNullOrEmpty(newEffect.sourceName))
                        existing.sourceName = newEffect.sourceName;
                    return;
                }
            }

            // ── DamageReduction — cumulatif, plafonné à 50% ───────────────
            if (newEffect.type == EffectType.DamageReduction)
            {
                var existing = activeEffects.Find(e => e.type == EffectType.DamageReduction);
                if (existing != null)
                {
                    existing.value          = Mathf.Min(existing.value + newEffect.value, MAX_DAMAGE_REDUCTION);
                    existing.remainingTurns = Mathf.Max(existing.remainingTurns, newEffect.remainingTurns);
                    if (newEffect.sourcePassiveTrigger.HasValue)
                    {
                        existing.sourcePassiveTrigger = newEffect.sourcePassiveTrigger;
                        existing.sourceElement        = newEffect.sourceElement;
                    }
                    if (!string.IsNullOrEmpty(newEffect.sourceName))
                    {
                        if (!string.IsNullOrEmpty(existing.sourceName)
                            && existing.sourceName != newEffect.sourceName)
                            existing.sourceName += " + " + newEffect.sourceName;
                        else
                            existing.sourceName = newEffect.sourceName;
                    }
                    return;
                }
                // Premier effet : plafonne quand même la valeur initiale
                newEffect.value = Mathf.Min(newEffect.value, MAX_DAMAGE_REDUCTION);
            }

            // ── AttackReduction — cumulatif, plafonné à 50% ───────────────
            if (newEffect.type == EffectType.AttackReduction)
            {
                var existing = activeEffects.Find(e => e.type == EffectType.AttackReduction);
                if (existing != null)
                {
                    existing.value          = Mathf.Min(existing.value + newEffect.value, MAX_DAMAGE_REDUCTION);
                    existing.remainingTurns = Mathf.Max(existing.remainingTurns, newEffect.remainingTurns);
                    if (newEffect.sourcePassiveTrigger.HasValue)
                    {
                        existing.sourcePassiveTrigger = newEffect.sourcePassiveTrigger;
                        existing.sourceElement        = newEffect.sourceElement;
                    }
                    if (!string.IsNullOrEmpty(newEffect.sourceName))
                    {
                        if (!string.IsNullOrEmpty(existing.sourceName)
                            && existing.sourceName != newEffect.sourceName)
                            existing.sourceName += " + " + newEffect.sourceName;
                        else
                            existing.sourceName = newEffect.sourceName;
                    }
                    return;
                }
                newEffect.value = Mathf.Min(newEffect.value, MAX_DAMAGE_REDUCTION);
            }

            // ── Saignement — même source → rafraîchit, source différente → empile ───
            if (newEffect.type == EffectType.Saignement)
            {
                var sameSrc = activeEffects.Find(e =>
                    e.type == EffectType.Saignement &&
                    e.sourceName == newEffect.sourceName &&
                    e.sourceSkillName == newEffect.sourceSkillName);
                if (sameSrc != null)
                    sameSrc.remainingTurns = Mathf.Max(sameSrc.remainingTurns, newEffect.remainingTurns);
                else
                    activeEffects.Add(newEffect);
                return;
            }


            // ── Autres effets — no-stack (valeur max) ─────────────────────
            var exist = activeEffects.Find(e => e.type == newEffect.type);
            if (exist != null)
            {
                exist.value          = Mathf.Max(exist.value, newEffect.value);
                exist.remainingTurns = Mathf.Max(exist.remainingTurns, newEffect.remainingTurns);
                if (newEffect.sourcePassiveTrigger.HasValue)
                {
                    exist.sourcePassiveTrigger = newEffect.sourcePassiveTrigger;
                    exist.sourceElement        = newEffect.sourceElement;
                }
                if (!string.IsNullOrEmpty(newEffect.sourceName))
                    exist.sourceName = newEffect.sourceName;
            }
            else
            {
                activeEffects.Add(newEffect);
            }
        }

        private bool IsPassiveEffectStillValid(ActiveEffect effect)
        {
            // Si pas de tag passif → toujours valide
            if (!effect.sourcePassiveTrigger.HasValue) return true;

            var trigger = effect.sourcePassiveTrigger.Value;

            // Vérifie les seuils de stacks
            if (trigger == PassiveTrigger.OnStackThreshold3 ||
                trigger == PassiveTrigger.OnStackThreshold5)
            {
                if (StackManager.Instance == null) return false;

                int requiredThreshold = trigger == PassiveTrigger.OnStackThreshold5 ? 5 : 3;
                int currentStacks = StackManager.Instance.GetStacks(
                                            ownerPlayerID, effect.sourceElement);

                // L'effet n'est valide que si le seuil est encore atteint
                return currentStacks >= requiredThreshold;
            }

            // Autres triggers → toujours valide
            return true;
        }
        // Retourne le multiplicateur de réduction de dégâts actif (ex. 0.8 = -20%)
        private float GetDamageReductionMultiplier()
        {
            float mult = 1f;
            foreach (var eff in activeEffects)
                if (eff.type == EffectType.DamageReduction)
                    mult *= 1f - eff.value;
            return mult;
        }

        public void ProcessActiveEffects()
        {
            int dotTotal = 0;
            int hotTotal = 0;
            bool healBlocked = activeEffects.Any(e => e.type == EffectType.HealBlock);

            // Calcul unique de la réduction dégâts pour ce tour
            float dmgReductMult = GetDamageReductionMultiplier();

            // ── Effets passifs conditionnels (seuils de stacks) ───────
            foreach (var cpe in conditionalPassiveEffects.ToList())
            {
                // Vérifie si le seuil est encore actif
                // IMPORTANT : utilise ownerPlayerID du passif, pas de la carte cible
                int currentStacks = StackManager.Instance != null
                    ? StackManager.Instance.GetStacks(cpe.ownerPlayerID, cpe.triggerElement)
                    : 0;

                if (currentStacks < cpe.requiredThreshold)
                {
                    conditionalPassiveEffects.Remove(cpe);
                    Debug.Log($"[Passif] {data.cardName} — effet {cpe.type} retiré " +
                              $"(stacks {cpe.triggerElement} = {currentStacks} < {cpe.requiredThreshold})");
                    continue;
                }

                // Applique sur la bonne cible selon effectTarget
                switch (cpe.type)
                {
                    case EffectType.Saignement:
                        {
                            int dot = Mathf.RoundToInt(data.maxHP * cpe.value);
                            if (StackManager.Instance != null)
                                dot = Mathf.RoundToInt(dot * (1f + StackManager.Instance.GetDarkIndirectBonus(cpe.ownerPlayerID)));
                            dot = Mathf.RoundToInt(dot * dmgReductMult);
                            TakeDamage(dot);
                            dotTotal += dot;
                            CombatLogManager.Instance?.AddEntry(
                                $"{data.cardName} -{dot} DGT (Passif Saignement)");
                            break;
                        }
                }
            }

            // ── Effets actifs normaux ─────────────────────────────────
            // Collecte le soin Lumière
            if (!healBlocked && StackManager.Instance != null)
            {
                float lightHoT = StackManager.Instance.GetLightHoTPercent(ownerPlayerID);
                if (lightHoT > 0f)
                {
                    int lightHeal = Mathf.RoundToInt(data.maxHP * lightHoT);
                    int before = currentHP;
                    currentHP = Mathf.Min(currentHP + lightHeal, data.maxHP);
                    hotTotal += currentHP - before;
                }
            }

            foreach (var effect in activeEffects.ToList())
            {
                switch (effect.type)
                {
                    case EffectType.Saignement:
                        {
                            int dot = Mathf.RoundToInt(data.maxHP * effect.value);
                            if (StackManager.Instance != null)
                                dot = Mathf.RoundToInt(dot * (1f + StackManager.Instance
                                      .GetDarkIndirectBonus(ownerPlayerID)));
                            dot = Mathf.RoundToInt(dot * dmgReductMult);
                            TakeDamage(dot);
                            dotTotal += dot;
                            CombatLogManager.Instance?.AddEntry(
                                $"{data.cardName} -{dot} DGT (Saignement)");
                            break;
                        }

                    case EffectType.Burn:
                        {
                            int burnDmg = Mathf.RoundToInt(data.maxHP * effect.value);
                            burnDmg = Mathf.RoundToInt(burnDmg * dmgReductMult);
                            TakeDamage(burnDmg);
                            dotTotal += burnDmg;
                            CombatLogManager.Instance?.AddEntry(
                                $"{data.cardName} -{burnDmg} DGT (Brûlure)");
                            break;
                        }

                    case EffectType.Poison:
                        {
                            int poisonDmg = Mathf.RoundToInt(data.maxHP * effect.value);
                            if (StackManager.Instance != null)
                                poisonDmg = Mathf.RoundToInt(poisonDmg * (1f + StackManager.Instance
                                            .GetDarkIndirectBonus(ownerPlayerID)));
                            poisonDmg = Mathf.RoundToInt(poisonDmg * dmgReductMult);
                            TakeDamage(poisonDmg, ignoreArmor: true);
                            dotTotal += poisonDmg;
                            break;
                        }

                    case EffectType.HealOverTime:
                        {
                            if (!healBlocked)
                            {
                                int hot = Mathf.RoundToInt(data.maxHP * effect.value);
                                int before = currentHP;
                                currentHP = Mathf.Min(currentHP + hot, data.maxHP);
                                hotTotal += currentHP - before;
                            }
                            break;
                        }
                }

                // Stun est décrémenté à la FIN du tour du joueur affecté (dans TurnManager)
                if (effect.remainingTurns != -1 && effect.type != EffectType.Stun)
                {
                    effect.remainingTurns--;
                    if (effect.remainingTurns <= 0)
                        activeEffects.Remove(effect);
                }
            }

            if (dotTotal > 0 || hotTotal > 0)
                StartCoroutine(ShowEffectPopupsSequenced(dotTotal, hotTotal));

            if (!IsAlive)
            {
                BoardManager.Instance?.DestroyCard(this);
                CombatLogManager.Instance?.AddEntry(
                    $"{data.cardName} est détruit !", isDeathEntry: true);
            }
        }

        private IEnumerator ShowEffectPopupsSequenced(int dotTotal, int hotTotal)
        {
            var popup = GetComponent<CombatPopupHandler>();
            if (popup == null) yield break;

            // DoT en premier
            if (dotTotal > 0)
            {
                yield return null;
                popup.ShowDamagePopup(dotTotal);
            }

            // HoT après 2 secondes — masque le DoT
            if (hotTotal > 0)
            {
                yield return new WaitForSeconds(dotTotal > 0 ? 2f : 0f);
                popup.HideDamagePopupImmediate();
                yield return null;
                popup.ShowHealPopup(hotTotal);
            }
        }

        public void RemovePassiveEffects(PassiveTrigger trigger, Element element)
        {
            activeEffects.RemoveAll(eff =>
                eff.sourcePassiveTrigger.HasValue &&
                eff.sourcePassiveTrigger.Value == trigger);

            Debug.Log($"[Passif] {data.cardName} — effets {trigger} retirés immédiatement");
        }



        // Liste séparée pour les effets passifs conditionnels (seuils de stacks)
        public List<ConditionalPassiveEffect> conditionalPassiveEffects
            = new List<ConditionalPassiveEffect>();

        [System.Serializable]
        public class ConditionalPassiveEffect
        {
            public EffectType type;
            public float value;
            public PassiveTrigger trigger;
            public int requiredThreshold;
            public Element triggerElement;
            public EffectTarget effectTarget;
            public int ownerPlayerID;
            public string sourceName = "";   // Nom de la carte dont provient ce passif
        }
    }
}