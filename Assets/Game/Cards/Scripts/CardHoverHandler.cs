using UnityEngine;
using UnityEngine.EventSystems;

namespace Astraleum
{
    public class CardHoverHandler : MonoBehaviour,
                                    IPointerEnterHandler,
                                    IPointerExitHandler
    {
        public void OnPointerEnter(PointerEventData eventData)
        {
            var card = GetComponent<CardInstance>();
            var uiManager = CombatUIManager.Instance;

            if (card == null || uiManager == null) return;

            // Pas de skill sélectionné → afficher les buffs actifs
            if (!uiManager.HasSkillSelected)
            {
                BuffTooltipManager.Instance?.Show(card, card.GetComponent<RectTransform>());
                return;
            }

            int perspectivePlayer = NetworkBridge.IsActive
                ? NetworkBridge.LocalPlayerID
                : (TurnManager.Instance?.currentPlayerID ?? 0);
            bool isEnemy = card.ownerPlayerID != perspectivePlayer;

            var skill = uiManager.GetSelectedSkill();
            if (skill == null) return;

            if (isEnemy)
            {
                if (skill.skillType == SkillType.Attack ||
                    skill.skillType == SkillType.Debuff ||
                    skill.skillType == SkillType.Mixed)
                {
                    // Pas de preview sur cible invisible non ciblable directement
                    if (card.IsInvisible && skill.targetType == SkillTargetType.SingleEnemy)
                        return;

                    uiManager.ShowDamagePreview(card, card.transform.position);

                    if (skill.targetType == SkillTargetType.AdjacentEnemies)
                        uiManager.ShowAdjacentDamagePreview(card);

                    SendArrowTarget(uiManager, card);
                }
                return;
            }

            // ── Carte alliée ──────────────────────────────────────────
            if (skill.skillType == SkillType.Heal)
            {
                uiManager.ShowHealPreview(card, card.transform.position);
                SendArrowTarget(uiManager, card);
                return;
            }

            if (skill.skillType == SkillType.Buff)
            {
                uiManager.ShowBuffPreview(card, skill);
                SendArrowTarget(uiManager, card);
                return;
            }

            if (skill.targetType == SkillTargetType.Self ||
                skill.targetType == SkillTargetType.SingleAlly ||
                skill.targetType == SkillTargetType.AllAllies)
            {
                uiManager.ShowBuffPreview(card, skill);
                SendArrowTarget(uiManager, card);
                return;
            }
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            var uiManager = CombatUIManager.Instance;
            if (uiManager == null) return;
            uiManager.HideDamagePreview();
            BuffTooltipManager.Instance?.Hide();

            if (NetworkBridge.IsActive && uiManager.HasSkillSelected)
                NetworkBridge.OnArrowTargetHideRequested?.Invoke();
        }

        private static void SendArrowTarget(CombatUIManager uiManager, CardInstance target)
        {
            if (!NetworkBridge.IsActive) return;
            var attacker = uiManager.CurrentPanelCard;
            if (attacker == null) return;
            NetworkBridge.OnArrowTargetRequested?.Invoke(
                attacker.ownerPlayerID, attacker.slotIndex,
                target.ownerPlayerID,   target.slotIndex);
        }
    }
}
