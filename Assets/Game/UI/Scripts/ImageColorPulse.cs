using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace Astraleum.UI
{
    // Pulsation légère (respiration) de la couleur d'une Image UI — utilisé pour les décors
    // de plateau statiques (ex. SlotsBoard_Voragoth_P1/P2/P3) afin qu'ils ne soient pas figés
    // à l'écran. OnEnable/OnDisable (pas Start) car ces décors sont activés/désactivés via
    // BossPhaseController.SwapBoard() à chaque transition de phase.
    [RequireComponent(typeof(Image))]
    public class ImageColorPulse : MonoBehaviour
    {
        [Tooltip("Couleur atteinte au pic de la pulsation (l'autre extrémité est la couleur actuelle de l'Image au réveil).")]
        public Color pulseColor = new Color(1f, 0.35f, 0.2f, 1f);
        [Tooltip("Durée d'un aller simple (secondes) — l'aller-retour complet dure 2x cette valeur.")]
        public float pulseDuration = 1.8f;

        private Image image;
        private Color baseColor;
        private Tween tween;

        private void Awake() => image = GetComponent<Image>();

        private void OnEnable()
        {
            baseColor = image.color;
            tween?.Kill();
            tween = image.DOColor(pulseColor, pulseDuration)
                         .SetLoops(-1, LoopType.Yoyo)
                         .SetEase(Ease.InOutSine)
                         .SetUpdate(true);
        }

        private void OnDisable()
        {
            tween?.Kill();
            if (image != null) image.color = baseColor;
        }
    }
}
