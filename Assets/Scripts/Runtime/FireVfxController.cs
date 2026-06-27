using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Wavekeep.Runtime
{
    /// <summary>
    /// Task 51: drives the Burn status VFX on a single enemy GameObject — the fire counterpart to Task 44's
    /// <see cref="FrostVfxController"/>. Builds a thin inflated "overlay" duplicate of the enemy's mesh(es) using
    /// the hand-written <c>Wavekeep/FireOverlay</c> URP shader and feeds it a single per-enemy intensity.
    ///
    /// Per-enemy independence (the Task 44/51 reviewer-blocking concern): every controller owns its OWN
    /// <see cref="MaterialPropertyBlock"/> but all overlays share ONE <see cref="Material"/> instance, so each
    /// Burning enemy shows its own intensity (scaled by its current Burn stack count, fed from
    /// <see cref="EnemyRuntime"/>) while still batching — there is NO global shader parameter.
    ///
    /// Pooling (CLAUDE.md §3.5): the overlay is built once per pooled GameObject and reset to a clean state via
    /// <see cref="ResetImmediate"/> on every reuse, so a previously-Burning enemy never spawns back already on
    /// fire. Driven by <see cref="EnemyRuntime"/>'s centralised tick (CLAUDE.md §3.4) — no <c>Update</c> here.
    /// </summary>
    public sealed class FireVfxController : MonoBehaviour
    {
        private const string ShaderResourceName = "FireOverlay";

        // Fade tuning (units/sec on the 0..1 range). Fire ignites quickly and lingers briefly as it dies down.
        private const float FadeInSpeed = 8f;
        private const float FadeOutSpeed = 2.5f;

        private static Material _sharedMaterial;
        private static bool _materialLoadAttempted;
        private static int _idAmount;

        private const string OverlayName = "FireOverlay";

        private Renderer[] _overlays; // Task 60: Renderer (not MeshRenderer) so skinned overlays are supported too
        private MaterialPropertyBlock _mpb;
        private bool _initialized;

        private float _displayedAmount;
        private float _targetAmount;

        /// <summary>Build the overlay mesh shell once for this (pooled) GameObject. Idempotent — the heavy work
        /// only runs the first time; safe to call on every spawn.</summary>
        public void EnsureInitialized()
        {
            if (_initialized) return;
            _initialized = true;

            EnsureSharedMaterial();
            _mpb = new MaterialPropertyBlock();
            if (_sharedMaterial == null) return;

            var overlays = new List<Renderer>();

            // Task 60: same fix as FrostVfxController — the Synty enemy models are SKINNED meshes (no MeshFilter),
            // which is why only-handling MeshFilter made the Burn effect vanish on Skeleton/EvilGod. Duplicate each
            // skinned renderer as an overlay sharing the SAME mesh + bones + rootBone so it deforms with animation;
            // cover all submeshes so multi-material parts are fully tinted.
            var skinned = GetComponentsInChildren<SkinnedMeshRenderer>(true);
            foreach (var smr in skinned)
            {
                if (smr == null || smr.sharedMesh == null || IsOverlay(smr.gameObject)) continue;

                var go = NewOverlayObject(smr.transform);
                var overlay = go.AddComponent<SkinnedMeshRenderer>();
                overlay.sharedMesh = smr.sharedMesh;
                overlay.bones = smr.bones;
                overlay.rootBone = smr.rootBone;
                overlay.localBounds = smr.localBounds;
                overlay.updateWhenOffscreen = smr.updateWhenOffscreen;
                ConfigureOverlayRenderer(overlay, smr.sharedMesh.subMeshCount);
                overlays.Add(overlay);
            }

            // Static meshes (e.g. a separate weapon mesh, or the legacy capsule) → a static overlay duplicate.
            var filters = GetComponentsInChildren<MeshFilter>(true);
            foreach (var mf in filters)
            {
                if (mf == null || mf.sharedMesh == null || IsOverlay(mf.gameObject)) continue;

                var go = NewOverlayObject(mf.transform);
                go.AddComponent<MeshFilter>().sharedMesh = mf.sharedMesh;
                var mr = go.AddComponent<MeshRenderer>();
                ConfigureOverlayRenderer(mr, mf.sharedMesh.subMeshCount);
                overlays.Add(mr);
            }

            _overlays = overlays.ToArray();
        }

        // Skip the frost/fire overlays themselves so the two controllers never duplicate each other's overlays.
        private static bool IsOverlay(GameObject go) =>
            go.name == OverlayName || go.name == "FrostOverlay";

        private GameObject NewOverlayObject(Transform parent)
        {
            var go = new GameObject(OverlayName);
            var t = go.transform;
            t.SetParent(parent, false);
            t.localPosition = Vector3.zero;
            t.localRotation = Quaternion.identity;
            t.localScale = Vector3.one;
            go.layer = parent.gameObject.layer;
            return go;
        }

        private void ConfigureOverlayRenderer(Renderer r, int subMeshCount)
        {
            if (subMeshCount <= 1)
            {
                r.sharedMaterial = _sharedMaterial;
            }
            else
            {
                var mats = new Material[subMeshCount];
                for (int i = 0; i < subMeshCount; i++) mats[i] = _sharedMaterial;
                r.sharedMaterials = mats;
            }
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.lightProbeUsage = LightProbeUsage.Off;
            r.reflectionProbeUsage = ReflectionProbeUsage.Off;
            r.enabled = false; // hidden until there's visible Burn
        }

        /// <summary>Snap all Burn VFX state to "clean" with no fade. Called on pooled reuse so a freshly spawned
        /// enemy never inherits a previous life's fire (Task 51 acceptance criterion).</summary>
        public void ResetImmediate()
        {
            _displayedAmount = 0f;
            _targetAmount = 0f;
            Push();
        }

        /// <summary>Set this frame's desired Burn intensity in [0,1] (higher = more Burn stacks). The displayed
        /// value then eases toward it in <see cref="TickVisual"/>.</summary>
        public void SetTarget(float intensity)
        {
            _targetAmount = Mathf.Clamp01(intensity);
        }

        /// <summary>Advance the eased fade by <paramref name="deltaTime"/> and push to the overlay. Called from
        /// <see cref="EnemyRuntime"/>'s tick (no per-component Update — CLAUDE.md §3.4).</summary>
        public void TickVisual(float deltaTime)
        {
            if (!_initialized || _overlays == null || _overlays.Length == 0) return;

            float speed = _targetAmount > _displayedAmount ? FadeInSpeed : FadeOutSpeed;
            _displayedAmount = Mathf.MoveTowards(_displayedAmount, _targetAmount, speed * deltaTime);
            Push();
        }

        private void Push()
        {
            if (_overlays == null || _mpb == null) return;
            _mpb.SetFloat(_idAmount, _displayedAmount);

            // Only render/skin the overlay while there's visible Burn — keeps un-burning enemies cheap.
            bool visible = _displayedAmount > 0.001f;
            for (int i = 0; i < _overlays.Length; i++)
            {
                if (_overlays[i] == null) continue;
                _overlays[i].enabled = visible;
                if (visible) _overlays[i].SetPropertyBlock(_mpb);
            }
        }

        private static void EnsureSharedMaterial()
        {
            if (_materialLoadAttempted) return;
            _materialLoadAttempted = true;

            var shader = Resources.Load<Shader>(ShaderResourceName);
            if (shader == null)
            {
                Debug.LogWarning($"[FireVfxController] Fire shader '{ShaderResourceName}' not found in Resources; Burn VFX disabled.");
                return;
            }

            if (!shader.isSupported)
                Debug.LogError($"[FireVfxController] Fire shader '{ShaderResourceName}' loaded but is NOT supported (compile error); Burn VFX will render incorrectly.");
            else
                Debug.Log($"[FireVfxController] Fire shader '{ShaderResourceName}' loaded OK — Burn VFX active.");

            _sharedMaterial = new Material(shader) { name = "FireOverlay (shared)" };
            _idAmount = Shader.PropertyToID("_FireAmount");
        }
    }
}
