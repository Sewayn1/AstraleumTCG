using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Linq;

namespace Astraleum
{
    public class CardVisualUpdater : MonoBehaviour
    {
        [Header("Références visuelles")]
        public TMP_Text hpCurrent;
        public TMP_Text hpMax;
        public TMP_Text armorText;
        public TMP_Text skill1DMG;
        public TMP_Text skill2DMG;
        public GameObject skill1CDIndicator;
        public GameObject skill2CDIndicator;
        public TMP_Text skill1CDText;
        public TMP_Text skill2CDText;
        public GameObject stateIcon;
        public GameObject exhaustedOverlay;
        public GameObject destroyedOverlay;

        private CardInstance cardInstance;
        private CanvasGroup canvasGroup;

        [Header("Icônes compétences")]
        public GameObject healGroup;
        public TMP_Text healValue;
        public GameObject armorGroup;
        public TMP_Text armorValue;

        [Header("Compétences — Carte")]
        public Image skill1TypeIcon;
        public TMP_Text skill1Name;
        public Image skill2TypeIcon;
        public TMP_Text skill2Name;


        [Header("Passif")]
        public Image passiveIcon; // ← icône coin inférieur droit de l'artwork

        [Header("Effets de statut")]
        public Image burnIcon;         // ← icône brûlure active sur la carte
        public Image poisonIcon;       // ← icône poison actif sur la carte
        public Image saignementIcon;   // ← icône saignement actif sur la carte
        public Image invisibleOverlay; // ← overlay bleu semi-transparent état Invisible

        [Header("Astral")]
        public GameObject astralArrow;
        public Image astralArrowImage;

        private void Awake()
        {
            cardInstance = GetComponent<CardInstance>();
            canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null)
                canvasGroup = gameObject.AddComponent<CanvasGroup>();
            FindReferences();
        }

        private void FindReferences()
        {
            if (hpCurrent == null)
                hpCurrent = transform.Find("HPCurrent")?.GetComponent<TMP_Text>();
            if (hpMax == null)
                hpMax = transform.Find("HPMax")?.GetComponent<TMP_Text>();
            if (armorText == null)
                armorText = transform.Find("ArmorText")?.GetComponent<TMP_Text>();
            if (skill1CDIndicator == null)
                skill1CDIndicator = transform.Find("Skill1_Cooldown")?.gameObject;
            if (skill2CDIndicator == null)
                skill2CDIndicator = transform.Find("Skill2_Cooldown")?.gameObject;
            if (skill1CDText == null)
                skill1CDText = transform.Find("Skill1_Cooldown/CD1_Text")?.GetComponent<TMP_Text>();
            if (skill2CDText == null)
                skill2CDText = transform.Find("Skill2_Cooldown/CD2_Text")?.GetComponent<TMP_Text>();
            if (stateIcon == null)
                stateIcon = transform.Find("StateIcon")?.gameObject;
            if (exhaustedOverlay == null)
                exhaustedOverlay = transform.Find("ExhaustedOverlay")?.gameObject;
            if (destroyedOverlay == null)
                destroyedOverlay = transform.Find("DestroyedOverlay")?.gameObject;
            if (skill1Name == null)
                skill1Name = transform.Find("SkillZone/Skill1_Row/Skill1_Name")?.GetComponent<TMP_Text>();
            if (skill1DMG == null)
                skill1DMG = transform.Find("SkillZone/Skill1_Row/Skill1_DMG")?.GetComponent<TMP_Text>();
            if (skill2Name == null)
                skill2Name = transform.Find("SkillZone/Skill2_Row/Skill2_Name")?.GetComponent<TMP_Text>();
            if (skill2DMG == null)
                skill2DMG = transform.Find("SkillZone/Skill2_Row/Skill2_DMG")?.GetComponent<TMP_Text>();

            if (invisibleOverlay == null)
                invisibleOverlay = transform.Find("InvisibleOverlay")?.GetComponent<Image>();
            if (burnIcon == null)
                burnIcon = transform.Find("BurnIcon")?.GetComponent<Image>();
            burnIcon?.gameObject.SetActive(false);
            if (poisonIcon == null)
                poisonIcon = transform.Find("PoisonIcon")?.GetComponent<Image>();
            poisonIcon?.gameObject.SetActive(false);
            if (saignementIcon == null)
                saignementIcon = transform.Find("SaignementIcon")?.GetComponent<Image>();
            saignementIcon?.gameObject.SetActive(false);

            // Désactive les descriptions directement sur la carte — affichées dans le tooltip à la place
            transform.Find("SkillZone/Skill1_Row/Skill1_InfoCol/Skill1_Desc")?.gameObject.SetActive(false);
            transform.Find("SkillZone/Skill2_Row/Skill2_InfoCol/Skill2_Desc")?.gameObject.SetActive(false);
        }

        private void LateUpdate()
        {
            if (cardInstance == null || cardInstance.data == null) return;
            UpdateVisuals();
        }

        public void UpdateVisuals()
        {
            UpdateHP();
            UpdateCooldowns();
            UpdateStackIcon();
            UpdateOverlays();
            UpdateSkillIcons();
            UpdateSkillDisplay();
            UpdatePassiveIcon();
            UpdateBurnIcon();
            UpdatePoisonIcon();
            UpdateSaignementIcon();
            UpdateAstralArrow();
        }

        private void UpdatePassiveIcon()
        {
            if (passiveIcon == null || cardInstance?.data == null) return;

            bool hasPassive = cardInstance.data.passive != null
                           && !string.IsNullOrEmpty(cardInstance.data.passive.passiveDescription);
            passiveIcon.gameObject.SetActive(hasPassive);
        }

        private void UpdateBurnIcon()
        {
            if (burnIcon == null || cardInstance == null) return;

            bool isBurning = cardInstance.activeEffects.Any(e => e.type == EffectType.Burn);
            if (isBurning && burnIcon.sprite == null && IconLibrary.Instance != null)
                burnIcon.sprite = IconLibrary.Instance.iconBurn;
            burnIcon.gameObject.SetActive(isBurning && burnIcon.sprite != null);
        }

        private void UpdatePoisonIcon()
        {
            if (poisonIcon == null || cardInstance == null) return;

            bool isPoisoned = cardInstance.activeEffects.Any(e => e.type == EffectType.Poison);
            if (isPoisoned && poisonIcon.sprite == null && IconLibrary.Instance != null)
                poisonIcon.sprite = IconLibrary.Instance.iconPoison;
            poisonIcon.gameObject.SetActive(isPoisoned && poisonIcon.sprite != null);
        }

        private void UpdateSaignementIcon()
        {
            if (saignementIcon == null || cardInstance == null) return;

            bool isBleeding = cardInstance.activeEffects.Any(e => e.type == EffectType.Saignement);
            if (isBleeding && saignementIcon.sprite == null && IconLibrary.Instance != null)
                saignementIcon.sprite = IconLibrary.Instance.iconSaignement;
            saignementIcon.gameObject.SetActive(isBleeding && saignementIcon.sprite != null);
        }

        public void TriggerHitShake()
        {
            if (gameObject.activeInHierarchy)
                StartCoroutine(HitShakeCoroutine());
        }

        private IEnumerator HitShakeCoroutine()
        {
            var rt = GetComponent<RectTransform>();
            if (rt == null) yield break;

            float duration = 0.25f;
            float magnitude = 6f;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                float dampened = magnitude * (1f - elapsed / duration);
                rt.localPosition = new Vector3(
                    Random.Range(-dampened, dampened),
                    Random.Range(-dampened, dampened), 0f);
                elapsed += Time.deltaTime;
                yield return null;
            }

            rt.localPosition = Vector3.zero;
        }

        private void UpdateSkillDisplay()
        {
            if (cardInstance?.data == null) return;
            SetSkillDisplay(cardInstance.data.skillOne,
                            skill1TypeIcon, skill1Name,
                            cardInstance.skill1Cooldown);
            SetSkillDisplay(cardInstance.data.skillTwo,
                            skill2TypeIcon, skill2Name,
                            cardInstance.skill2Cooldown);
        }

        private void SetSkillDisplay(CardSkill skill, Image typeIcon, TMP_Text nameText, int cooldown)
        {
            if (skill == null) return;

            if (nameText != null)
            {
                nameText.text = skill.skillName;
                nameText.fontSize = 12f;
                nameText.alignment = TMPro.TextAlignmentOptions.Center;
                nameText.color = cooldown > 0
                    ? new Color(0.5f, 0.5f, 0.5f)
                    : Color.white;
            }

            if (typeIcon != null)
            {
                typeIcon.sprite = GetSkillTypeSprite(skill.skillType);
                typeIcon.color = cooldown > 0
                    ? new Color(0.5f, 0.5f, 0.5f, 0.6f)
                    : GetSkillTypeColor(skill.skillType);
            }
        }

        private Sprite GetSkillTypeSprite(SkillType type)
        {
            var lib = IconLibrary.Instance;
            if (lib == null) return null;
            return type switch
            {
                SkillType.Attack => lib.iconAttack,
                SkillType.Heal => lib.iconHeal,
                SkillType.Buff => lib.iconBuff,
                SkillType.Debuff => lib.iconDeath,
                SkillType.Mixed => lib.iconAttack,
                _ => lib.iconAttack
            };
        }

        private Color GetSkillTypeColor(SkillType type)
        {
            return type switch
            {
                SkillType.Attack => new Color(0.86f, 0.31f, 0.31f), // rouge
                SkillType.Heal => new Color(0.39f, 0.86f, 0.59f), // vert
                SkillType.Buff => new Color(1f, 0.85f, 0.31f), // or
                SkillType.Debuff => new Color(0.6f, 0.2f, 0.8f),  // violet
                SkillType.Mixed => new Color(1f, 0.6f, 0.2f),  // orange
                _ => Color.white
            };
        }

        private void UpdateSkillIcons()
        {
            if (cardInstance?.data == null) return;

            float healPercent = 0f;
            int armorGain = 0;

            // Cherche dans les deux compétences
            foreach (var skill in new[] { cardInstance.data.skillOne, cardInstance.data.skillTwo })
            {
                if (skill == null) continue;

                // Détecte un soin
                float h = skill.GetImmediateHealPercent();
                if (h > healPercent) healPercent = h;

                // Détecte un gain d'armure
                foreach (var eff in skill.effects)
                {
                    if (eff.type == EffectType.GiveArmor)
                        armorGain = Mathf.Max(armorGain, Mathf.RoundToInt(eff.value));
                }
            }

            // Groupe soin
            if (healGroup != null)
            {
                bool showHeal = healPercent > 0f;
                healGroup.SetActive(showHeal);
                if (showHeal && healValue != null)
                {
                    int healAmt = Mathf.RoundToInt(cardInstance.data.maxHP * healPercent);
                    healValue.text = $"+{healAmt}";
                }
            }

            // Groupe armure
            if (armorGroup != null)
            {
                bool showArmor = armorGain > 0;
                armorGroup.SetActive(showArmor);
                if (showArmor && armorValue != null)
                    armorValue.text = $"+{armorGain}";
            }
        }

        private void UpdateHP()
        {
            if (hpCurrent != null)
                hpCurrent.text = cardInstance.currentHP.ToString();

            if (hpMax != null)
                hpMax.text = cardInstance.data.maxHP.ToString();

            if (hpCurrent != null)
                hpCurrent.color = Color.white;

            // Armure — pool HP secondaire
            if (armorText != null)
            {
                if (cardInstance.currentArmor > 0)
                {
                    armorText.text = cardInstance.currentArmor.ToString();
                    armorText.color = new Color(0.6f, 0.85f, 1f);
                }
                else
                {
                    armorText.text = "";
                }
            }
        }

        private void UpdateCooldowns()
        {
            bool cd1 = cardInstance.skill1Cooldown > 0;
            if (skill1CDIndicator != null) skill1CDIndicator.SetActive(cd1);
            if (skill1CDText != null && cd1) skill1CDText.text = cardInstance.skill1Cooldown.ToString();

            bool cd2 = cardInstance.skill2Cooldown > 0;
            if (skill2CDIndicator != null) skill2CDIndicator.SetActive(cd2);
            if (skill2CDText != null && cd2) skill2CDText.text = cardInstance.skill2Cooldown.ToString();
        }

        private void UpdateStackIcon()
        {
            if (stateIcon == null) return;

            // Affiche l'élément de la carte + stacks actifs
            bool hasStacks = StackManager.Instance != null &&
                             StackManager.Instance.GetStacks(
                                 cardInstance.ownerPlayerID,
                                 cardInstance.data.element) > 0;

            stateIcon.SetActive(hasStacks);

            if (hasStacks && ElementIconDatabase.Instance != null)
            {
                var img = stateIcon.GetComponent<Image>();
                var sprite = ElementIconDatabase.Instance.GetIcon(cardInstance.data.element);
                if (img != null)
                {
                    img.sprite = sprite;
                    img.color = sprite != null ? Color.white
                                               : GetElementColor(cardInstance.data.element);
                }

                float pulse = 1f + Mathf.Sin(Time.time * 3f) * 0.06f;
                stateIcon.transform.localScale = Vector3.one * pulse;
            }
        }

        private void UpdateOverlays()
        {
            if (exhaustedOverlay != null)
                exhaustedOverlay.SetActive(
                    (cardInstance.hasActedThisTurn ||
                     cardInstance.activeEffects.Any(e => e.type == EffectType.Stun))
                    && cardInstance.IsAlive);
            if (destroyedOverlay != null)
                destroyedOverlay.SetActive(!cardInstance.IsAlive);

            // Invisible : overlay bleu semi-transparent
            bool isInvis = cardInstance.IsInvisible && cardInstance.IsAlive;
            if (invisibleOverlay != null)
                invisibleOverlay.gameObject.SetActive(isInvis);
            if (canvasGroup != null)
                canvasGroup.alpha = 1f;
        }


        private Color GetElementColor(Element element)
        {
            return element switch
            {
                Element.Feu => new Color(1f, 0.4f, 0.1f),
                Element.Eau => new Color(0.2f, 0.6f, 1f),
                Element.Terre => new Color(0.4f, 0.7f, 0.2f),
                Element.Air => new Color(0.6f, 0.9f, 1f),
                Element.Lumiere => new Color(1f, 0.95f, 0.4f),
                Element.Tenebres => new Color(0.6f, 0.2f, 0.8f),
                Element.Astral => new Color(0.5f, 0.7f, 1f),
                _ => Color.white
            };
        }

        private void UpdateAstralArrow()
        {
            if (astralArrow == null || cardInstance?.data == null) return;

            if (cardInstance.data.element != Element.Astral)
            {
                astralArrow.SetActive(false);
                return;
            }

            var leftCard = BoardManager.Instance?.GetCardToTheLeft(cardInstance);
            bool hasTarget = leftCard != null;

            astralArrow.SetActive(hasTarget);

            if (!hasTarget) return;

            var rt = astralArrow.GetComponent<RectTransform>();
            if (rt == null) return;

            // La carte copiée est toujours à slotIndex - 1 (gauche) → flèche à gauche pour tous
            rt.anchorMin = new Vector2(0f, 0.5f);
            rt.anchorMax = new Vector2(0f, 0.5f);
            rt.anchoredPosition = new Vector2(-20f, 0f);
            astralArrow.transform.localEulerAngles = new Vector3(0f, 0f, 180f);
        }
    }
}