using System.Collections.Generic;
using UnityEngine;

namespace Astraleum
{
    public class PassiveManager : MonoBehaviour
    {
        public static PassiveManager Instance;
        private void Awake() => Instance = this;

        // ── Appels depuis le jeu ──────────────────────────────────────

        // Appelé quand n'importe quelle carte est détruite
        public void OnCardDestroyed(CardInstance destroyedCard)
        {
            // Si la carte détruite avait un passif stacksPerTrigger → retire ses effets des survivants
            if (destroyedCard.data?.passive != null &&
                destroyedCard.data.passive.stacksPerTrigger &&
                destroyedCard.passiveStackCount > 0)
            {
                string srcName = destroyedCard.data.cardName;
                foreach (var card in GetAllAliveCards())
                {
                    int removed = card.activeEffects.RemoveAll(e => e.sourceName == srcName);
                    if (removed > 0)
                        Debug.Log($"[Passif] {card.data.cardName} — {removed} effet(s) retiré(s) " +
                                  $"(source '{srcName}' détruite)");
                }
            }

            var allCards = GetAllAliveCards();
            foreach (var card in allCards)
            {
                TriggerPassive(card, PassiveTrigger.OnCardDestroyed, destroyedCard);

                // Si c'était un allié
                if (destroyedCard.ownerPlayerID == card.ownerPlayerID)
                    TriggerPassive(card, PassiveTrigger.OnAllyDestroyed, destroyedCard);
            }

            StackManager.Instance?.RefreshPermanentStacks();
        }

        // Appelé quand une carte en détruit une autre
        public void OnCardDestroyedByCard(CardInstance killer, CardInstance victim)
        {
            TriggerPassive(killer, PassiveTrigger.WhenThisCardDestroysCard, victim);
        }

        // Appelé quand les stacks d'un élément changent
        public void OnStacksChanged(int playerID, Element element, int newCount)
        {

            var allies = BoardManager.Instance.GetAliveCards(playerID);
            foreach (var card in allies)
            {
                if (newCount == 3)
                    TriggerPassive(card, PassiveTrigger.OnStackThreshold3, null, element);
                else if (newCount == 5)
                    TriggerPassive(card, PassiveTrigger.OnStackThreshold5, null, element);
            }
        }

        // Appelé au début de chaque tour
        public void OnTurnStart(int playerID)
        {
            var allies = BoardManager.Instance.GetAliveCards(playerID);
            foreach (var card in allies)
                TriggerPassive(card, PassiveTrigger.OnTurnStart, null);
        }

        // ── Résolution du passif ──────────────────────────────────────

        private void TriggerPassive(CardInstance card, PassiveTrigger trigger,
                             CardInstance source, Element element = Element.Feu)
        {
            if (card?.data?.passive == null) return;
            if (string.IsNullOrEmpty(card.data.passive.passiveDescription)) return;

            if (trigger == PassiveTrigger.OnStackThreshold3 ||
                trigger == PassiveTrigger.OnStackThreshold5)
            {
                TriggerElement triggerElem = card.data.passive.triggerElement;

                bool elementMatch;
                if (triggerElem == TriggerElement.Any)
                {
                    string elemName = element.ToString();
                    elementMatch = elemName != "Astral";
                }
                else
                {
                    elementMatch = triggerElem.ToString() == element.ToString();
                }

                if (!elementMatch) return;
            }

            ResolvePassive(card, trigger, source, element);
        }

        // ── Resolution des Passifs 2 ────────────────────────────────────────────────

        private void ResolvePassive(CardInstance card, PassiveTrigger trigger,
                             CardInstance source, Element element)
        {
            if (card.data.passive == null) return;

            var passive = card.data.passive;

            if (passive.trigger != trigger) return;

            // Passif cumulatif (stacksPerTrigger) → délégué à ResolveStackingPassive
            if (passive.stacksPerTrigger)
            {
                ResolveStackingPassive(card, trigger, source);
                return;
            }

            if (trigger == PassiveTrigger.OnStackThreshold3 ||
                trigger == PassiveTrigger.OnStackThreshold5)
            {
                if (passive.triggerElement != TriggerElement.Any &&
                    passive.triggerElement.ToString() != element.ToString()) return;
            }

            // Filtre par élément pour OnAllyDestroyed / OnCardDestroyed
            if (trigger == PassiveTrigger.OnAllyDestroyed || trigger == PassiveTrigger.OnCardDestroyed)
            {
                if (source != null && passive.triggerElement != TriggerElement.Any)
                {
                    if (source.data.element.ToString() != passive.triggerElement.ToString()) return;
                }
            }

            if (passive.effects == null || passive.effects.Count == 0) return;

            int threshold = trigger == PassiveTrigger.OnStackThreshold5 ? 5 : 3;

            // Pour OnAllyDestroyed/OnCardDestroyed, la carte détruite n'est plus alive :
            // on la remplace par la carte passive elle-même comme cible par défaut.
            CardInstance resolvedSource = (trigger == PassiveTrigger.OnAllyDestroyed ||
                                           trigger == PassiveTrigger.OnCardDestroyed)
                ? card
                : (source != null ? source : card);

            foreach (var effect in passive.effects)
            {
                try
                {
                    if (effect.effectTarget == EffectTarget.AllEnemies)
                    {
                        if (BoardManager.Instance == null) return;

                        int enemyID = card.ownerPlayerID == 0 ? 1 : 0;
                        var enemies = BoardManager.Instance.GetAliveCards(enemyID);

                        if (enemies == null) return;

                        foreach (var enemy in enemies)
                        {
                            bool enemyHasEffect = enemy.conditionalPassiveEffects.Exists(e =>
                                e.type == effect.type &&
                                e.trigger == trigger &&
                                e.ownerPlayerID == card.ownerPlayerID);

                            if (!enemyHasEffect)
                            {
                                enemy.conditionalPassiveEffects.Add(
                                    new CardInstance.ConditionalPassiveEffect
                                    {
                                        type = effect.type,
                                        value = effect.value,
                                        trigger = trigger,
                                        requiredThreshold = threshold,
                                        triggerElement = element,
                                        effectTarget = effect.effectTarget,
                                        ownerPlayerID = card.ownerPlayerID,
                                        sourceName = card.data.cardName
                                    });
                            }
                        }
                    }
                    else if (effect.effectTarget == EffectTarget.RandomAllies)
                    {
                        if (BoardManager.Instance == null) return;
                        var allies = BoardManager.Instance.GetAliveCards(card.ownerPlayerID);
                        if (allies != null && allies.Count > 0)
                            SkillExecutor.ApplyEffect(effect, card,
                                allies[UnityEngine.Random.Range(0, allies.Count)]);
                    }
                    else if (effect.effectTarget == EffectTarget.RandomEnnemies)
                    {
                        if (BoardManager.Instance == null) return;
                        int enemyID = card.ownerPlayerID == 0 ? 1 : 0;
                        var enemies = BoardManager.Instance.GetAliveCards(enemyID);
                        if (enemies != null && enemies.Count > 0)
                            SkillExecutor.ApplyEffect(effect, card,
                                enemies[UnityEngine.Random.Range(0, enemies.Count)]);
                    }
                    else
                    {
                        SkillExecutor.ApplyEffect(effect, card, resolvedSource);
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"[PassiveManager] Exception dans ResolvePassive : {e.Message}");
                }
            }

            CombatLogManager.Instance?.AddEntry(
                $"{card.data.cardName} ⚡ {passive.passiveName}", playerID: card.ownerPlayerID);
        }



        // ── Passif Cumulatif (stacksPerTrigger) ──────────────────────────

        private void ResolveStackingPassive(CardInstance card, PassiveTrigger trigger,
                                             CardInstance source)
        {
            var passive = card.data.passive;

            // Vérifie l'élément de l'allié/carte détruite
            if (source != null)
            {
                bool elementMatch;
                if (passive.triggerElement == TriggerElement.Any)
                    elementMatch = source.data.element.ToString() != "Astral";
                else
                    elementMatch = source.data.element.ToString() == passive.triggerElement.ToString();

                if (!elementMatch) return;
            }

            // Plafond de stacks atteint ?
            if (card.passiveStackCount >= passive.maxTriggerStacks) return;

            card.passiveStackCount++;

            foreach (var effect in passive.effects)
            {
                // Retire l'effet précédent du même type appliqué par ce passif
                card.activeEffects.RemoveAll(e =>
                    e.type == effect.type &&
                    e.sourceName == card.data.cardName);

                // Applique la valeur cumulée (value × nombre de déclenchements)
                var newEffect = new ActiveEffect
                {
                    type             = effect.type,
                    value            = effect.value * card.passiveStackCount,
                    remainingTurns   = effect.durationTurns,
                    sourceName       = card.data.cardName,
                    sourcePassiveTrigger = trigger,
                    sourceElement    = card.data.element
                };
                card.activeEffects.Add(newEffect);
            }

            CombatLogManager.Instance?.AddEntry(
                $"{card.data.cardName} ⚡ {passive.passiveName} ({card.passiveStackCount}/{passive.maxTriggerStacks})", playerID: card.ownerPlayerID);
        }

        // ── Perte de Stack ────────────────────────────────────────────────
        public void OnStackThresholdLost(int playerID, Element element, int threshold)
        {
            if (BoardManager.Instance == null) return;

            PassiveTrigger lostTrigger = threshold == 5
                ? PassiveTrigger.OnStackThreshold5
                : PassiveTrigger.OnStackThreshold3;

            // Quand P2 perd son seuil → ses CPE (ownerPlayerID=P2) 
            // doivent être retirés des cartes de P1 (les ennemis de P2)
            int enemyID = playerID == 0 ? 1 : 0;
            var enemies = BoardManager.Instance.GetAliveCards(enemyID);

            foreach (var enemy in enemies)
            {
                int removed = enemy.conditionalPassiveEffects.RemoveAll(cpe =>
                    cpe.trigger == lostTrigger &&
                    cpe.ownerPlayerID == playerID); // ← retire les CPE appartenant au joueur qui a perdu le seuil

                if (removed > 0)
                    Debug.Log($"[Passif] {enemy.data.cardName} — {removed} CPE retiré(s) " +
                              $"(P{playerID} a perdu seuil {threshold})");
            }
        }
        // ── Utilitaire ────────────────────────────────────────────────

        private List<CardInstance> GetAllAliveCards()
        {
            var all = new List<CardInstance>();
            if (BoardManager.Instance == null) return all;
            all.AddRange(BoardManager.Instance.GetAliveCards(0));
            all.AddRange(BoardManager.Instance.GetAliveCards(1));
            return all;
        }
    }
}