using UnityEngine;

namespace Astraleum
{
    /// <summary>
    /// Composant à placer sur CardPrefab.
    /// Gère le spawn et la durée de vie des effets visuels liés aux compétences.
    /// Les VFX sont toujours instanciés hors de la hiérarchie Canvas pour éviter
    /// les conflits de rendu, avec un sorting order forcé au-dessus de l'UI.
    /// </summary>
    public class CardVFXHandler : MonoBehaviour
    {
        [Tooltip("Point d'ancrage pour les effets sur la carte (centre de l'artwork). " +
                 "Si non assigné, utilise le transform du GameObject.")]
        public Transform vfxAnchor;

        [Tooltip("Sorting layer utilisé pour les particules. Doit être au-dessus de l'UI. " +
                 "Laisser 'Default' si un seul layer existe.")]
        public string vfxSortingLayer = "VFX";

        [Tooltip("Order in Layer des particules. Valeur élevée = au-dessus de l'UI.")]
        public int vfxSortingOrder = 100;

        private Vector3 AnchorPos
        {
            get
            {
                if (vfxAnchor != null) return vfxAnchor.position;
                var rt = GetComponent<RectTransform>();
                if (rt != null) return rt.TransformPoint(rt.rect.center);
                return transform.position;
            }
        }

        /// <summary>
        /// Instancie le prefab VFX centré sur la carte, hors hiérarchie Canvas.
        /// Force le sorting pour apparaître au-dessus de l'UI.
        /// </summary>
        public GameObject SpawnVFX(GameObject prefab, float autoDestroyAfter = 3f)
        {
            if (prefab == null) return null;
            var go = Instantiate(prefab, AnchorPos, Quaternion.identity);
            ApplySorting(go);
            if (autoDestroyAfter > 0f)
                Destroy(go, autoDestroyAfter);
            return go;
        }

        /// <summary>
        /// Même chose que SpawnVFX — sans parent Canvas pour éviter les conflits de rendu.
        /// </summary>
        public GameObject SpawnVFXAttached(GameObject prefab, float autoDestroyAfter = 3f)
        {
            return SpawnVFX(prefab, autoDestroyAfter);
        }

        /// <summary>
        /// Force le sorting layer et l'order sur tous les ParticleSystemRenderer du prefab.
        /// </summary>
        private void ApplySorting(GameObject go)
        {
            foreach (var psr in go.GetComponentsInChildren<ParticleSystemRenderer>(true))
            {
                // Utilise le layer demandé s'il existe, sinon garde celui du prefab
                if (HasSortingLayer(vfxSortingLayer))
                    psr.sortingLayerName = vfxSortingLayer;
                psr.sortingOrder = vfxSortingOrder;
            }
        }

        private static bool HasSortingLayer(string layerName)
        {
            foreach (var layer in UnityEngine.SortingLayer.layers)
                if (layer.name == layerName) return true;
            return false;
        }
    }
}
