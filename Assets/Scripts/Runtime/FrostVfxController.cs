using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Wavekeep.Runtime
{
    /// <summary>
    /// Task 44: drives the Frost status VFX (Slow vs. Freeze) on a single enemy GameObject. Builds a thin
    /// "overlay" duplicate of the enemy's mesh(es) using the hand-written <c>Wavekeep/FrostOverlay</c> URP
    /// shader, then feeds frost intensity/mode/pulse into it.
    ///
    /// Per-enemy independence (a Task 44 reviewer-blocking concern): every controller owns its OWN
    /// <see cref="MaterialPropertyBlock"/> but all overlays share a single <see cref="Material"/> instance,
    /// so each enemy shows its own frost state while still batching — there is NO global shader parameter
    /// that would affect all enemies at once.
    ///
    /// Pooling (CLAUDE.md §3.5): the overlay is built once per pooled GameObject and reset to a clean state
    /// via <see cref="ResetImmediate"/> on every reuse, so a previously-frozen enemy never spawns back in
    /// already showing frost. Driven by <see cref="EnemyRuntime"/>'s centralised tick (CLAUDE.md §3.4) — this
    /// component has no <c>Update</c> of its own.
    /// </summary>
    public sealed class FrostVfxController : MonoBehaviour
    {
        /// <summary>Discrete visual tiers, mapped from <see cref="EnemyRuntime"/>'s status state.</summary>
        public enum FrostTier { None, Slow, Freeze }

        // Loaded from Resources so the shader/material are guaranteed in the build without per-scene wiring.
        private const string ShaderResourceName = "FrostOverlay";

        // Fade/transition tuning (units/sec on the 0..1 ranges). Fade-in is snappier than fade-out so the
        // effect appears promptly but clears with a brief, readable fade rather than an instant cut (Task 44).
        private const float FadeInSpeed = 7f;
        private const float FadeOutSpeed = 3f;
        private const float ModeLerpSpeed = 12f;
        private const float PulseDecaySpeed = 3.2f;

        // One shared material across every enemy (per-instance values come from each controller's MPB).
        private static Material _sharedMaterial;
        private static bool _materialLoadAttempted;
        private static int _idAmount, _idMode, _idPulse;

        private const string OverlayName = "FrostOverlay";

        private Renderer[] _overlays; // Task 60: Renderer (not MeshRenderer) so skinned overlays are supported too
        private MaterialPropertyBlock _mpb;
        private bool _initialized;

        private float _displayedAmount;
        private float _displayedMode;
        private float _pulse;
        private float _targetAmount;
        private float _targetMode;
        private bool _targetIsFreeze;

        /// <summary>
        /// Build the overlay mesh shell once for this (pooled) GameObject. Idempotent — safe to call on every
        /// spawn; the heavy work only runs the first time. No-op (other than the guard) if the frost material
        /// failed to load, so enemies still spawn/behave normally without the VFX.
        /// </summary>
        public void EnsureInitialized()
        {
            if (_initialized) return;
            _initialized = true;

            EnsureSharedMaterial();
            _mpb = new MaterialPropertyBlock();
            if (_sharedMaterial == null) return;

            var overlays = new List<Renderer>();

            // Task 60: the Synty enemy models (Skeleton/EvilGod) are SKINNED meshes — the old capsule was a
            // static MeshFilter, which is why only-handling MeshFilter made the effect vanish. Duplicate each
            // skinned renderer as an overlay that shares the SAME mesh + bones + rootBone, so it deforms with the
            // animation. Covers ALL submeshes (one frost material per slot) so multi-material parts are fully tinted.
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
            go.name == OverlayName || go.name == "FireOverlay";

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
            r.enabled = false; // hidden until there's visible frost (avoids skinning a hidden overlay every frame)
        }

        /// <summary>Snap all frost VFX state to "clean" with no fade. Called on pooled reuse so a freshly
        /// spawned enemy never inherits a previous life's frost (Task 44 acceptance criterion).</summary>
        public void ResetImmediate()
        {
            _displayedAmount = 0f;
            _displayedMode = 0f;
            _pulse = 0f;
            _targetAmount = 0f;
            _targetMode = 0f;
            _targetIsFreeze = false;
            Push();
        }

        /// <summary>Set the desired visual tier for this frame (the displayed values then ease toward it in
        /// <see cref="TickVisual"/>). Entering the Freeze tier from a non-frozen state fires the one-shot snap
        /// pulse, giving the player an unmistakable "moment of freeze".</summary>
        public void SetTarget(FrostTier tier, float intensity)
        {
            bool freeze = tier == FrostTier.Freeze;
            if (freeze && !_targetIsFreeze)
                _pulse = 1f; // snap flash + scale punch on the transition INTO freeze
            _targetIsFreeze = freeze;

            switch (tier)
            {
                case FrostTier.Freeze:
                    _targetAmount = 1f;
                    _targetMode = 1f;
                    break;
                case FrostTier.Slow:
                    _targetAmount = Mathf.Clamp01(intensity);
                    _targetMode = 0f;
                    break;
                default: // None — fade amount out; leave mode to ease back so a freeze→clear fade reads well.
                    _targetAmount = 0f;
                    break;
            }
        }

        /// <summary>Advance the eased fade/transition by <paramref name="deltaTime"/> and push to the overlay.
        /// Called from <see cref="EnemyRuntime"/>'s tick (no per-component Update — CLAUDE.md §3.4).</summary>
        public void TickVisual(float deltaTime)
        {
            if (!_initialized || _overlays == null || _overlays.Length == 0) return;

            float amountSpeed = _targetAmount > _displayedAmount ? FadeInSpeed : FadeOutSpeed;
            _displayedAmount = Mathf.MoveTowards(_displayedAmount, _targetAmount, amountSpeed * deltaTime);
            _displayedMode = Mathf.MoveTowards(_displayedMode, _targetMode, ModeLerpSpeed * deltaTime);
            if (_pulse > 0f)
                _pulse = Mathf.MoveTowards(_pulse, 0f, PulseDecaySpeed * deltaTime);

            Push();
        }

        private void Push()
        {
            if (_overlays == null || _mpb == null) return;

            _mpb.SetFloat(_idAmount, _displayedAmount);
            _mpb.SetFloat(_idMode, _displayedMode);
            _mpb.SetFloat(_idPulse, _pulse);

            // Only render/skin the overlay while there's something to show — keeps idle (un-frosted) enemies as
            // cheap as before instead of skinning a permanently-invisible duplicate every frame.
            bool visible = _displayedAmount > 0.001f || _pulse > 0.001f;
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
                Debug.LogWarning($"[FrostVfxController] Frost shader '{ShaderResourceName}' not found in Resources; frost VFX disabled.");
                return;
            }

            if (!shader.isSupported)
            {
                Debug.LogError($"[FrostVfxController] Frost shader '{ShaderResourceName}' loaded but is NOT supported (compile error); frost VFX will render incorrectly.");
            }
            else
            {
                Debug.Log($"[FrostVfxController] Frost shader '{ShaderResourceName}' loaded OK — frost VFX active.");
            }

            _sharedMaterial = new Material(shader) { name = "FrostOverlay (shared)" };
            _idAmount = Shader.PropertyToID("_FrostAmount");
            _idMode = Shader.PropertyToID("_FrostMode");
            _idPulse = Shader.PropertyToID("_FrostPulse");
        }
    }
}
