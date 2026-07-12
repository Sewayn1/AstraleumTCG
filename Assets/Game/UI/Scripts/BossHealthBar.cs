using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Astraleum
{
    /// <summary>
    /// Jauge de PV dédiée au Boss, avec marqueurs visuels aux seuils de transition de phase
    /// (66,67% / 33,33%). Masquée par défaut, activée par BossGameController après le spawn.
    /// </summary>
    public class BossHealthBar : MonoBehaviour
    {
        public static BossHealthBar Instance;

        [Header("Racine — masquée hors combat Boss")]
        public GameObject barRoot;

        [Header("Jauge")]
        [Tooltip("Image type=Filled (Horizontal, origine Left).")]
        public Image fillImage;
        public TMP_Text hpText;

        [Header("Marqueurs de palier (enfants de l'arrière-plan de la jauge, PAS du fill)")]
        public RectTransform phase2Marker;
        public RectTransform phase3Marker;

        [Header("Animation")]
        [Tooltip("Vitesse de déremplissage/remplissage de la jauge, en fraction de la barre par seconde (0.5 = la barre pleine se vide en 2s).")]
        public float fillSpeed = 0.5f;

        private CardInstance bossCard;
        private int totalMaxHP;
        private float displayedRatio = 1f;

        private void Awake()
        {
            Instance = this;
            if (barRoot != null) barRoot.SetActive(false);
        }

        /// <summary>Appelé une fois par BossGameController juste après le spawn du Boss.</summary>
        public void Bind(CardInstance boss, int maxHP)
        {
            bossCard = boss;
            totalMaxHP = maxHP;

            if (barRoot != null) barRoot.SetActive(true);

            SetMarkerX(phase2Marker, BossPhaseController.Instance != null ? BossPhaseController.Instance.phase2Threshold : 2f / 3f);
            SetMarkerX(phase3Marker, BossPhaseController.Instance != null ? BossPhaseController.Instance.phase3Threshold : 1f / 3f);

            // Pas d'animation au premier affichage — la jauge démarre directement au bon ratio.
            displayedRatio = totalMaxHP > 0 ? Mathf.Clamp01((float)bossCard.currentHP / totalMaxHP) : 1f;
            UpdateBar();
        }

        private void SetMarkerX(RectTransform marker, float t)
        {
            if (marker == null) return;
            marker.anchorMin = new Vector2(t, marker.anchorMin.y);
            marker.anchorMax = new Vector2(t, marker.anchorMax.y);
            marker.anchoredPosition = new Vector2(0f, marker.anchoredPosition.y);
        }

        private void Update()
        {
            if (bossCard == null) return;

            if (!bossCard.IsAlive)
            {
                if (barRoot != null) barRoot.SetActive(false);
                bossCard = null;
                return;
            }

            UpdateBar();
        }

        private void UpdateBar()
        {
            if (totalMaxHP <= 0) return;

            float targetRatio = Mathf.Clamp01((float)bossCard.currentHP / totalMaxHP);
            displayedRatio = Mathf.MoveTowards(displayedRatio, targetRatio, fillSpeed * Time.deltaTime);

            if (fillImage != null)
                fillImage.fillAmount = displayedRatio;

            if (hpText != null)
                hpText.text = $"{Mathf.Max(0, bossCard.currentHP)} / {totalMaxHP}";
        }
    }
}
