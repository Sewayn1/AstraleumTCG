using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Astraleum.UI
{
    /// <summary>
    /// Gère Panel_CardDetail dans Panel_MyCards.
    /// À attacher sur Panel_CardDetail.
    /// </summary>
    public class CardDetailPanel : MonoBehaviour
    {
        public static CardDetailPanel Instance;

        [Header("Artwork")]
        public Image artworkImage;

        [Header("Compétence 1")]
        public TMP_Text skill1Name;
        public TMP_Text skill1Damage;
        public TMP_Text skill1Cooldown;
        public TMP_Text skill1Description;
        public GameObject skill1Block; // parent à masquer si la compétence est absente

        [Header("Compétence 2")]
        public TMP_Text skill2Name;
        public TMP_Text skill2Damage;
        public TMP_Text skill2Cooldown;
        public TMP_Text skill2Description;
        public GameObject skill2Block;

        [Header("Passif")]
        public TMP_Text passiveName;
        public TMP_Text passiveDescription;
        public GameObject passiveBlock; // masqué si pas de passif

        [Header("Lore")]
        [Tooltip("RectTransform de la bulle entière — sa hauteur s'adapte au texte.")]
        public RectTransform loreBubble;
        public TMP_Text loreText;
        public TMP_Text loreQuote;

        private void Awake()
        {
            Instance = this;
            SetupLoreBubble();
            gameObject.SetActive(false);
        }

        private void SetupLoreBubble()
        {
            // ── LoreBubble : ContentSizeFitter vertical uniquement ────
            if (loreBubble != null)
            {
                var bubbleFitter = loreBubble.GetComponent<ContentSizeFitter>();
                if (bubbleFitter == null)
                    bubbleFitter = loreBubble.gameObject.AddComponent<ContentSizeFitter>();

                bubbleFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
                bubbleFitter.verticalFit   = ContentSizeFitter.FitMode.PreferredSize;
            }

            // ── LoreText : word wrap + hauteur adaptative ─────────────
            if (loreText != null)
            {
                loreText.textWrappingMode = TMPro.TextWrappingModes.Normal;
                loreText.overflowMode       = TextOverflowModes.Overflow;

                var textFitter = loreText.GetComponent<ContentSizeFitter>();
                if (textFitter == null)
                    textFitter = loreText.gameObject.AddComponent<ContentSizeFitter>();

                textFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
                textFitter.verticalFit   = ContentSizeFitter.FitMode.PreferredSize;
            }

            if (loreQuote != null)
            {
                loreQuote.textWrappingMode = TMPro.TextWrappingModes.Normal;
                loreQuote.overflowMode       = TextOverflowModes.Overflow;
            }
        }

        public void Show(CardData card)
        {
            if (card == null) return;

            int n = card.cardNumber;

            // ── Artwork ───────────────────────────────────────────────
            if (artworkImage != null)
                artworkImage.sprite = card.artwork;

            // ── Compétences ───────────────────────────────────────────
            PopulateSkill(card.skillOne, n, "skill1",
                          skill1Block, skill1Name, skill1Damage,
                          skill1Cooldown, skill1Description);

            PopulateSkill(card.skillTwo, n, "skill2",
                          skill2Block, skill2Name, skill2Damage,
                          skill2Cooldown, skill2Description);

            // ── Passif ────────────────────────────────────────────────
            string passiveDescRaw = LocalizationManager.GetCard(n, "passive_desc",
                                        card.passive?.passiveDescription ?? "");
            bool hasPassive = card.passive != null && !string.IsNullOrEmpty(passiveDescRaw);

            if (passiveBlock != null)
                passiveBlock.SetActive(hasPassive);

            if (hasPassive)
            {
                if (passiveName != null)
                {
                    passiveName.text  = LocalizationManager.GetCard(n, "passive_name",
                                            card.passive.passiveName);
                    passiveName.color = card.passive.passiveColor;
                }
                if (passiveDescription != null)
                    passiveDescription.text = passiveDescRaw;
            }

            // ── Lore ──────────────────────────────────────────────────
            if (loreText != null)
                loreText.text = LocalizationManager.GetCard(n, "lore", card.loreText);

            if (loreQuote != null)
            {
                string quoteRaw = LocalizationManager.GetCard(n, "quote", card.loreQuote);
                bool hasQuote = !string.IsNullOrEmpty(quoteRaw);
                loreQuote.gameObject.SetActive(hasQuote);
                if (hasQuote) loreQuote.text = LocalizationManager.Get("ui_lore_quote_fmt", quoteRaw);
            }

            // Recalcul immédiat de la taille de la bulle après écriture du texte
            if (loreBubble != null)
                LayoutRebuilder.ForceRebuildLayoutImmediate(loreBubble);

            gameObject.SetActive(true);
        }

        private void PopulateSkill(CardSkill skill, int cardNumber, string skillKey,
                                    GameObject block,
                                    TMP_Text nameText,
                                    TMP_Text damageText,
                                    TMP_Text cooldownText,
                                    TMP_Text descText)
        {
            bool hasSkill = skill != null && !string.IsNullOrEmpty(skill.skillName);

            if (block != null)
                block.SetActive(hasSkill);

            if (!hasSkill) return;

            if (nameText != null)
            {
                string locName = LocalizationManager.GetCard(cardNumber, $"{skillKey}_name", skill.skillName);
                if (skill.cooldownTurns > 0)
                {
                    string plural    = skill.cooldownTurns > 1 ? "s" : "";
                    string coolLabel = LocalizationManager.Get("combat_label_cooldown", skill.cooldownTurns, plural);
                    nameText.text = $"{locName}  —  {coolLabel}";
                }
                else
                    nameText.text = locName;
            }

            if (damageText != null)
            {
                damageText.gameObject.SetActive(skill.damage > 0);
                if (skill.damage > 0)
                    damageText.text = skill.damage.ToString();
            }

            // cooldownText reste optionnel — masqué si assigné, le titre suffit
            if (cooldownText != null)
                cooldownText.gameObject.SetActive(false);

            if (descText != null)
                descText.text = LocalizationManager.GetCard(cardNumber, $"{skillKey}_desc", skill.description);
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }
    }
}
