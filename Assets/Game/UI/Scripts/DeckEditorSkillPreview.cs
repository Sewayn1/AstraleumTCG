using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using System.Collections;

namespace Astraleum.UI
{
    public class DeckEditorSkillPreview : MonoBehaviour, IPointerClickHandler
    {
        public static DeckEditorSkillPreview Instance;

        [Header("Card Info")]
        public Image    cardArtwork;
        public TMP_Text cardNameText;
        public TMP_Text elementText;

        [Header("Skill 1")]
        public GameObject skill1Block;
        public TMP_Text   skill1Name;
        public TMP_Text   skill1Damage;
        public TMP_Text   skill1Cooldown;
        public TMP_Text   skill1Desc;

        [Header("Skill 2")]
        public GameObject skill2Block;
        public TMP_Text   skill2Name;
        public TMP_Text   skill2Damage;
        public TMP_Text   skill2Cooldown;
        public TMP_Text   skill2Desc;

        [Header("Passif")]
        public GameObject passiveBlock;
        public TMP_Text   passiveNameText;
        public TMP_Text   passiveDescText;

        private void Awake()
        {
            Instance = this;
            gameObject.SetActive(false);
        }

        public void Show(CardData card)
        {
            if (card == null) return;

            int n = card.cardNumber;

            if (cardArtwork != null && card.artwork != null)
                cardArtwork.sprite = card.artwork;

            if (cardNameText != null)
                cardNameText.text = LocalizationManager.GetCard(n, "card_name", card.cardName);

            if (elementText != null)
                elementText.text = card.element.ToString();

            PopulateSkill(card.skillOne, n, "skill1",
                          skill1Block, skill1Name, skill1Damage, skill1Cooldown, skill1Desc);
            PopulateSkill(card.skillTwo, n, "skill2",
                          skill2Block, skill2Name, skill2Damage, skill2Cooldown, skill2Desc);

            string passiveDescRaw = card.passive != null
                ? LocalizationManager.GetCard(n, "passive_desc", card.passive.passiveDescription)
                : "";
            bool hasPassive = card.passive != null && !string.IsNullOrEmpty(passiveDescRaw);

            if (passiveBlock != null) passiveBlock.SetActive(hasPassive);

            if (hasPassive)
            {
                if (passiveNameText != null)
                {
                    passiveNameText.text  = LocalizationManager.GetCard(n, "passive_name", card.passive.passiveName);
                    passiveNameText.color = card.passive.passiveColor;
                }
                if (passiveDescText != null)
                    passiveDescText.text = passiveDescRaw;
            }

            gameObject.SetActive(true);
            // Force layout recalc after content is populated
            StartCoroutine(RebuildNextFrame());
        }

        private IEnumerator RebuildNextFrame()
        {
            yield return null;
            LayoutRebuilder.ForceRebuildLayoutImmediate(transform as RectTransform);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.pointerCurrentRaycast.gameObject == gameObject)
                gameObject.SetActive(false);
        }

        private void PopulateSkill(CardSkill skill, int cardNumber, string key,
                                    GameObject block, TMP_Text nameText,
                                    TMP_Text damageText, TMP_Text coolText,
                                    TMP_Text descText)
        {
            bool has = skill != null && !string.IsNullOrEmpty(skill.skillName);
            if (block != null) block.SetActive(has);
            if (!has) return;

            if (nameText != null)
                nameText.text = LocalizationManager.GetCard(cardNumber, key + "_name", skill.skillName);

            if (damageText != null)
            {
                bool showDmg = skill.damage > 0;
                damageText.gameObject.SetActive(showDmg);
                if (showDmg) damageText.text = skill.damage.ToString();
            }

            if (coolText != null)
            {
                bool hasCool = skill.cooldownTurns > 0;
                coolText.gameObject.SetActive(hasCool);
                if (hasCool)
                {
                    string plural = skill.cooldownTurns > 1 ? "s" : "";
                    coolText.text = LocalizationManager.Get("combat_label_cooldown", skill.cooldownTurns, plural);
                }
            }

            if (descText != null)
                descText.text = LocalizationManager.GetCard(cardNumber, key + "_desc", skill.description);
        }
    }
}
