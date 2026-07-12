using System.Collections;
using UnityEngine;

namespace Astraleum
{
    public class CardVFXHandler : MonoBehaviour
    {
        [Tooltip("Ancrage optionnel (ex. centre artwork). x/y = ce transform, z toujours recalculé.")]
        public Transform vfxAnchor;

        [Tooltip("Order in Layer ajouté par-dessus l'order du Canvas racine.")]
        public int vfxSortingOrder = 100;

        private static int _vfxLayerIndex = -2;   // -2 = non initialisé

        private void Awake()
        {
            if (_vfxLayerIndex == -2)
                _vfxLayerIndex = LayerMask.NameToLayer("VFX");
        }

        // ── position monde ──────────────────────────────────────────────────────
        private Vector3 AnchorPos
        {
            get
            {
                Vector3 pos;
                if (vfxAnchor != null)
                    pos = vfxAnchor.position;
                else
                {
                    var rt = GetComponent<RectTransform>();
                    pos = rt != null ? rt.TransformPoint(rt.rect.center) : transform.position;
                }

                // Toujours 2 unités devant le plan Canvas
                var canvas = GetComponentInParent<Canvas>();
                var cam    = canvas?.worldCamera ?? Camera.main;
                if (cam != null)
                {
                    float planeDist = canvas != null ? canvas.planeDistance : 10f;
                    pos.z = cam.transform.position.z + planeDist - 2f;
                }
                return pos;
            }
        }

        public Vector3 GetAnchorPosition() => AnchorPos;

        // ── API publique ─────────────────────────────────────────────────────────
        public GameObject SpawnVFX(GameObject prefab, float autoDestroyAfter = 3f, Vector3 offset = default)
        {
            if (prefab == null) return null;
            Vector3 worldOffset = offset != Vector3.zero ? LocalToWorldOffset(offset) : Vector3.zero;
            var go = Instantiate(prefab, AnchorPos + worldOffset, Quaternion.identity);
            ApplySorting(go);
            PlayAllParticleSystems(go);

#if UNITY_EDITOR
            Debug.Log($"[VFX] '{prefab.name}' pos={go.transform.position} layer={go.layer}({LayerMask.LayerToName(go.layer)})");
#endif

            if (autoDestroyAfter > 0f)
                Destroy(go, autoDestroyAfter);
            return go;
        }

        public GameObject SpawnVFXAttached(GameObject prefab, float autoDestroyAfter = 3f)
            => SpawnVFX(prefab, autoDestroyAfter);

        /// <summary>
        /// Spawne un projectile à la position de cette carte et le fait voyager vers targetWorldPos
        /// sur la durée travelTime. La rotation est orientée vers la cible (axe Y local = direction de vol).
        /// Le caller est responsable de détruire le GameObject retourné.
        /// </summary>
        public GameObject SpawnProjectileVFX(GameObject prefab, Vector3 targetWorldPos, float travelTime, float scale = 1f)
        {
            if (prefab == null) return null;

            Vector3 startPos = AnchorPos;
            // La cible conserve le même Z que l'attaquant (même plan canvas)
            targetWorldPos.z = startPos.z;

            // Orientation : axe Y local pointe vers la cible dans le plan XY
            Vector3 dirXY = new Vector3(targetWorldPos.x - startPos.x, targetWorldPos.y - startPos.y, 0f);
            Quaternion rot = dirXY.sqrMagnitude > 0.001f
                ? Quaternion.LookRotation(Vector3.forward, dirXY.normalized)
                : Quaternion.identity;

            var go = Instantiate(prefab, startPos, rot);
            if (scale != 1f) go.transform.localScale = Vector3.one * scale;
            ApplySorting(go);
            PlayAllParticleSystems(go);

#if UNITY_EDITOR
            Debug.Log($"[VFX-Projectile] '{prefab.name}' {startPos} → {targetWorldPos} ({travelTime}s)");
#endif

            StartCoroutine(MoveProjectile(go, startPos, targetWorldPos, travelTime));
            return go;
        }

        private IEnumerator MoveProjectile(GameObject go, Vector3 start, Vector3 end, float duration)
        {
            if (duration <= 0f)
            {
                if (go != null) go.transform.position = end;
                yield break;
            }

            float elapsed = 0f;
            while (elapsed < duration)
            {
                if (go == null) yield break;
                go.transform.position = Vector3.Lerp(start, end, elapsed / duration);
                elapsed += Time.deltaTime;
                yield return null;
            }
            if (go != null)
                go.transform.position = end;
        }

        private Vector3 LocalToWorldOffset(Vector3 localOffset)
        {
            var rt = GetComponent<RectTransform>();
            return rt != null ? rt.TransformVector(localOffset) : localOffset;
        }

        private void PlayAllParticleSystems(GameObject go)
        {
            // root.Play(true) mirrors what playOnAwake=True does: registers the whole
            // PS hierarchy with the URP Render Graph in one shot. Calling Play(false)
            // on each child individually skips this registration and leaves them invisible.
            var rootPS = go.GetComponent<ParticleSystem>();
            if (rootPS != null)
                rootPS.Play(true);
            else
                foreach (var ps in go.GetComponentsInChildren<ParticleSystem>(true))
                    if (!ps.isPlaying) ps.Play(false);
        }

        // ── sorting + layer ──────────────────────────────────────────────────────
        private void ApplySorting(GameObject go)
        {
            var rootCanvas = GetComponentInParent<Canvas>()?.rootCanvas
                             ?? Object.FindFirstObjectByType<Canvas>();
            string layer = rootCanvas != null ? rootCanvas.sortingLayerName : "Default";
            int    order = (rootCanvas != null ? rootCanvas.sortingOrder : 0) + vfxSortingOrder;

            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                bool wasDisabled = !r.enabled;
                if (wasDisabled) r.enabled = true;
                r.sortingLayerName = layer;
                r.sortingOrder     = order;
                r.renderingLayerMask = 1;
                if (wasDisabled && r.sharedMaterial != null) r.enabled = false;

                // HorizontalBillboard lies flat in XZ plane — invisible from a front-facing
                // camera. Force Billboard so all particles face the camera.
                if (r is ParticleSystemRenderer psr &&
                    psr.renderMode == ParticleSystemRenderMode.HorizontalBillboard)
                    psr.renderMode = ParticleSystemRenderMode.Billboard;
            }

            if (_vfxLayerIndex >= 0)
                SetLayerRecursive(go, _vfxLayerIndex);
        }

        private static void SetLayerRecursive(GameObject obj, int layer)
        {
            obj.layer = layer;
            foreach (Transform child in obj.transform)
                SetLayerRecursive(child.gameObject, layer);
        }
    }
}
