using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;
using System.Collections;
using DG.Tweening;

namespace Astraleum
{
    public class CombatUIManager : MonoBehaviour
    {
        public static CombatUIManager Instance;

        [Header("Panneaux")]
        public GameObject cardSkillPanel;
        private CardZoomHandler currentZoomedCard;
        public bool IsSkillPanelOpen => (cardSkillPanel != null && cardSkillPanel.activeSelf)
                                      || (cardSkillPanelBoss != null && cardSkillPanelBoss.activeSelf);

        [Header("CardSkillPanel — Références")]
        public Image skillPanelCardPreview;
        public TMP_Text skillPanelCardName;
        public Button skill1Button;
        public Button skill2Button;
        public TMP_Text skill1Name;
        public TMP_Text skill1DMG;
        public TMP_Text skill1Desc;
        public TMP_Text skill2Name;
        public TMP_Text skill2DMG;
        public TMP_Text skill2Desc;
        public GameObject skill1CD;
        public GameObject skill2CD;
        public TMP_Text skill1CDText;
        public TMP_Text skill2CDText;

        [Header("CardSkillPanel — Icônes type")]
        public Image skill1TypeIcon;
        public Image skill2TypeIcon;

        [Header("CardSkillPanel — Overlays assombrissement")]
        public Image skill1DarkenOverlay;
        public Image skill2DarkenOverlay;

        [Header("CardSkillPanelBoss — Références (3 compétences, lecture seule)")]
        public GameObject cardSkillPanelBoss;
        public TMP_Text bossPanelCardName;
        public Button bossSkill1Button;
        public Button bossSkill2Button;
        public Button bossSkill3Button;
        public TMP_Text bossSkill1Name;
        public TMP_Text bossSkill1DMG;
        public TMP_Text bossSkill1Desc;
        public TMP_Text bossSkill2Name;
        public TMP_Text bossSkill2DMG;
        public TMP_Text bossSkill2Desc;
        public TMP_Text bossSkill3Name;
        public TMP_Text bossSkill3DMG;
        public TMP_Text bossSkill3Desc;
        public GameObject bossSkill1CD;
        public GameObject bossSkill2CD;
        public GameObject bossSkill3CD;
        public TMP_Text bossSkill1CDText;
        public TMP_Text bossSkill2CDText;
        public TMP_Text bossSkill3CDText;
        public Image bossSkill1DarkenOverlay;
        public Image bossSkill2DarkenOverlay;
        public Image bossSkill3DarkenOverlay;

        [Header("DamagePreviewBar — Références")]
        public GameObject damagePreviewBar;      // ← la barre entière
        public TMP_Text bar_SkillName;
        public TMP_Text bar_BaseValue;
        public TMP_Text bar_FinalValue;
        public GameObject bar_BlockBuff;
        public TMP_Text bar_BuffLabel;
        public TMP_Text bar_BuffValue;
        public GameObject bar_BlockArmor;
        public TMP_Text bar_ArmorLabel;
        public TMP_Text bar_ArmorValue;
        public GameObject bar_BlockHeal;
        public TMP_Text bar_HealValue;

        [Header("DamagePreviewBar — Séparateurs")]
        public GameObject vSep2;
        public GameObject vSep3;
        public GameObject vSep4;
        public GameObject vSep5;

        [Header("ActionBar")]
        public Image dot1;
        public Image dot2;
        public Sprite spriteActionUsed; // big_roundframe — assigné dans l'Inspector
        public Image btnFinTourImage;
        public Color btnFinTourNormal = new Color(0.3f, 0.25f, 0.6f, 1f);
        public Color btnFinTourHighlight = new Color(0.48f, 0.36f, 0.96f, 1f);

        [Header("Animation Dots")]
        public Color dotUsedColor = new Color(0.22f, 0.22f, 0.28f, 1f);

        private Sprite _dot1OriginalSprite;
        private Sprite _dot2OriginalSprite;
        private Color  _dot1OriginalColor;
        private Color  _dot2OriginalColor;
        private bool   _dot1WasUsed;
        private bool   _dot2WasUsed;

        [Header("TurnIndicator")]
        public TMP_Text turnText;

        private CardInstance selectedCard;
        private int selectedSkillIndex = -1;
        private CardInstance currentReadOnlyCard;

        public bool HasSkillSelected => selectedSkillIndex >= 0 && selectedCard != null;
        public CardInstance CurrentPanelCard => selectedCard;

        private void Awake()
        {
            Instance = this;
            if (dot1 != null) { _dot1OriginalSprite = dot1.sprite; _dot1OriginalColor = dot1.color; }
            if (dot2 != null) { _dot2OriginalSprite = dot2.sprite; _dot2OriginalColor = dot2.color; }

            // La barre ne doit pas intercepter les événements pointeur (sinon elle vole les OnPointerExit des cartes)
            if (damagePreviewBar != null)
            {
                var cg = damagePreviewBar.GetComponent<CanvasGroup>();
                if (cg == null) cg = damagePreviewBar.AddComponent<CanvasGroup>();
                cg.blocksRaycasts = false;
                cg.interactable   = false;
            }

            // CardSkillPanel rendu au-dessus des VFX (sortingOrder 150 > VFX Stun 50)
            if (cardSkillPanel != null)
            {
                var rootCanvas = GetComponentInParent<Canvas>()?.rootCanvas ?? FindFirstObjectByType<Canvas>();
                var cv = cardSkillPanel.GetComponent<Canvas>();
                if (cv == null) cv = cardSkillPanel.AddComponent<Canvas>();
                cv.overrideSorting = true;
                cv.sortingLayerName = rootCanvas != null ? rootCanvas.sortingLayerName : "Default";
                cv.sortingOrder = 150;
                if (cardSkillPanel.GetComponent<GraphicRaycaster>() == null)
                    cardSkillPanel.AddComponent<GraphicRaycaster>();
            }

            // Même traitement pour le panel Boss (3 compétences)
            if (cardSkillPanelBoss != null)
            {
                var rootCanvas = GetComponentInParent<Canvas>()?.rootCanvas ?? FindFirstObjectByType<Canvas>();
                var cv = cardSkillPanelBoss.GetComponent<Canvas>();
                if (cv == null) cv = cardSkillPanelBoss.AddComponent<Canvas>();
                cv.overrideSorting = true;
                cv.sortingLayerName = rootCanvas != null ? rootCanvas.sortingLayerName : "Default";
                cv.sortingOrder = 150;
                if (cardSkillPanelBoss.GetComponent<GraphicRaycaster>() == null)
                    cardSkillPanelBoss.AddComponent<GraphicRaycaster>();
            }
        }

        // ─── CardSkillPanel ───────────────────────────────────────────

        public void OpenSkillPanel(CardInstance card, bool readOnly = false)
        {
            // Carte à 3 compétences (Boss uniquement) → panel dédié CardSkillPanelBoss,
            // toujours en lecture seule (le joueur ne contrôle jamais le Boss).
            if (card.data.HasSkillThree)
            {
                OpenBossSkillPanel(card);
                return;
            }

            // En mode normal, vérifie qu'il reste des actions
            if (!readOnly && TurnManager.Instance.actionsRemaining <= 0) return;

            // Déjà ouvert pour la même carte → ne pas repositionner
            if (cardSkillPanel != null && cardSkillPanel.activeSelf)
            {
                if (!readOnly && selectedCard == card) return;
                if (readOnly && currentReadOnlyCard == card) return;
            }

            // Réinitialiser les overlays à chaque ouverture
            if (skill1DarkenOverlay != null) skill1DarkenOverlay.gameObject.SetActive(false);
            if (skill2DarkenOverlay != null) skill2DarkenOverlay.gameObject.SetActive(false);

            // Dézoome la carte précédente si elle existe
            if (currentZoomedCard != null)
            {
                currentZoomedCard.ZoomOut();
                currentZoomedCard = null;
            }

            // En lecture seule (carte adverse) : on n'assigne pas selectedCard
            if (!readOnly)
            {
                selectedCard = card;
                selectedSkillIndex = -1;
                currentReadOnlyCard = null;
                if (NetworkBridge.IsActive)
                    NetworkBridge.OnCardSelectedRequested?.Invoke(card.ownerPlayerID, card.slotIndex);
            }
            else
            {
                currentReadOnlyCard = card;
            }

            // Zoom sur la nouvelle carte sélectionnée
            var zoomHandler = card.GetComponent<CardZoomHandler>();
            if (zoomHandler != null)
            {
                currentZoomedCard = zoomHandler;
                zoomHandler.ZoomIn();
            }

            // Nom de la carte
            if (skillPanelCardName != null)
                skillPanelCardName.text = card.data.cardName;

            // Artwork
            if (skillPanelCardPreview != null && card.data.artwork != null)
                skillPanelCardPreview.sprite = card.data.artwork;

            // skillbutton1
            UpdateSkillButton(skill1Button, skill1Name, skill1DMG, skill1Desc,
                              skill1CD, skill1CDText, skill1TypeIcon,
                              card.data.skillOne, card.skill1Cooldown, card, skill1DarkenOverlay);

            // skillbutton2
            UpdateSkillButton(skill2Button, skill2Name, skill2DMG, skill2Desc,
                              skill2CD, skill2CDText, skill2TypeIcon,
                              card.data.skillTwo, card.skill2Cooldown, card, skill2DarkenOverlay);

            // Lecture seule : force les boutons non-interactifs, pas de changement visuel
            if (readOnly)
            {
                if (skill1Button != null) skill1Button.interactable = false;
                if (skill2Button != null) skill2Button.interactable = false;
            }

            // Active d'abord pour que ContentSizeFitter calcule la taille réelle,
            // puis positionne (ForceRebuildLayoutImmediate garantit rect à jour)
            cardSkillPanel.SetActive(true);
            var panelRTTemp = cardSkillPanel.GetComponent<RectTransform>();
            UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(panelRTTemp);
            PositionPanelNextToCard(cardSkillPanel, card.GetComponent<RectTransform>());
        }

        // ─── CardSkillPanelBoss (3 compétences, lecture seule) ─────────

        public void OpenBossSkillPanel(CardInstance card)
        {
            if (cardSkillPanelBoss == null) return;

            // Déjà ouvert pour la même carte → ne pas repositionner
            if (cardSkillPanelBoss.activeSelf && currentReadOnlyCard == card) return;

            if (bossSkill1DarkenOverlay != null) bossSkill1DarkenOverlay.gameObject.SetActive(false);
            if (bossSkill2DarkenOverlay != null) bossSkill2DarkenOverlay.gameObject.SetActive(false);
            if (bossSkill3DarkenOverlay != null) bossSkill3DarkenOverlay.gameObject.SetActive(false);

            if (currentZoomedCard != null)
            {
                currentZoomedCard.ZoomOut();
                currentZoomedCard = null;
            }

            currentReadOnlyCard = card;

            var zoomHandler = card.GetComponent<CardZoomHandler>();
            if (zoomHandler != null)
            {
                currentZoomedCard = zoomHandler;
                zoomHandler.ZoomIn();
            }

            if (bossPanelCardName != null)
                bossPanelCardName.text = card.data.cardName;

            UpdateSkillButton(bossSkill1Button, bossSkill1Name, bossSkill1DMG, bossSkill1Desc,
                              bossSkill1CD, bossSkill1CDText, null,
                              card.data.skillOne, card.skill1Cooldown, card, bossSkill1DarkenOverlay);
            UpdateSkillButton(bossSkill2Button, bossSkill2Name, bossSkill2DMG, bossSkill2Desc,
                              bossSkill2CD, bossSkill2CDText, null,
                              card.data.skillTwo, card.skill2Cooldown, card, bossSkill2DarkenOverlay);
            UpdateSkillButton(bossSkill3Button, bossSkill3Name, bossSkill3DMG, bossSkill3Desc,
                              bossSkill3CD, bossSkill3CDText, null,
                              card.data.skillThree, card.skill3Cooldown, card, bossSkill3DarkenOverlay);

            // Toujours en lecture seule — le joueur ne contrôle jamais le Boss
            if (bossSkill1Button != null) bossSkill1Button.interactable = false;
            if (bossSkill2Button != null) bossSkill2Button.interactable = false;
            if (bossSkill3Button != null) bossSkill3Button.interactable = false;

            cardSkillPanelBoss.SetActive(true);
            var panelRTTemp = cardSkillPanelBoss.GetComponent<RectTransform>();
            UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(panelRTTemp);
            PositionPanelNextToCard(cardSkillPanelBoss, card.GetComponent<RectTransform>());
        }

        /// <summary>
        /// Positionne le panel de compétences (normal ou Boss) à droite de la carte cible.
        /// Si le panel sortirait du canvas, il est recadré automatiquement.
        /// </summary>
        private void PositionPanelNextToCard(GameObject panel, RectTransform cardRT)
        {
            if (panel == null || cardRT == null) return;

            var panelRT = panel.GetComponent<RectTransform>();
            if (panelRT == null) return;

            Canvas cv = panel.GetComponentInParent<Canvas>()?.rootCanvas;
            if (cv == null) { Debug.LogError("[SkillPanel] Canvas introuvable"); return; }

            RectTransform cvRT = cv.GetComponent<RectTransform>();
            Camera cam = cv.renderMode == RenderMode.ScreenSpaceOverlay ? null : cv.worldCamera;

            panelRT.anchorMin = new Vector2(0.5f, 0.5f);
            panelRT.anchorMax = new Vector2(0.5f, 0.5f);
            panelRT.pivot     = new Vector2(0.5f, 0.5f);

            Vector3[] corners = new Vector3[4];
            cardRT.GetWorldCorners(corners);

            // En ScreenSpaceCamera, GetWorldCorners retourne du world-space 3D
            // → il faut projeter en pixels écran avant de passer à ScreenPointToLocalPointInRectangle
            Vector2 c2, c3;
            if (cv.renderMode == RenderMode.ScreenSpaceCamera && cam != null)
            {
                c2 = cam.WorldToScreenPoint(corners[2]);
                c3 = cam.WorldToScreenPoint(corners[3]);
            }
            else
            {
                c2 = corners[2];
                c3 = corners[3];
            }
            Vector2 cardRightCenter = (c2 + c3) * 0.5f;

            bool ok = RectTransformUtility.ScreenPointToLocalPointInRectangle(cvRT, cardRightCenter, cam, out Vector2 localPos);

            float panelW    = panelRT.rect.width;
            float panelH    = panelRT.rect.height;
            float panelHalfW = panelW * 0.5f;
            float panelHalfH = panelH * 0.5f;
            float gap = 14f;

            Vector2 cvHalf  = cvRT.rect.size * 0.5f;
            Vector2 targetPos = new Vector2(localPos.x + panelHalfW + gap, localPos.y);
            targetPos.x = Mathf.Clamp(targetPos.x, -cvHalf.x + panelHalfW, cvHalf.x - panelHalfW);
            targetPos.y = Mathf.Clamp(targetPos.y, -cvHalf.y + panelHalfH, cvHalf.y - panelHalfH);

            panelRT.anchoredPosition = targetPos;
        }

        private Color GetSkillColor(SkillType type)
        {
            return type switch
            {
                SkillType.Attack => new Color(0.86f, 0.31f, 0.31f),
                SkillType.Heal => new Color(0.39f, 0.86f, 0.59f),
                SkillType.Buff => new Color(0.98f, 0.85f, 0.31f),
                SkillType.Debuff => new Color(0.6f, 0.2f, 0.8f),
                SkillType.Mixed => new Color(1f, 0.6f, 0.2f),
                _ => Color.white
            };
        }
        private void UpdateSkillButton(Button btn,
                                TMP_Text nameText,
                                TMP_Text dmgText,
                                TMP_Text descText,
                                GameObject cdGO,
                                TMP_Text cdText,
                                Image typeIcon,
                                CardSkill skill,
                                int cooldown,
                                CardInstance card,
                                Image darkenOverlay = null)
        {
            if (skill == null) return;

            if (nameText != null)
                nameText.text = skill.cooldownTurns > 0
                    ? $"{skill.skillName}  <size=75%><color=#aaaaaa>CD {skill.cooldownTurns}</color></size>"
                    : skill.skillName;

            if (typeIcon != null)
                typeIcon.gameObject.SetActive(false);

            // Valeur affichée selon type
            if (dmgText != null)
            {
                dmgText.text = skill.skillType switch
                {
                    SkillType.Attack => skill.damage > 0 ? skill.damage.ToString() : "",
                    SkillType.Heal => skill.GetImmediateHealPercent() > 0
                                        ? $"{skill.GetImmediateHealPercent() * 100:0}%"
                                        : "",
                    SkillType.Buff => "",
                    SkillType.Debuff => "",
                    SkillType.Mixed => skill.damage > 0 ? skill.damage.ToString() : "",
                    _ => ""
                };
            }

            TMPIconReplacer.ApplyWithModifiers(descText, skill.description, card);

            bool onCooldown = cooldown > 0;

            // Interaction
            if (btn != null)
            {
                btn.interactable = !onCooldown;
                // Neutraliser la transition ColorBlock (évite la transparence Unity par défaut)
                var colors = btn.colors;
                colors.disabledColor = Color.white;
                btn.colors = colors;
            }

            // Assombrissement à la place de la transparence
            if (darkenOverlay != null) darkenOverlay.gameObject.SetActive(onCooldown);

            if (cdGO != null) cdGO.SetActive(onCooldown);
            if (cdText != null && onCooldown) cdText.text = cooldown.ToString();
        }

        public void SelectSkill(int skillIndex)
        {
            selectedSkillIndex = skillIndex;
            cardSkillPanel.SetActive(false);

            if (currentZoomedCard != null)
            {
                currentZoomedCard.ZoomOut();
                currentZoomedCard = null;
            }

            HighlightValidTargets();

            // Affiche les previews de dégâts sur toutes les cartes adverses si AoE
            ShowAoEDamagePreview();

            // Affiche la flèche de ciblage depuis la carte sélectionnée
            var cardRT = selectedCard?.GetComponent<RectTransform>();
            if (cardRT != null)
            {
                var skill = skillIndex == 0 ? selectedCard.data.skillOne : selectedCard.data.skillTwo;
                bool isAlly    = skill == null || skill.TargetsAllies;
                Color arrowCol = isAlly
                    ? new Color(0.2f, 0.85f, 0.35f, 0.85f)
                    : new Color(0.9f, 0.2f, 0.2f, 0.85f);
                TargetingArrow.Instance?.Show(cardRT, arrowCol, isAlly);
                if (NetworkBridge.IsActive && selectedCard != null)
                {
                    Debug.Log($"[Net] Arrow show demandé — P{selectedCard.ownerPlayerID} slot {selectedCard.slotIndex}");
                    NetworkBridge.OnArrowShowRequested?.Invoke(selectedCard.ownerPlayerID, selectedCard.slotIndex);
                }
            }
        }

        public void CancelSelection()
        {
            ClearAllHighlights();
            HideDamagePreview();

            TargetingArrow.Instance?.Hide();
            if (NetworkBridge.IsActive)
            {
                NetworkBridge.OnArrowHideRequested?.Invoke();
                NetworkBridge.OnArrowTargetHideRequested?.Invoke();
                NetworkBridge.OnCardDeselectedRequested?.Invoke();
            }
            PassiveTooltipManager.Instance?.Hide();

            if (currentZoomedCard != null)
            {
                currentZoomedCard.ZoomOut();
                currentZoomedCard = null;
            }

            if (cardSkillPanel != null)
                cardSkillPanel.SetActive(false);
            if (cardSkillPanelBoss != null)
                cardSkillPanelBoss.SetActive(false);

            selectedCard = null;
            selectedSkillIndex = -1;
            currentReadOnlyCard = null;
        }

        public void CloseSkillPanel() => CancelSelection();

        // ─── Exécution de l'action ────────────────────────────────────

        public void ExecuteSelectedSkill(CardInstance target)
        {
            if (!HasSkillSelected) return;
            if (!selectedCard.IsAlive || !target.IsAlive) return;

            // Garde Invisible : une compétence SingleEnemy ne peut pas cibler une carte invisible
            var selectedSkill = GetSelectedSkill();
            if (target.IsInvisible && selectedSkill?.targetType == SkillTargetType.SingleEnemy)
                return;

            // ← Vérifie qu'il reste des actions
            if (TurnManager.Instance.actionsRemaining <= 0)
            {
                CancelSelection();
                return;
            }

            ClearAllHighlights();
            HideDamagePreview();
            TargetingArrow.Instance?.Hide();
            if (NetworkBridge.IsActive)
            {
                NetworkBridge.OnArrowHideRequested?.Invoke();
                NetworkBridge.OnArrowTargetHideRequested?.Invoke();
                NetworkBridge.OnCardDeselectedRequested?.Invoke();
            }

            ClearAllPreviewPopups();

            // En réseau : route via NetworkBridge
            if (NetworkBridge.IsActive)
                NetworkBridge.OnExecuteSkillRequested?.Invoke(selectedCard, selectedSkillIndex, target);
            else
                CombatManager.Instance.ExecuteSkill(selectedCard, selectedSkillIndex, target);

            HideDamagePreview();
            UpdateActionDots();

            selectedCard = null;
            selectedSkillIndex = -1;
        }

        public void ClearAllPreviewPopups()
        {
            for (int p = 0; p < 2; p++)
            {
                if (BoardManager.Instance == null) break;
                var cards = BoardManager.Instance.GetAliveCards(p);
                foreach (var card in cards)
                    card.GetComponent<CombatPopupHandler>()?.HideDamagePreviewPopup();
            }
        }

        // Retourne la compétence actuellement sélectionnée
        public CardSkill GetSelectedSkill()
        {
            if (selectedCard == null || selectedSkillIndex < 0) return null;
            return selectedSkillIndex == 0
                ? selectedCard.data.skillOne
                : selectedCard.data.skillTwo;
        }

        // Prévisualisation d'un soin
        private void ResetAllRows()
        {

            // ← Réactivez bar_FinalValue à chaque reset
            if (bar_FinalValue != null)
                bar_FinalValue.gameObject.SetActive(true);

            if (bar_BlockBuff != null) bar_BlockBuff.SetActive(false);
            if (bar_BlockArmor != null) bar_BlockArmor.SetActive(false);
            if (bar_BlockHeal != null) bar_BlockHeal.SetActive(false);

            // Cache aussi les séparateurs adjacents aux blocs dynamiques
            if (vSep2 != null) vSep2.SetActive(false);
            if (vSep3 != null) vSep3.SetActive(false);
            if (vSep4 != null) vSep4.SetActive(false);
            if (vSep5 != null) vSep5.SetActive(false);
        }
        public void ShowHealPreview(CardInstance target, Vector3 worldPos)
        {
            if (!HasSkillSelected || damagePreviewBar == null) return;
            var skill = GetSelectedSkill();
            if (skill == null) return;

            ResetAllRows();

            if (bar_SkillName != null)
                bar_SkillName.text = skill.skillName;

            bool healBlocked = target.activeEffects
                .Exists(e => e.type == EffectType.HealBlock);

            // Bonus Lumière (stacks)
            float healBonus = 0f;
            if (StackManager.Instance != null)
                healBonus = StackManager.Instance.GetHealBonus(selectedCard.ownerPlayerID);

            // ── ImmediateHeal ─────────────────────────────────────────────
            float healPercent = skill.GetImmediateHealPercent();
            if (healPercent > 0f)
            {
                int baseHeal = Mathf.RoundToInt(target.EffectiveMaxHP * healPercent);
                int boostedHeal = Mathf.RoundToInt(baseHeal * (1f + healBonus));
                int missingHP = target.EffectiveMaxHP - target.currentHP;
                int actualHeal = healBlocked ? 0 : Mathf.Min(boostedHeal, missingHP);

                if (bar_BaseValue != null)
                {
                    int healFixed = Mathf.RoundToInt(target.EffectiveMaxHP * healPercent);
                    bar_BaseValue.text = LocalizationManager.Get("combat_label_hp_gain", healFixed);
                }

                // Bonus Lumière
                if (bar_BlockBuff != null && healBonus > 0f && !healBlocked)
                {
                    bar_BlockBuff.SetActive(true);
                    if (vSep3 != null) vSep3.SetActive(true);
                    if (bar_BuffLabel != null)
                        bar_BuffLabel.text = LocalizationManager.Get("combat_label_lumiere_bonus", $"{healBonus * 100:0}");
                    if (bar_BuffValue != null)
                    {
                        bar_BuffValue.text = LocalizationManager.Get("combat_label_hp_gain", Mathf.RoundToInt(baseHeal * healBonus));
                        bar_BuffValue.color = new Color(0.39f, 0.86f, 0.59f);
                    }
                }

                if (bar_BlockHeal != null)
                {
                    bar_BlockHeal.SetActive(true);
                    if (bar_HealValue != null)
                    {
                        bar_HealValue.fontSize = 11f;
                        bar_HealValue.text = healBlocked
                            ? LocalizationManager.Get("preview_incurable")
                            : LocalizationManager.Get("combat_label_hp_gain", actualHeal);
                        bar_HealValue.color = healBlocked
                            ? new Color(0.6f, 0.6f, 0.6f)
                            : new Color(0.39f, 0.86f, 0.59f);
                    }
                }

                if (bar_FinalValue != null)
                {
                    bar_FinalValue.text = healBlocked ? "0" : $"+{actualHeal}";
                    bar_FinalValue.color = healBlocked
                        ? new Color(0.6f, 0.6f, 0.6f)
                        : new Color(0.39f, 0.86f, 0.59f);
                }
            }

            // ── HealOverTime ──────────────────────────────────────────────
            float hotPercent = skill.GetHealOverTimePercent();
            int hotDuration = skill.GetHealOverTimeDuration();
            if (hotPercent > 0f)
            {
                int hotBase = Mathf.RoundToInt(target.EffectiveMaxHP * hotPercent);
                int hotBoosted = Mathf.RoundToInt(hotBase * (1f + healBonus));
                int hotTotal = hotBoosted * hotDuration;

                if (bar_BaseValue != null)
                    bar_BaseValue.text = healPercent > 0f
                        ? bar_BaseValue.text + $" + {hotPercent * 100:0}%/tour"
                        : $"{hotPercent * 100:0}% PV/tour";

                // Réutilise bar_BlockArmor pour afficher le HoT
                if (bar_BlockArmor != null)
                {
                    bar_BlockArmor.SetActive(true);
                    if (vSep4 != null) vSep4.SetActive(true);
                    if (bar_ArmorLabel != null)
                        bar_ArmorLabel.text = LocalizationManager.Get("combat_label_regen_turns", hotDuration);
                    if (bar_ArmorValue != null)
                    {
                        bar_ArmorValue.text = healBlocked
                            ? LocalizationManager.Get("preview_incurable")
                            : LocalizationManager.Get("preview_hot_total", hotTotal);
                        bar_ArmorValue.color = healBlocked
                            ? new Color(0.6f, 0.6f, 0.6f)
                            : new Color(0.39f, 0.86f, 0.59f);
                    }
                }

                if (healPercent == 0f && bar_FinalValue != null)
                {
                    bar_FinalValue.text = healBlocked ? "0" : $"+{hotTotal}";
                    bar_FinalValue.color = healBlocked
                        ? new Color(0.6f, 0.6f, 0.6f)
                        : new Color(0.39f, 0.86f, 0.59f);
                }
            }
            CancelHideDamagePreview();
            ShowDamagePreviewBar();
            PositionPreviewNearCursor();
        }

        // ─── DamagePreviewBubble ──────────────────────────────────────

        // Respecte l'option Panel_Settings (CombatOptionsPanel.DamagePreviewBarEnabled)
        private void ShowDamagePreviewBar()
        {
            if (damagePreviewBar == null) return;
            damagePreviewBar.SetActive(CombatOptionsPanel.DamagePreviewBarEnabled);
        }

        private Coroutine hidePreviewCoroutine;

        public void HideDamagePreview()
        {
            if (hidePreviewCoroutine != null)
            {
                StopCoroutine(hidePreviewCoroutine);
                hidePreviewCoroutine = null;
            }
            if (damagePreviewBar != null)
                damagePreviewBar.SetActive(false);
        }

        // Démarre un délai avant de cacher la barre — annulable si le curseur entre sur la barre.
        public void HideDamagePreviewSoon()
        {
            if (hidePreviewCoroutine != null) { StopCoroutine(hidePreviewCoroutine); hidePreviewCoroutine = null; }
            hidePreviewCoroutine = StartCoroutine(HidePreviewDelayed());
        }

        public void CancelHideDamagePreview()
        {
            if (hidePreviewCoroutine != null) { StopCoroutine(hidePreviewCoroutine); hidePreviewCoroutine = null; }
        }

        private IEnumerator HidePreviewDelayed()
        {
            yield return new WaitForSeconds(0.5f);
            if (damagePreviewBar != null)
                damagePreviewBar.SetActive(false);
            hidePreviewCoroutine = null;
        }

        // rebuild=true + snap immédiat à l'ouverture ; rebuild=false + smooth pour le suivi frame-par-frame
        private void PositionPreviewNearCursor(bool rebuild = true, bool smooth = false)
        {
            if (damagePreviewBar == null) return;
            Canvas cv = damagePreviewBar.GetComponentInParent<Canvas>();
            if (cv == null) return;

            var barRT  = damagePreviewBar.GetComponent<RectTransform>();
            var cvRT   = cv.GetComponent<RectTransform>();
            Camera cam = cv.renderMode == RenderMode.ScreenSpaceCamera ? cv.worldCamera : null;

            if (rebuild)
                LayoutRebuilder.ForceRebuildLayoutImmediate(barRT);

            float barW    = barRT.rect.width;
            float barH    = barRT.rect.height;
            const float gap = 15f;
            Vector2 cvHalf = cvRT.rect.size * 0.5f;

            Vector2 mouseScreen = Mouse.current != null
                ? Mouse.current.position.ReadValue()
                : new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);

            RectTransformUtility.ScreenPointToLocalPointInRectangle(cvRT, mouseScreen, cam, out Vector2 mouseLocal);

            barRT.anchorMin = barRT.anchorMax = new Vector2(0.5f, 0.5f);
            barRT.pivot = new Vector2(0.5f, 0.5f);

            // Bord gauche de la barre à 15px du curseur (droite en priorité, sinon gauche)
            float x = mouseLocal.x + gap + barW * 0.5f;
            if (x + barW * 0.5f > cvHalf.x)
                x = mouseLocal.x - gap - barW * 0.5f;

            // Centré verticalement sur le curseur
            float y = mouseLocal.y;
            x = Mathf.Clamp(x, -cvHalf.x + barW * 0.5f, cvHalf.x - barW * 0.5f);
            y = Mathf.Clamp(y, -cvHalf.y + barH * 0.5f, cvHalf.y - barH * 0.5f);

            var target = new Vector2(x, y);
            if (smooth)
                barRT.anchoredPosition = Vector2.Lerp(barRT.anchoredPosition, target, Time.unscaledDeltaTime * 15f);
            else
                barRT.anchoredPosition = target;
        }

        public void ShowDamagePreview(CardInstance target, Vector3 worldPos)
        {
            if (!HasSkillSelected || damagePreviewBar == null) return;

            var skill = GetSelectedSkill();
            if (skill == null) return;

            ResetAllRows();

            // ── Nom compétence ────────────────────────────────────────────
            if (bar_SkillName != null)
                bar_SkillName.text = skill.skillName;

            // ── Dégâts de base ────────────────────────────────────────────
            if (bar_BaseValue != null)
                bar_BaseValue.text = skill.damage.ToString();

            // ── Collecte des modificateurs ────────────────────────────────

            // Bonus Feu mineur (attaquant)
            float fireBonus = 0f;
            if (StackManager.Instance != null)
                fireBonus = StackManager.Instance.GetFireDamageBonus(selectedCard.ownerPlayerID);

            // AttackBoost (flat) + AttackBoostFlat — cumulés ensemble pour l'affichage
            float attackBoostFlat = 0f;
            foreach (var eff in selectedCard.activeEffects)
            {
                if (eff.type == EffectType.AttackBoost)     attackBoostFlat += eff.value;
                if (eff.type == EffectType.AttackBoostFlat) attackBoostFlat += eff.value;
            }
            foreach (var cpe in selectedCard.conditionalPassiveEffects)
            {
                int s = StackManager.Instance?.GetStacks(selectedCard.ownerPlayerID, cpe.triggerElement) ?? 0;
                if (s < cpe.requiredThreshold) continue;
                if (cpe.type == EffectType.AttackBoost)     attackBoostFlat += cpe.value;
                if (cpe.type == EffectType.AttackBoostFlat) attackBoostFlat += cpe.value;
            }

            // Amplification sur la cible
            var ampEff = target.activeEffects.Find(e => e.type == EffectType.DamageAmplify);
            bool hasDmgAmp = ampEff != null;

            // Réductions (Eau mineur, Eau majeur cartes Eau, DamageReduction buff)
            float waterReduction = 0f;
            if (StackManager.Instance != null)
                waterReduction = StackManager.Instance.GetWaterDamageReduction(target.ownerPlayerID);

            float waterMajorReduction = 0f;
            if (StackManager.Instance != null && target.data.element == Element.Eau)
                waterMajorReduction = StackManager.Instance.GetWaterMajorEnemyReduction(target.ownerPlayerID);

            var dmgRedEff = target.activeEffects.Find(e => e.type == EffectType.DamageReduction);
            bool hasDmgRed = dmgRedEff != null;

            float armorIgnore = skill.GetArmorIgnorePercent() >= 1f ? 1f : 0f;
            float effArmor = target.TotalArmor;

            // ── Bloc offensif : Feu / AttackBoost (flat) / Amplify ──────────
            bool hasOffensive = fireBonus > 0f || attackBoostFlat > 0f || hasDmgAmp;

            if (bar_BlockBuff != null)
            {
                bar_BlockBuff.SetActive(hasOffensive);
                if (vSep3 != null) vSep3.SetActive(hasOffensive);

                if (hasOffensive)
                {
                    if (bar_BuffLabel != null)
                    {
                        var parts = new System.Collections.Generic.List<string>();
                        if (fireBonus > 0f)       parts.Add(LocalizationManager.Get("preview_fire_bonus", $"{fireBonus * 100:0}"));
                        if (attackBoostFlat > 0f) parts.Add(LocalizationManager.Get("preview_atkflat_bonus", $"{attackBoostFlat:0}"));
                        if (hasDmgAmp)            parts.Add(LocalizationManager.Get("preview_amplified_bonus", $"{ampEff.value * 100:0}"));
                        bar_BuffLabel.text = string.Join(" | ", parts);
                    }

                    if (bar_BuffValue != null)
                    {
                        float totalOffPct = fireBonus * 100f + (hasDmgAmp ? ampEff.value * 100f : 0f);
                        string buffVal = totalOffPct > 0f ? $"+{totalOffPct:0}%" : "";
                        if (attackBoostFlat > 0f)
                            buffVal += (buffVal.Length > 0 ? " " : "") + LocalizationManager.Get("preview_atkflat_bonus", $"{attackBoostFlat:0}");
                        bar_BuffValue.text  = buffVal;
                        bar_BuffValue.color = new Color(1f, 0.85f, 0.31f);
                    }
                }
            }

            // ── Bloc défensif : Armure / Eau / DamageReduction ──────────
            bool showDefense = effArmor > 0 || armorIgnore > 0f
                            || waterReduction > 0f || waterMajorReduction > 0f
                            || hasDmgRed;

            if (bar_BlockArmor != null)
            {
                bar_BlockArmor.SetActive(showDefense);
                if (vSep4 != null) vSep4.SetActive(showDefense);

                if (showDefense)
                {
                    // Label : liste des éléments présents
                    if (bar_ArmorLabel != null)
                    {
                        var parts = new System.Collections.Generic.List<string>();
                        if (effArmor > 0)
                            parts.Add(armorIgnore >= 1f
                                ? LocalizationManager.Get("preview_armor_ignored")
                                : LocalizationManager.Get("preview_armor"));
                        if (waterReduction > 0f || waterMajorReduction > 0f)
                            parts.Add(LocalizationManager.Get("ui_element_eau"));
                        if (hasDmgRed)
                            parts.Add(LocalizationManager.Get("preview_reduce"));
                        bar_ArmorLabel.text = string.Join(" + ", parts);
                    }

                    // Valeur : détail compact
                    if (bar_ArmorValue != null)
                    {
                        var parts = new System.Collections.Generic.List<string>();
                        if (effArmor > 0)
                            parts.Add(armorIgnore >= 1f
                                ? LocalizationManager.Get("preview_armor_pts_ignored", (int)effArmor)
                                : LocalizationManager.Get("preview_armor_pts", (int)effArmor));
                        float totalWater = waterReduction + waterMajorReduction;
                        if (totalWater > 0f)
                            parts.Add(LocalizationManager.Get("combat_label_malus_pct", $"{totalWater * 100:0}"));
                        if (hasDmgRed)
                            parts.Add(LocalizationManager.Get("combat_label_malus_pct", $"{dmgRedEff.value * 100:0}"));
                        bar_ArmorValue.text  = string.Join(" | ", parts);
                        bar_ArmorValue.color = new Color(0.6f, 0.85f, 1f);
                    }
                }
            }

            // ── Bloc effets secondaires : DoT / Drain / Poison ──────────
            var secondaryParts = new System.Collections.Generic.List<string>();

            foreach (var eff in skill.effects)
            {
                switch (eff.type)
                {
                    case EffectType.Saignement when eff.durationTurns > 0:
                        secondaryParts.Add(LocalizationManager.Get("preview_dot_fx", $"{eff.value * 100:0}", eff.durationTurns));
                        break;
                    case EffectType.ImmediateHeal when eff.durationTurns == -1:
                        int drainAmt = Mathf.RoundToInt(skill.damage * eff.value);
                        secondaryParts.Add(LocalizationManager.Get("preview_drain_fx", drainAmt));
                        break;
                    case EffectType.LifeSteal when eff.durationTurns == -1:
                        int stealAmt = Mathf.RoundToInt(skill.damage * eff.value);
                        secondaryParts.Add(LocalizationManager.Get("preview_lifesteal_fx", stealAmt, $"{eff.value * 100:0}"));
                        break;
                    case EffectType.HealBlock:
                        secondaryParts.Add(LocalizationManager.Get("preview_incurable"));
                        break;
                    case EffectType.Stun:
                        secondaryParts.Add(LocalizationManager.Get("preview_stun_fx"));
                        break;
                    case EffectType.CooldownIncrease:
                        secondaryParts.Add(LocalizationManager.Get("preview_cd_inc_fx"));
                        break;
                }
            }

            // Passif dynamique CardIsBurning
            float burningPassiveBonus = DamageCalculator.GetBurningPassiveFlatBonus(selectedCard);
            if (burningPassiveBonus > 0f)
            {
                int burningCount = DamageCalculator.CountBurningCardsOnField();
                secondaryParts.Add(LocalizationManager.Get("preview_burning_passive_fx", Mathf.RoundToInt(burningPassiveBonus), burningCount));
            }

            // Passif dynamique ForEachAllyAlive
            float allyAlivePassiveBonus = DamageCalculator.GetAllyAliveFlatBonus(selectedCard);
            if (allyAlivePassiveBonus > 0f)
            {
                int allyCount = DamageCalculator.CountAliveAllies(selectedCard);
                secondaryParts.Add(LocalizationManager.Get("preview_ally_alive_passive_fx", Mathf.RoundToInt(allyAlivePassiveBonus), allyCount));
            }

            // LifeSteal buff persistant sur l'attaquant + bonus Ténèbres stacks
            float activeLifeSteal = 0f;
            foreach (var eff in selectedCard.activeEffects)
                if (eff.type == EffectType.LifeSteal)
                    activeLifeSteal += eff.value;
            if (selectedCard.data.element == Element.Tenebres && StackManager.Instance != null)
                activeLifeSteal += StackManager.Instance.GetDarkLifeStealBonus(selectedCard.ownerPlayerID);
            if (activeLifeSteal > 0f)
            {
                var preview2 = DamageCalculator.GetPreview(selectedCard, skill, target);
                int stealAmt2 = Mathf.RoundToInt(preview2.estimatedDamage * activeLifeSteal);
                secondaryParts.Add(LocalizationManager.Get("preview_lifesteal_fx", stealAmt2, $"{activeLifeSteal * 100:0}"));
            }

            bool hasSecondary = secondaryParts.Count > 0;
            if (bar_BlockHeal != null)
            {
                bar_BlockHeal.SetActive(hasSecondary);
                if (vSep5 != null) vSep5.SetActive(hasSecondary);
                if (hasSecondary && bar_HealValue != null)
                {
                    bar_HealValue.fontSize = 9f;
                    bar_HealValue.text     = string.Join("  |  ", secondaryParts);
                    bar_HealValue.color    = new Color(1f, 0.6f, 0.2f); // orange
                }
            }

            // ── Dégâts finaux ─────────────────────────────────────────────
            var preview = DamageCalculator.GetPreview(selectedCard, skill, target);
            if (bar_FinalValue != null)
            {
                bar_FinalValue.text = preview.hpDamage > 0 && effArmor > 0 && armorIgnore < 1f
                    ? LocalizationManager.Get("combat_label_hp", preview.hpDamage)
                    : preview.estimatedDamage.ToString();
                bar_FinalValue.color = preview.isAmplified
                    ? new Color(1f, 0.5f, 0.3f)
                    : preview.isReduced
                    ? new Color(0.6f, 0.85f, 1f)
                    : new Color(1f, 0.85f, 0.31f);
            }

            CancelHideDamagePreview();
            ShowDamagePreviewBar();

            // ── Dégâts adjacents — écrase le bloc offensif ────────────────
            if (skill.targetType == SkillTargetType.AdjacentEnemies
                && skill.adjacentDamagePercent > 0f)
            {
                int adjDmg = Mathf.RoundToInt(preview.estimatedDamage * skill.adjacentDamagePercent);

                if (bar_BlockBuff != null)
                {
                    bar_BlockBuff.SetActive(true);
                    if (vSep3 != null) vSep3.SetActive(true);
                    if (bar_BuffLabel != null)
                        bar_BuffLabel.text = LocalizationManager.Get("combat_label_adj_dmg", $"{skill.adjacentDamagePercent * 100:0}");
                    if (bar_BuffValue != null)
                    {
                        bar_BuffValue.text  = $"-{adjDmg}";
                        bar_BuffValue.color = new Color(1f, 0.75f, 0.4f);
                    }
                }
            }

            PositionPreviewNearCursor();
        }

        public void ShowAdjacentDamagePreview(CardInstance primaryTarget)
        {
            var skill = GetSelectedSkill();
            if (skill == null || selectedCard == null) return;

            // D'abord masque tous les previews existants
            int enemyID = NetworkBridge.IsActive
                ? 1 - NetworkBridge.LocalPlayerID
                : (TurnManager.Instance.currentPlayerID == 0 ? 1 : 0);
            foreach (var enemy in BoardManager.Instance.GetAliveCards(enemyID))
                enemy.GetComponent<CombatPopupHandler>()?.HideDamagePreviewPopup();

            // Calcule les dégâts principaux sur la cible survolée
            int mainDmg = DamageCalculator.Calculate(selectedCard, skill, primaryTarget);
            primaryTarget.GetComponent<CombatPopupHandler>()
                ?.ShowDamagePreviewPopup(mainDmg, false);

            // Dégâts adjacents uniquement sur les cartes à côté de la cible survolée
            if (skill.adjacentDamagePercent > 0f)
            {
                var adjacents = BoardManager.Instance.GetAdjacentCards(primaryTarget);
                foreach (var adj in adjacents)
                {
                    if (!adj.IsAlive) continue;
                    int adjDmg = Mathf.RoundToInt(mainDmg * skill.adjacentDamagePercent);
                    adj.GetComponent<CombatPopupHandler>()
                        ?.ShowDamagePreviewPopup(adjDmg, false);
                }
            }
        }

        public void ShowBuffPreview(CardInstance target, CardSkill skill)
        {
            if (damagePreviewBar == null || skill == null) return;

            ResetAllRows();

            if (bar_SkillName != null)
                bar_SkillName.text = skill.skillName;

            if (bar_FinalValue != null)
                bar_FinalValue.gameObject.SetActive(false);

            foreach (var eff in skill.effects)
            {
                // ── Soin immédiat ─────────────────────────────────────
                if (eff.type == EffectType.ImmediateHeal)
                {
                    int healAmt = Mathf.RoundToInt(target.EffectiveMaxHP * eff.value);
                    if (bar_BlockHeal != null)
                    {
                        bar_BlockHeal.SetActive(true);
                        if (bar_ArmorLabel != null) bar_ArmorLabel.text = LocalizationManager.Get("combat_label_heal");
                        if (bar_ArmorValue != null)
                        {
                            bar_ArmorValue.fontSize = 11f;
                            bar_ArmorValue.text  = LocalizationManager.Get("combat_label_hp_gain", healAmt);
                            bar_ArmorValue.color = new Color(0.39f, 0.86f, 0.59f);
                        }
                    }
                }

                // ── HealOverTime ──────────────────────────────────────
                if (eff.type == EffectType.HealOverTime)
                {
                    int hotPerTurn = Mathf.RoundToInt(target.EffectiveMaxHP * eff.value);
                    int hotTotal = hotPerTurn * Mathf.Max(1, eff.durationTurns);
                    if (bar_BlockHeal != null)
                    {
                        bar_BlockHeal.SetActive(true);
                        if (bar_ArmorLabel != null)
                            bar_ArmorLabel.text = eff.durationTurns > 0
                                ? LocalizationManager.Get("preview_hot_x_turns", eff.durationTurns)
                                : LocalizationManager.Get("preview_hot_per_turn");
                        if (bar_ArmorValue != null)
                        {
                            bar_ArmorValue.fontSize = 11f;
                            bar_ArmorValue.text = eff.durationTurns > 0
                                ? LocalizationManager.Get("preview_hp_total", hotPerTurn, hotTotal)
                                : LocalizationManager.Get("combat_label_hp_gain", hotPerTurn);
                            bar_ArmorValue.color = new Color(0.39f, 0.86f, 0.59f);
                        }
                    }
                }

                // ── GiveArmor ────────────────────────────────────────
                if (eff.type == EffectType.GiveArmor || eff.type == EffectType.GiveArmorAdjacent)
                {
                    if (bar_BlockArmor != null)
                    {
                        bar_BlockArmor.SetActive(true);
                        if (bar_ArmorLabel != null)
                            bar_ArmorLabel.text = eff.type == EffectType.GiveArmorAdjacent
                                ? LocalizationManager.Get("preview_armor_adj")
                                : LocalizationManager.Get("preview_armor");
                        if (bar_ArmorValue != null)
                        {
                            bar_ArmorValue.text = LocalizationManager.Get("combat_label_armor_pts", (int)eff.value);
                            bar_ArmorValue.color = new Color(0.6f, 0.85f, 1f);
                        }
                    }
                }

                // ── AttackBoost (flat) ────────────────────────────────
                if (eff.type == EffectType.AttackBoost)
                {
                    if (bar_BlockBuff != null)
                    {
                        bar_BlockBuff.SetActive(true);
                        if (vSep3 != null) vSep3.SetActive(true);
                        if (bar_BuffLabel != null)
                            bar_BuffLabel.text = eff.durationTurns > 0
                                ? LocalizationManager.Get("preview_boost_atkflat_dur", eff.durationTurns)
                                : LocalizationManager.Get("preview_boost_atkflat");
                        if (bar_BuffValue != null)
                        {
                            bar_BuffValue.text = LocalizationManager.Get("preview_atkflat_bonus", $"{eff.value:0}");
                            bar_BuffValue.color = new Color(1f, 0.85f, 0.31f);
                        }
                    }
                }

                // ── AttackBoostFlat ───────────────────────────────────
                if (eff.type == EffectType.AttackBoostFlat)
                {
                    if (bar_BlockBuff != null)
                    {
                        bar_BlockBuff.SetActive(true);
                        if (vSep3 != null) vSep3.SetActive(true);
                        if (bar_BuffLabel != null)
                            bar_BuffLabel.text = eff.durationTurns > 0
                                ? LocalizationManager.Get("preview_boost_atkflat_dur", eff.durationTurns)
                                : LocalizationManager.Get("preview_boost_atkflat");
                        if (bar_BuffValue != null)
                        {
                            bar_BuffValue.text = LocalizationManager.Get("preview_atkflat_bonus", $"{eff.value:0}");
                            bar_BuffValue.color = new Color(1f, 0.85f, 0.31f);
                        }
                    }
                }

                // ── DamageReduction ──────────────────────────────────
                if (eff.type == EffectType.DamageReduction)
                {
                    if (bar_BlockBuff != null)
                    {
                        bar_BlockBuff.SetActive(true);
                        if (bar_BuffLabel != null)
                            bar_BuffLabel.text = eff.durationTurns > 0
                                ? LocalizationManager.Get("preview_dmgred_dur", eff.durationTurns)
                                : LocalizationManager.Get("preview_dmgred");
                        if (bar_BuffValue != null)
                        {
                            bar_BuffValue.text = LocalizationManager.Get("combat_label_malus_pct", $"{eff.value * 100:0}");
                            bar_BuffValue.color = new Color(0.39f, 0.86f, 0.59f);
                        }
                    }
                }

                // ── DamageAmplify ─────────────────────────────────────
                if (eff.type == EffectType.DamageAmplify)
                {
                    if (bar_BlockBuff != null)
                    {
                        bar_BlockBuff.SetActive(true);
                        if (bar_BuffLabel != null)
                            bar_BuffLabel.text = eff.durationTurns > 0
                                ? LocalizationManager.Get("preview_amplify_dur", eff.durationTurns)
                                : LocalizationManager.Get("preview_amplify");
                        if (bar_BuffValue != null)
                        {
                            bar_BuffValue.text = LocalizationManager.Get("combat_label_bonus_pct", $"{eff.value * 100:0}");
                            bar_BuffValue.color = new Color(1f, 0.5f, 0.2f);
                        }
                    }
                }
            }

            CancelHideDamagePreview();
            ShowDamagePreviewBar();
            PositionPreviewNearCursor();
        }


        //--------->Pour les AOE
        public void ShowAoEDamagePreview()
        {
            var skill = GetSelectedSkill();
            if (skill == null) return;

            int enemyID = NetworkBridge.IsActive
                ? 1 - NetworkBridge.LocalPlayerID
                : (TurnManager.Instance.currentPlayerID == 0 ? 1 : 0);
            var enemies = BoardManager.Instance.GetAliveCards(enemyID);

            if (skill.targetType == SkillTargetType.AllEnemies)
            {
                // Chaque carte reçoit ses propres dégâts calculés
                foreach (var enemy in enemies)
                {
                    int estimated = DamageCalculator.Calculate(selectedCard, skill, enemy);
                    enemy.GetComponent<CombatPopupHandler>()
                        ?.ShowDamagePreviewPopup(estimated, false);
                }
            }
            else if (skill.targetType == SkillTargetType.AdjacentEnemies)
            {
                // Dégâts principaux sur chaque cible possible
                foreach (var enemy in enemies)
                {
                    int mainDmg = DamageCalculator.Calculate(selectedCard, skill, enemy);
                    enemy.GetComponent<CombatPopupHandler>()
                        ?.ShowDamagePreviewPopup(mainDmg, false);

                    // Dégâts adjacents sur les cartes autour de chaque cible possible
                    var adjacents = BoardManager.Instance.GetAdjacentCards(enemy);
                    foreach (var adj in adjacents)
                    {
                        if (!adj.IsAlive) continue;
                        int adjDmg = Mathf.RoundToInt(mainDmg * skill.adjacentDamagePercent);

                        // Affiche uniquement si les dégâts adjacents sont plus élevés
                        // (une carte peut être adjacente à plusieurs cibles)
                        var adjPopup = adj.GetComponent<CombatPopupHandler>();
                        if (adjPopup != null)
                        {
                            int currentVal = adjPopup.GetCurrentPreviewValue();
                            if (adjDmg > currentVal)
                                adjPopup.ShowDamagePreviewPopup(adjDmg, false);
                        }
                    }
                }
            }
        }

        // ─── CardInspectPanel ─────────────────────────────────────────

        public void OpenInspectPanel(CardInstance card)
        {
            // Implémenté plus tard (CardInspectPanel reporté)
            Debug.Log($"Inspection de {card.data.cardName} — à implémenter");
        }

        // ─── ActionBar ────────────────────────────────────────────────

        public void UpdateActionDots()
        {
            int actions = TurnManager.Instance.actionsRemaining;
            bool dot1Used = actions < 1;
            bool dot2Used = actions < 2;

            if (dot1 != null)
            {
                dot1.sprite = dot1Used && spriteActionUsed != null ? spriteActionUsed : _dot1OriginalSprite;
                if (dot1Used  && !_dot1WasUsed) AnimateDotUsed(dot1, _dot1OriginalColor);
                if (!dot1Used && _dot1WasUsed)  ResetDotVisual(dot1, _dot1OriginalColor);
                _dot1WasUsed = dot1Used;
            }

            if (dot2 != null)
            {
                dot2.sprite = dot2Used && spriteActionUsed != null ? spriteActionUsed : _dot2OriginalSprite;
                if (dot2Used  && !_dot2WasUsed) AnimateDotUsed(dot2, _dot2OriginalColor);
                if (!dot2Used && _dot2WasUsed)  ResetDotVisual(dot2, _dot2OriginalColor);
                _dot2WasUsed = dot2Used;
            }

            bool allUsed = actions <= 0;
            if (btnFinTourImage != null)
                btnFinTourImage.color = allUsed ? btnFinTourHighlight : btnFinTourNormal;
            BtnFinTourGlow.Instance?.SetGlowing(allUsed);
        }

        private void AnimateDotUsed(Image dot, Color originalColor)
        {
            dot.transform.DOKill();
            dot.DOKill();
            var seq = DOTween.Sequence().SetUpdate(true);
            seq.Append(dot.transform.DOPunchScale(Vector3.one * 0.4f, 0.3f, 1, 0.4f));
            seq.Join(dot.DOColor(dotUsedColor, 0.25f).SetDelay(0.08f).SetEase(Ease.InQuad));
        }

        private void ResetDotVisual(Image dot, Color originalColor)
        {
            dot.transform.DOKill();
            dot.DOKill();
            dot.transform.localScale = Vector3.one;
            dot.color = originalColor;
        }

        public void UpdateTurnIndicator(int playerID)
        {
            if (turnText == null) return;
            bool isMyTurn = !NetworkBridge.IsActive
                ? playerID == 0
                : playerID == NetworkBridge.LocalPlayerID;

            if (NetworkBridge.IsActive)
                turnText.text = isMyTurn ? NetworkBridge.LocalPlayerName : NetworkBridge.OpponentPlayerName;
            else
                turnText.text = isMyTurn
                    ? LocalizationManager.Get("combat_turn_your")
                    : LocalizationManager.Get("combat_turn_opponent");
        }

        private void Update()
        {
            // Échap → annule la sélection
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
                CancelSelection();

            // La barre suit le curseur en temps réel, mouvement lissé
            if (damagePreviewBar != null && damagePreviewBar.activeSelf)
                PositionPreviewNearCursor(false, true);
        }

        //Add Highlights to targeted cards

        public void HighlightValidTargets()
        {
            if (!HasSkillSelected) return;

            var skill = selectedSkillIndex == 0
                ? selectedCard.data.skillOne
                : selectedCard.data.skillTwo;

            if (skill.targetType == SkillTargetType.Self)
                HighlightSelfTarget();
            else if (skill.TargetsAllies)
                HighlightAlliedTargets();
            else
                HighlightEnemyTargets();
        }

        private void HighlightEnemyTargets()
        {
            int enemyID = NetworkBridge.IsActive
                ? 1 - NetworkBridge.LocalPlayerID
                : (TurnManager.Instance.currentPlayerID == 0 ? 1 : 0);
            var enemies = BoardManager.Instance.GetAliveCards(enemyID);

            var skill = GetSelectedSkill();
            bool isSingleEnemy = skill?.targetType == SkillTargetType.SingleEnemy;

            foreach (var enemy in enemies)
            {
                if (isSingleEnemy && enemy.IsInvisible) continue;
                var highlight = enemy.GetComponent<CardTargetHighlight>();
                highlight?.ActivateHighlight(HighlightType.Attack);

                if (skill != null && WouldExecute(selectedCard, skill, enemy))
                    highlight?.ActivateExecuteIndicator();
                else
                    highlight?.DeactivateExecuteIndicator();
            }
        }

        // Vrai si une branche BranchEffectType.Execute de la compétence tuerait cette cible
        // maintenant (condition déjà remplie) — utilisé pour l'indicateur visuel de ciblage,
        // pas pour l'application réelle de l'effet (voir SkillExecutor.ApplyBranches).
        private bool WouldExecute(CardInstance attacker, CardSkill skill, CardInstance target)
        {
            if (skill.branches == null || target?.data == null) return false;
            if (target.data.immuneToExecute) return false;

            foreach (var branch in skill.branches)
            {
                if (branch.effectType != BranchEffectType.Execute) continue;
                if (branch.target != BranchTarget.Target) continue;
                if (branch.condition.Evaluate(attacker, target)) return true;
            }
            return false;
        }

        private void HighlightSelfTarget()
        {
            bool isBlocked = selectedCard.activeEffects.Exists(e => e.type == EffectType.HealBlock);
            var highlight = selectedCard.GetComponent<CardTargetHighlight>();
            highlight?.ActivateHighlight(isBlocked ? HighlightType.Blocked : HighlightType.Heal);
        }

        private void HighlightAlliedTargets()
        {
            int allyID = NetworkBridge.IsActive
                ? NetworkBridge.LocalPlayerID
                : TurnManager.Instance.currentPlayerID;
            var allies = BoardManager.Instance.GetAliveCards(allyID);

            foreach (var ally in allies)
            {
                // Vérifie si la carte a le débuff HealBlock
                bool isBlocked = ally.activeEffects
                    .Exists(e => e.type == EffectType.HealBlock);

                var highlight = ally.GetComponent<CardTargetHighlight>();
                if (isBlocked)
                    // Grisée — non soignable
                    highlight?.ActivateHighlight(HighlightType.Blocked);
                else
                    // Verte — soignable
                    highlight?.ActivateHighlight(HighlightType.Heal);
            }
        }

        public void ClearAllHighlights()
        {
            for (int p = 0; p < 2; p++)
            {
                var cards = BoardManager.Instance.GetAliveCards(p);
                foreach (var card in cards)
                {
                    var highlight = card.GetComponent<CardTargetHighlight>();
                    highlight?.DeactivateHighlight();
                    highlight?.DeactivateExecuteIndicator();
                }
            }
        }

        private Sprite GetSkillIcon(SkillType type)
        {
            var lib = IconLibrary.Instance;
            if (lib == null) return null;

            return type switch
            {
                SkillType.Attack => lib.iconAttack,
                SkillType.Heal => lib.iconHeal,
                SkillType.Buff => lib.iconBuff,
                SkillType.Debuff => lib.iconDebuff,
                SkillType.Mixed => lib.iconAttack,
                _ => lib.iconAttack
            };
        }

        // Appelé par le bouton ConfirmGiveUp — passe toujours par GameManager.Instance
        // pour éviter le problème DontDestroyOnLoad (le GM local est détruit au chargement)
        public void ConfirmGiveUp()
        {
            // Ferme le panel d'abandon pour éviter le chevauchement avec Panel_EndGame
            var canvas = FindAnyObjectByType<Canvas>();
            if (canvas != null)
            {
                foreach (var t in canvas.GetComponentsInChildren<Transform>(true))
                {
                    if (t.name == "Panel_GiveUpNormal")
                    {
                        t.gameObject.SetActive(false);
                        break;
                    }
                }
            }
            GameManager.Instance?.GiveUp();
        }
    }
}