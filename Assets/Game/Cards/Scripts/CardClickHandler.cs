using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections.Generic;
using System.Linq;

namespace Astraleum
{
    public class CardClickHandler : MonoBehaviour, IPointerClickHandler
    {
        private static CardZoomHandler currentZoomedEnemy;

        public void OnPointerClick(PointerEventData eventData)
        {
            var card = GetComponent<CardInstance>();
            var uiManager = CombatUIManager.Instance;

            if (card == null || uiManager == null) return;

            // Bloquer tous les clics pendant une animation d'attaque
            if (CombatManager.Instance != null && CombatManager.Instance.IsAnimating) return;

            // En réseau, la perspective est toujours celle du joueur LOCAL
            int perspectivePlayer = NetworkBridge.IsActive
                ? NetworkBridge.LocalPlayerID
                : (TurnManager.Instance?.currentPlayerID ?? 0);
            bool isEnemy = card.ownerPlayerID != perspectivePlayer;
            if (NetworkBridge.IsActive)
                Debug.Log($"[Click] {card.data?.cardName} ownerP:{card.ownerPlayerID} perspP:{perspectivePlayer} isEnemy:{isEnemy} localID:{NetworkBridge.LocalPlayerID} btn:{eventData.button}");

            // ── Clic droit → annulation totale ───────────────────────
            if (eventData.button == PointerEventData.InputButton.Right)
            {
                uiManager.CancelSelection();
                DezoomEnemy();
                return;
            }

            if (eventData.button != PointerEventData.InputButton.Left) return;

            // Cas 1 : Compétence sélectionnée + clic sur ennemi → Attaque
            if (uiManager.HasSkillSelected && isEnemy)
            {
                var skill = uiManager.GetSelectedSkill();
                // Vérifie que le skill est bien offensif avant d'attaquer
                if (skill != null &&
                    (skill.skillType == SkillType.Attack ||
                     skill.skillType == SkillType.Debuff ||
                     skill.skillType == SkillType.Mixed))
                {
                    // Cible invisible + compétence à cible directe → non ciblable
                    if (card.IsInvisible && skill.targetType == SkillTargetType.SingleEnemy)
                        return;

                    DezoomEnemy();
                    uiManager.ExecuteSelectedSkill(card);
                    return;
                }
            }

            // Cas 2 : Compétence sélectionnée + clic sur allié → Buff/Heal
            if (uiManager.HasSkillSelected && !isEnemy)
            {
                var skill = uiManager.GetSelectedSkill();
                if (skill != null &&
                    (skill.skillType == SkillType.Heal ||
                     skill.skillType == SkillType.Buff ||
                     skill.targetType == SkillTargetType.Self ||
                     skill.targetType == SkillTargetType.SingleAlly ||
                     skill.targetType == SkillTargetType.AllAllies))
                {
                    DezoomEnemy();
                    uiManager.ExecuteSelectedSkill(card);
                    return;
                }
            }

            // Cas 3 : Pas de compétence + clic sur ennemi → SkillPanel lecture seule
            if (!uiManager.HasSkillSelected && isEnemy)
            {
                uiManager.CancelSelection();

                var zoom = card.GetComponent<CardZoomHandler>();
                if (zoom != null)
                {
                    if (currentZoomedEnemy == zoom)
                    {
                        zoom.ZoomOut();
                        currentZoomedEnemy = null;
                        uiManager.CloseSkillPanel();
                        PassiveTooltipManager.Instance?.Hide();
                    }
                    else
                    {
                        DezoomEnemy();
                        zoom.ZoomIn();
                        currentZoomedEnemy = zoom;
                        uiManager.OpenSkillPanel(card, readOnly: true);
                        PassiveTooltipManager.Instance?.Show(card, card.GetComponent<RectTransform>());
                    }
                }
                return;
            }

            // Cas 4 : Pas de compétence + clic sur allié → Ouvre le SkillPanel
            if (!uiManager.HasSkillSelected && !isEnemy)
            {
                if (TurnManager.Instance.actionsRemaining <= 0) return;
                if (card.hasActedThisTurn && card.bonusActionsRemaining <= 0) return;
                if (card.activeEffects.Any(e => e.type == EffectType.Stun)) return; // carte étourdie

                // En réseau : un joueur ne peut agir que sur SES cartes ET pendant SON tour
                if (NetworkBridge.IsActive && card.ownerPlayerID != NetworkBridge.LocalPlayerID)
                    return;
                if (NetworkBridge.IsActive && TurnManager.Instance?.currentPlayerID != NetworkBridge.LocalPlayerID)
                    return;

                DezoomEnemy();
                uiManager.OpenSkillPanel(card);
                PassiveTooltipManager.Instance?.Show(card, card.GetComponent<RectTransform>());
                return;
            }
        }

        private static void DezoomEnemy()
        {
            if (currentZoomedEnemy != null)
            {
                currentZoomedEnemy.ZoomOut();
                currentZoomedEnemy = null;
            }
            PassiveTooltipManager.Instance?.Hide(); // ← Hide existe maintenant
        }
    }
}