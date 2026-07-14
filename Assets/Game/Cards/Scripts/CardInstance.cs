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
        public int slotIndex;
        public int ownerPlayerID;
        public bool hasActedThisTurn;
        public int bonusActionsRemaining;
        public int skill1Cooldown;
        public int skill2Cooldown;
        [Tooltip("Cooldown de la 3e compétence (cartes Boss uniquement).")]
        public int skill3Cooldown;

        public List<ActiveEffect> activeEffects = new List<ActiveEffect>();

        // Incantations en cours (sorts à activation différée)
        public List<PendingIncantation> pendingIncantations = new List<PendingIncantation>();

        // Compteur pour les passifs stacksPerTrigger (ex. "pour chaque allié détruit")
        public int passiveStackCount = 0;

        private const int MAX_GIVE_ARMOR = 20;

        // Armure totale = armure de base + GiveArmor actifs (plafonnés à 20) − ReduceArmor actifs − réduction Corrosif adversaire
        public int TotalArmor
        {
            get
            {
                int total = data?.armorPoints ?? 0;
                int giveArmorSum = 0;
                int reduceArmorSum = 0;
                foreach (var eff in activeEffects)
                {
                    if (eff.type == EffectType.GiveArmor)
                        giveArmorSum += Mathf.RoundToInt(eff.value);
                    else if (eff.type == EffectType.ReduceArmor)
                        reduceArmorSum += Mathf.RoundToInt(eff.value);
                }
                total += Mathf.Min(giveArmorSum, MAX_GIVE_ARMOR);
                total -= reduceArmorSum;
                if (StackManager.Instance != null)
                    total -= StackManager.Instance.GetCorrosifArmorReduction(1 - ownerPlayerID);
                return Mathf.Max(0, total);
            }
        }

        // PV Max effectifs = data.maxHP − réductions actives (effets temporaires + stacks Corrosif adversaire)
        public int EffectiveMaxHP
        {
            get
            {
                float reduction = 0f;
                foreach (var eff in activeEffects)
                    if (eff.type == EffectType.MaxHPReduction)
                        reduction += eff.value;
                if (StackManager.Instance != null)
                    reduction += StackManager.Instance.GetCorrosifMaxHPReduction(1 - ownerPlayerID);
                return Mathf.Max(1, Mathf.RoundToInt(data.maxHP * (1f - reduction)));
            }
        }

        // Plafonne currentHP à EffectiveMaxHP si nécessaire (appelé quand la réduction augmente)
        public void ClampCurrentHP()
        {
            int max = EffectiveMaxHP;
            if (currentHP > max)
                currentHP = max;
        }

        // Le Boss (maxHP=3000, ~10-15x une carte normale) a un pool de PV dimensionné pour un
        // combat 1-contre-5 long, PAS pour servir de référence à un DoT en % PV max — Saignement/
        // Burn/Poison à 5% (valeur normale en PvP, ~10-15 DGT/tour sur une carte à 200-400 PV)
        // deviendraient ~150 DGT/tour sur le Boss, et empilent en instances indépendantes par
        // source (jusqu'à 5 cartes différentes) : un deck Feu dédié pouvait terrasser Voragoth en
        // ~20 tours via le seul Burn, sans quasi aucun dégât direct. DotReferenceMaxHP plafonne la
        // base de calcul des DoT au Boss à une valeur de carte normale forte (350, alignée sur
        // Card_048) — uniquement pour ces 3 effets, jamais pour currentHP/EffectiveMaxHP/TotalArmor
        // qui restent sur le vrai pool de 3000.
        private const int BOSS_DOT_REFERENCE_HP = 350;
        public int DotReferenceMaxHP =>
            (AI.GameModeContext.IsBossMatch && ownerPlayerID == 1) ? BOSS_DOT_REFERENCE_HP : data.maxHP;

        // Chance critique effective : base CardData + CritChanceBoost actifs + bonus Air stacks
        public float EffectiveCritChance
        {
            get
            {
                float total = data?.critChance ?? 0f;
                foreach (var eff in activeEffects)
                    if (eff.type == EffectType.CritChanceBoost)
                        total += eff.value;
                if (StackManager.Instance != null)
                {
                    total += StackManager.Instance.GetAirCritChanceBonus(ownerPlayerID);
                    total += StackManager.Instance.GetAirMajorCritChanceBonus(ownerPlayerID);
                    if (data?.element == Element.Feu)
                        total += StackManager.Instance.GetFireMajorCritChanceBonus(ownerPlayerID);
                }
                return total;
            }
        }

        // Bonus DGT critique effectif : 50% de base + CritDamageBoost actifs + bonus Air stacks + Feu majeur 5
        public float EffectiveCritDamageBonus
        {
            get
            {
                float total = 0.5f;
                foreach (var eff in activeEffects)
                    if (eff.type == EffectType.CritDamageBoost)
                        total += eff.value;
                if (StackManager.Instance != null)
                {
                    total += StackManager.Instance.GetAirMajorCritDamageBonus(ownerPlayerID);
                    if (data?.element == Element.Feu)
                        total += StackManager.Instance.GetFireMajorCritDamageBonus(ownerPlayerID);
                }
                return total;
            }
        }

        public bool IsAlive      => currentHP > 0;
        public bool IsInvisible  => activeEffects.Any(e => e.type == EffectType.Invisible);
        public bool IsReady => (!hasActedThisTurn || bonusActionsRemaining > 0)
                            && (skill1Cooldown == 0 || skill2Cooldown == 0
                                || (data.HasSkillThree && skill3Cooldown == 0))
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
            hasActedThisTurn = false;
            bonusActionsRemaining = 0;
            skill1Cooldown = 0;
            skill2Cooldown = 0;
            skill3Cooldown = 0;
            activeEffects.Clear();
            pendingIncantations.Clear();
            passiveStackCount = 0;
        }

        // ── Tour ──────────────────────────────────────────────────────

        public void OnTurnStart()
        {
            hasActedThisTurn = false;

            // Recharge gelée (EffectType.CooldownIncrease) : ne décompte pas ce tour
            bool cooldownLocked = activeEffects.Any(e => e.type == EffectType.CooldownIncrease);
            if (!cooldownLocked)
            {
                if (skill1Cooldown > 0) skill1Cooldown--;
                if (skill2Cooldown > 0) skill2Cooldown--;
                if (skill3Cooldown > 0) skill3Cooldown--;
            }
            ProcessActiveEffects();

            // Force la mise à jour visuelle immédiate
            GetComponent<CardVisualUpdater>()?.UpdateVisuals();
        }

        public void UseSkill(int skillIndex)
        {
            if (hasActedThisTurn && bonusActionsRemaining > 0)
                bonusActionsRemaining--;
            hasActedThisTurn = true;
            switch (skillIndex)
            {
                case 0: skill1Cooldown = data.skillOne?.cooldownTurns ?? 0; break;
                case 1: skill2Cooldown = data.skillTwo?.cooldownTurns ?? 0; break;
                case 2: skill3Cooldown = data.skillThree?.cooldownTurns ?? 0; break;
            }

            // Utiliser une compétence brise l'invisibilité jusqu'au prochain tour
            activeEffects.RemoveAll(e => e.type == EffectType.Invisible);
        }

        // ── Dégâts ────────────────────────────────────────────────────

        /// <summary>Mode Sandbox (IA solo) : carte immortelle, ignore tous les dégâts.</summary>
        [System.NonSerialized] public bool isImmortal = false;

        // Retourne les PV réellement perdus (après réduction par l'Armure).
        public int TakeDamage(int damage, bool ignoreArmor = false)
        {
            if (isImmortal) return 0;
            if (damage <= 0) return 0;

            int actual = ignoreArmor ? damage : Mathf.Max(0, damage - TotalArmor);

            if (actual > 0)
            {
                currentHP -= actual;
                currentHP = Mathf.Max(0, currentHP);
            }

            return actual;
        }

        // ── Soins ─────────────────────────────────────────────────────

        public int Heal(int amount, bool showPopup = true)
        {
            bool healBlocked = activeEffects.Any(e => e.type == EffectType.HealBlock);
            if (healBlocked || amount <= 0) return 0;

            float healBonusMultiplier = 1f;
            if (StackManager.Instance != null)
                healBonusMultiplier = 1f + StackManager.Instance.GetHealBonus(ownerPlayerID);

            float healReduction = 0f;
            foreach (var eff in activeEffects)
                if (eff.type == EffectType.HealReduction) healReduction += eff.value;
            healBonusMultiplier *= Mathf.Max(0f, 1f - healReduction);

            int boostedAmount = Mathf.RoundToInt(amount * healBonusMultiplier);
            int before = currentHP;
            currentHP = Mathf.Min(currentHP + boostedAmount, EffectiveMaxHP);
            int actual = currentHP - before;

            if (actual > 0 && showPopup)
            {
                GetComponent<CombatPopupHandler>()?.ShowHealPopup(actual);
                GetComponent<CardVisualUpdater>()?.SpawnHealVFX();
            }

            return actual;
        }


        // ── Effets actifs ─────────────────────────────────────────────

        private const float MAX_DAMAGE_REDUCTION      = 0.5f;   // % — DamageReduction
        private const float MAX_ATTACK_REDUCTION_FLAT = 50f;   // flat — AttackReduction
        private const float MAX_HEAL_REDUCTION        = 0.5f;   // % — HealReduction (Nécrotique)

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

            // ── HealReduction (Nécrotique) — cumulatif, plafonné à 50% ────
            if (newEffect.type == EffectType.HealReduction)
            {
                var existing = activeEffects.Find(e => e.type == EffectType.HealReduction);
                if (existing != null)
                {
                    existing.value          = Mathf.Min(existing.value + newEffect.value, MAX_HEAL_REDUCTION);
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
                newEffect.value = Mathf.Min(newEffect.value, MAX_HEAL_REDUCTION);
            }

            // ── AttackReduction — cumulatif flat, plafonné à MAX_ATTACK_REDUCTION_FLAT ─
            if (newEffect.type == EffectType.AttackReduction)
            {
                var existing = activeEffects.Find(e => e.type == EffectType.AttackReduction);
                if (existing != null)
                {
                    existing.value          = Mathf.Min(existing.value + newEffect.value, MAX_ATTACK_REDUCTION_FLAT);
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
                newEffect.value = Mathf.Min(newEffect.value, MAX_ATTACK_REDUCTION_FLAT);
            }

            // ── GiveArmor — même source+skill → rafraîchit, sinon empile ──────────
            if (newEffect.type == EffectType.GiveArmor)
            {
                var sameSrc = activeEffects.Find(e =>
                    e.type == EffectType.GiveArmor &&
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


            // ── ReduceArmor — même source+skill → rafraîchit, sinon empile ──────────
            if (newEffect.type == EffectType.ReduceArmor)
            {
                var sameSrc = activeEffects.Find(e =>
                    e.type == EffectType.ReduceArmor &&
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

            // ── MaxHPReduction — empile, puis plafonne currentHP si nécessaire ──────────
            if (newEffect.type == EffectType.MaxHPReduction)
            {
                activeEffects.Add(newEffect);
                ClampCurrentHP();
                return;
            }

            // ── Tous les autres effets — instances indépendantes empilables ──────────
            activeEffects.Add(newEffect);
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
            bool necroseBonusApplied = false;
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
                            int dot = Mathf.RoundToInt(DotReferenceMaxHP * cpe.value);
                            dot = Mathf.RoundToInt(dot * dmgReductMult);
                            int actualDot = TakeDamage(dot, ignoreArmor: true);
                            dotTotal += actualDot;
                            CombatLogManager.Instance?.AddEntry(
                                $"{data.cardName} -{actualDot} DGT (Passif Saignement)", playerID: ownerPlayerID);
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
                    int lightHeal = Mathf.RoundToInt(EffectiveMaxHP * lightHoT);
                    int before = currentHP;
                    currentHP = Mathf.Min(currentHP + lightHeal, EffectiveMaxHP);
                    int actualLightHeal = currentHP - before;
                    hotTotal += actualLightHeal;
                    if (actualLightHeal > 0)
                        CombatLogManager.Instance?.AddEntry(
                            $"{data.cardName} +{actualLightHeal} PV (Régén. Lumière)", playerID: ownerPlayerID);
                }
            }

            foreach (var effect in activeEffects.ToList())
            {
                switch (effect.type)
                {
                    case EffectType.Saignement:
                        {
                            int dot = Mathf.RoundToInt(DotReferenceMaxHP * effect.value);
                            dot = Mathf.RoundToInt(dot * dmgReductMult);
                            int actualDot = TakeDamage(dot, ignoreArmor: true);
                            dotTotal += actualDot;
                            CombatLogManager.Instance?.AddEntry(
                                $"{data.cardName} -{actualDot} DGT (Saignement)", playerID: ownerPlayerID);
                            break;
                        }

                    case EffectType.Burn:
                        {
                            int burnDmg = Mathf.RoundToInt(DotReferenceMaxHP * effect.value);
                            burnDmg = Mathf.RoundToInt(burnDmg * dmgReductMult);
                            int actualBurn = TakeDamage(burnDmg, ignoreArmor: true);
                            dotTotal += actualBurn;
                            CombatLogManager.Instance?.AddEntry(
                                $"{data.cardName} -{actualBurn} DGT (Brûlure)", playerID: ownerPlayerID);
                            break;
                        }

                    case EffectType.Poison:
                        {
                            int poisonDmg = Mathf.RoundToInt(DotReferenceMaxHP * effect.value);
                            poisonDmg = Mathf.RoundToInt(poisonDmg * dmgReductMult);
                            int actualPoison = TakeDamage(poisonDmg, ignoreArmor: true); // ignore armure
                            dotTotal += actualPoison;
                            CombatLogManager.Instance?.AddEntry(
                                $"{data.cardName} -{actualPoison} DGT (Poison)", playerID: ownerPlayerID);
                            break;
                        }

                    case EffectType.Necrose:
                        {
                            // Nécrotique : DGT plat/tour (pas % PV max) — bonus mineur (+1 DGT
                            // par carte Nécrotique en jeu, tous joueurs) ajouté une seule fois par tick,
                            // pas par instance empilée
                            float mineurBonus = necroseBonusApplied
                                ? 0f
                                : (StackManager.Instance?.GetNecrotiqueBoardCount() ?? 0);
                            necroseBonusApplied = true;
                            int necroDmg = Mathf.RoundToInt((effect.value + mineurBonus) * dmgReductMult);
                            int actualNecro = TakeDamage(necroDmg, ignoreArmor: true);
                            dotTotal += actualNecro;
                            CombatLogManager.Instance?.AddEntry(
                                $"{data.cardName} -{actualNecro} DGT (Nécrose)", playerID: ownerPlayerID);
                            break;
                        }

                    case EffectType.HealOverTime:
                        {
                            if (!healBlocked)
                            {
                                int hot = Mathf.RoundToInt(EffectiveMaxHP * effect.value);
                                int before = currentHP;
                                currentHP = Mathf.Min(currentHP + hot, EffectiveMaxHP);
                                int actualHot = currentHP - before;
                                hotTotal += actualHot;
                                if (actualHot > 0)
                                    CombatLogManager.Instance?.AddEntry(
                                        $"{data.cardName} +{actualHot} PV (Régénération)", playerID: ownerPlayerID);
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
                if (!NecroticReviveHandler.TryRevive(this))
                {
                    NecroticExplosionHandler.TriggerExplosionIfApplicable(this);
                    BoardManager.Instance?.DestroyCard(this);
                    CombatLogManager.Instance?.AddEntry(
                        $"{data.cardName} est détruit !", isDeathEntry: true, playerID: ownerPlayerID);
                }
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
                GetComponent<CardVisualUpdater>()?.SpawnHealVFX();
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

        // ── Incantations ──────────────────────────────────────────────

        public void AddIncantation(CardSkill skill, int skillIndex, CardInstance target, int delay)
        {
            pendingIncantations.Add(new PendingIncantation
            {
                skill           = skill,
                skillIndex      = skillIndex,
                targetPlayerID  = target != null ? target.ownerPlayerID  : -1,
                targetSlotIndex = target != null ? target.slotIndex      : -1,
                turnsRemaining  = delay,
            });
            GetComponent<CardVisualUpdater>()?.UpdateVisuals();
        }
    }

    [System.Serializable]
    public class PendingIncantation
    {
        public CardSkill skill;
        public int skillIndex;       // 0 ou 1 (pour affichage)
        public int targetPlayerID;   // -1 si pas de cible fixe (AoE / Self)
        public int targetSlotIndex;  // -1 si pas de cible fixe
        public int turnsRemaining;
    }
}