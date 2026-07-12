using DG.Tweening;
using UnityEngine;

namespace Astraleum.UI
{
    // Fait doucement dériver/tourner un fragment de roche flottant (décor Phase 3 Voragoth,
    // thème Vide/Univers) en boucle infinie. OnEnable/OnDisable (pas Start) car ces fragments
    // sont enfants de SlotsBoard_Voragoth_P3, activé/désactivé via BossPhaseController.SwapBoard().
    // Un délai initial aléatoire par instance évite que tous les fragments oscillent en phase.
    public class FloatingRockDrift : MonoBehaviour
    {
        [Tooltip("Amplitude du va-et-vient vertical (unités UI).")]
        public float driftAmplitude = 25f;
        [Tooltip("Durée d'un aller simple du va-et-vient vertical (secondes).")]
        public float driftDuration = 3f;
        [Tooltip("Angle de rotation atteint au pic de l'oscillation (degrés, +/-).")]
        public float rotationAmplitude = 6f;
        [Tooltip("Durée d'un aller simple de la rotation (secondes).")]
        public float rotationDuration = 4f;

        private RectTransform rt;
        private Vector2 basePos;
        private float baseRotZ;
        private Tween posTween;
        private Tween rotTween;

        private void Awake()
        {
            rt = (RectTransform)transform;
            basePos = rt.anchoredPosition;
            baseRotZ = rt.localEulerAngles.z;
        }

        private void OnEnable()
        {
            rt.anchoredPosition = basePos;
            rt.localRotation = Quaternion.Euler(0f, 0f, baseRotZ);

            posTween?.Kill();
            rotTween?.Kill();

            posTween = rt.DOAnchorPosY(basePos.y + driftAmplitude, driftDuration)
                         .SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo)
                         .SetDelay(Random.Range(0f, driftDuration)).SetUpdate(true);
            rotTween = rt.DOLocalRotate(new Vector3(0f, 0f, baseRotZ + rotationAmplitude), rotationDuration)
                         .SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo)
                         .SetDelay(Random.Range(0f, rotationDuration)).SetUpdate(true);
        }

        private void OnDisable()
        {
            posTween?.Kill();
            rotTween?.Kill();
            if (rt != null)
            {
                rt.anchoredPosition = basePos;
                rt.localRotation = Quaternion.Euler(0f, 0f, baseRotZ);
            }
        }
    }
}
