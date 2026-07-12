using UnityEngine;
using TMPro;
using Astraleum;

namespace Astraleum.UI
{
    /// <summary>
    /// Panneau lecture seule affiché au clic droit sur une carte de collection.
    /// Références assignées dans l'inspecteur ou auto-détectées par nom.
    /// </summary>
    public class CardSkillPanelUI : MonoBehaviour
    {
        public static CardSkillPanelUI Instance;

        [Header("Skill 1")]
        public TMP_Text skill1Name;
        public TMP_Text skill1Desc;

        [Header("Skill 2")]
        public TMP_Text skill2Name;
        public TMP_Text skill2Desc;

        [Header("Passive")]
        public TMP_Text passiveName;
        public TMP_Text passiveDesc;

        private CardData _currentCard;

        private void Awake()
        {
            Instance = this;

            // Auto-détection par nom si non assigné en inspecteur
            if (skill1Name  == null) skill1Name  = FindLabel("Skill1_Name");
            if (skill1Desc  == null) skill1Desc  = FindLabel("Skill1_Desc");
            if (skill2Name  == null) skill2Name  = FindLabel("Skill2_Name");
            if (skill2Desc  == null) skill2Desc  = FindLabel("Skill2_Desc");
            if (passiveName == null) passiveName = FindLabel("PassiveName");
            if (passiveDesc == null) passiveDesc = FindLabel("PassiveDesc");

            gameObject.SetActive(false);
        }

        public void Show(CardData card, Vector2 screenPos)
        {
            if (card == null) return;

            // Toggle : reclique même carte → ferme
            if (_currentCard == card && gameObject.activeSelf)
            {
                Hide();
                return;
            }

            _currentCard = card;

            // Skill 1
            bool hasS1 = card.skillOne != null;
            if (skill1Name != null) skill1Name.text = hasS1 ? SkillNameWithCD(card.skillOne) : "";
            TMPIconReplacer.ApplyTo(skill1Desc, hasS1 ? card.skillOne.description : "");

            // Skill 2
            bool hasS2 = card.skillTwo != null && !string.IsNullOrEmpty(card.skillTwo.skillName);
            if (skill2Name != null) skill2Name.text = hasS2 ? SkillNameWithCD(card.skillTwo) : "";
            TMPIconReplacer.ApplyTo(skill2Desc, hasS2 ? card.skillTwo.description : "");

            // Passive
            bool hasP = card.passive != null;
            if (passiveName != null) passiveName.text = hasP ? card.passive.passiveName        : "Aucun Passif";
            TMPIconReplacer.ApplyTo(passiveDesc, hasP ? card.passive.passiveDescription : "Cette carte n'a aucun Passif.");

            gameObject.SetActive(true);
            PositionNearCursor(screenPos);
        }

        public void Hide()
        {
            _currentCard = null;
            gameObject.SetActive(false);
        }

        private void PositionNearCursor(Vector2 screenPos)
        {
            var rt = GetComponent<RectTransform>();
            // Coin supérieur-gauche comme point de référence
            rt.pivot = new Vector2(0f, 1f);

            // Utiliser le Canvas racine pour éviter les offsets du parent
            var canvas = GetComponentInParent<Canvas>()?.rootCanvas;
            if (canvas == null) return;

            Camera cam = canvas.renderMode == UnityEngine.RenderMode.ScreenSpaceOverlay
                ? null : canvas.worldCamera;
            var canvasRT = canvas.GetComponent<RectTransform>();

            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRT, screenPos, cam, out Vector2 cur);

            float w = rt.sizeDelta.x;
            float h = rt.sizeDelta.y;
            const float margin = 8f;

            // Gauche du curseur ; droite si trop près du bord gauche
            float x = screenPos.x - w - margin >= 0f
                ? cur.x - w - margin
                : cur.x + margin;

            // Top du panel aligné sur le curseur, clampé dans le canvas
            float y = Mathf.Clamp(cur.y, canvasRT.rect.yMin + h, canvasRT.rect.yMax);

            // Positionner en world-space (indépendant des anchors du parent)
            rt.position = canvasRT.TransformPoint(new Vector3(x, y, 0f));
        }

        private static string SkillNameWithCD(CardSkill skill)
            => skill.cooldownTurns > 0
                ? $"{skill.skillName}  <size=75%><color=#aaaaaa>CD {skill.cooldownTurns}</color></size>"
                : skill.skillName;

        private TMP_Text FindLabel(string goName)
        {
            var t = transform.Find(goName);
            return t != null ? t.GetComponent<TMP_Text>() : null;
        }
    }
}
