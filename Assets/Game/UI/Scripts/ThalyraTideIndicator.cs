using TMPro;
using UnityEngine;

namespace Astraleum
{
    /// <summary>
    /// Petit indicateur de combat affichant l'état courant du Cycle des Marées de Thalyra
    /// (Haute/Basse) + le compte à rebours avant la prochaine bascule — le télégraphe visuel qui
    /// permet au joueur de lire le cycle et timer ses gros coups. Masqué par défaut, activé par
    /// ThalyraGameController après le spawn (même schéma que BossHealthBar).
    /// </summary>
    public class ThalyraTideIndicator : MonoBehaviour
    {
        public static ThalyraTideIndicator Instance;

        [Header("Racine — masquée hors combat Thalyra")]
        public GameObject root;

        [Header("Texte")]
        public TMP_Text stateText;
        public TMP_Text countdownText;

        [Header("Couleurs par état")]
        public Color hauteColor = new Color(0.35f, 0.65f, 1f);
        public Color basseColor = new Color(1f, 0.55f, 0.2f);

        private void Awake()
        {
            Instance = this;
            if (root != null) root.SetActive(false);
        }

        public void Bind()
        {
            if (root != null) root.SetActive(true);
            UpdateDisplay();
        }

        public void Hide()
        {
            if (root != null) root.SetActive(false);
        }

        private void Update()
        {
            if (ThalyraPhaseController.Instance == null) return;
            UpdateDisplay();
        }

        private void UpdateDisplay()
        {
            var phase = ThalyraPhaseController.Instance;
            if (phase == null) return;

            bool haute = phase.CurrentTideState == ThalyraTideState.Haute;

            if (stateText != null)
            {
                stateText.text = LocalizationManager.Get(haute ? "ui_tide_haute" : "ui_tide_basse");
                stateText.color = haute ? hauteColor : basseColor;
            }

            if (countdownText != null)
                countdownText.text = string.Format(LocalizationManager.Get("ui_tide_countdown"), phase.TurnsUntilTideChange);
        }
    }
}
