using System.Collections;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace Astraleum.UI
{
    // Éclair ambiant qui flashe par intermittence à une position aléatoire dans une zone donnée
    // (décor Phase 3 Voragoth, thème Vide/Univers). OnEnable/OnDisable (pas Start) car ces éclairs
    // sont enfants de SlotsBoard_Voragoth_P3, activé/désactivé via BossPhaseController.SwapBoard().
    [RequireComponent(typeof(RawImage))]
    public class LightningFlicker : MonoBehaviour
    {
        [Tooltip("Position (anchoredPosition) autour de laquelle les éclairs apparaissent.")]
        public Vector2 areaCenter = Vector2.zero;
        [Tooltip("Étendue de la zone d'apparition (largeur/hauteur) autour de areaCenter.")]
        public Vector2 areaSize = new Vector2(700f, 500f);

        [Tooltip("Pause minimale/maximale entre deux éclairs (secondes).")]
        public Vector2 pauseRange = new Vector2(1.5f, 4f);
        [Tooltip("Durée d'un flash individuel (fondu in puis out, secondes).")]
        public float flashInDuration = 0.06f;
        public float flashOutDuration = 0.25f;
        [Tooltip("Nombre de flashs successifs par éclair (scintillement), 1 à 3.")]
        public int flickerCount = 2;

        [Tooltip("Variation d'échelle aléatoire appliquée à chaque éclair.")]
        public Vector2 scaleRange = new Vector2(0.8f, 1.3f);
        [Tooltip("Rotation Z aléatoire max (degrés, +/-) appliquée à chaque éclair.")]
        public float rotationRange = 15f;

        private static readonly Rect[] Quadrants =
        {
            new Rect(0f,   0.5f, 0.5f, 0.5f),
            new Rect(0.5f, 0.5f, 0.5f, 0.5f),
            new Rect(0f,   0f,   0.5f, 0.5f),
            new Rect(0.5f, 0f,   0.5f, 0.5f),
        };

        private RectTransform rt;
        private RawImage image;
        private Color baseColor;
        private Coroutine loop;
        private Sequence flashSeq;

        private void Awake()
        {
            rt = (RectTransform)transform;
            image = GetComponent<RawImage>();
            baseColor = image.color;
        }

        private void OnEnable()
        {
            SetAlpha(0f);
            loop = StartCoroutine(FlickerLoop());
        }

        private void OnDisable()
        {
            if (loop != null) StopCoroutine(loop);
            flashSeq?.Kill();
            SetAlpha(0f);
        }

        private IEnumerator FlickerLoop()
        {
            while (true)
            {
                yield return new WaitForSeconds(Random.Range(pauseRange.x, pauseRange.y));

                // Repositionne/reforme l'éclair avant chaque flash — donne l'impression d'un
                // éclair différent à chaque fois plutôt qu'un seul élément qui clignote sur place.
                image.uvRect = Quadrants[Random.Range(0, Quadrants.Length)];
                rt.anchoredPosition = areaCenter + new Vector2(
                    Random.Range(-areaSize.x * 0.5f, areaSize.x * 0.5f),
                    Random.Range(-areaSize.y * 0.5f, areaSize.y * 0.5f));
                rt.localRotation = Quaternion.Euler(0f, 0f, Random.Range(-rotationRange, rotationRange));
                float scale = Random.Range(scaleRange.x, scaleRange.y);
                rt.localScale = new Vector3(scale, scale, 1f);

                flashSeq?.Kill();
                flashSeq = DOTween.Sequence().SetUpdate(true);
                for (int i = 0; i < Mathf.Max(1, flickerCount); i++)
                {
                    flashSeq.Append(image.DOFade(baseColor.a, flashInDuration).SetEase(Ease.OutQuad));
                    flashSeq.Append(image.DOFade(0f, flashOutDuration).SetEase(Ease.InQuad));
                }
                yield return flashSeq.WaitForCompletion();
            }
        }

        private void SetAlpha(float a)
        {
            var c = baseColor;
            c.a = a;
            if (image != null) image.color = c;
        }
    }
}
