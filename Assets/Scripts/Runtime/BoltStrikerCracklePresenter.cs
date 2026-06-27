using System.Collections.Generic;
using UnityEngine;
using Wavekeep.Abilities;

namespace Wavekeep.Runtime
{
    /// <summary>
    /// Task 58 (revised) — drives Bolt Striker's electric-crackle overlay so it appears as a brief FLARE while
    /// the hero attacks, instead of covering the model permanently. An <see cref="IAbilityFeedback"/> sink (joins
    /// the hero's composite like the other VFX presenters): on each lightning strike it pushes the crackle
    /// shader's <c>_CrackleAmount</c> to its peak, then fades it back to 0 over a tunable time. Between attacks the
    /// amount sits at 0, so the model is clean.
    ///
    /// It drives the value through a <see cref="MaterialPropertyBlock"/> on whichever of the hero model's
    /// renderers carry the <c>Wavekeep/BoltStrikerCrackle</c> overlay material (added by the Task 58 setup), so it
    /// never mutates the shared material asset. Fully inert for any other hero — those models have no crackle
    /// overlay material (nothing to drive) and their abilities never call <see cref="OnLightningStrike"/>.
    /// </summary>
    [AddComponentMenu("Wavekeep/Runtime/Bolt Striker Crackle Presenter")]
    public sealed class BoltStrikerCracklePresenter : MonoBehaviour, IAbilityFeedback
    {
        private const string CrackleShaderName = "Wavekeep/BoltStrikerCrackle";
        private static readonly int CrackleAmountId = Shader.PropertyToID("_CrackleAmount");

        [Tooltip("Peak crackle visibility [0..1] reached the instant Bolt Striker strikes.")]
        [SerializeField, Range(0f, 1f)] private float _peakAmount = 1f;

        [Tooltip("Seconds for the crackle to fade from its peak back to invisible after a strike. Rapid attacks " +
                 "keep re-lighting it; this only governs the fade once attacks stop.")]
        [SerializeField, Min(0.05f)] private float _flareDecay = 0.45f;

        // The renderer + material-slot pairs carrying the crackle overlay material (the model's surfaces).
        private readonly List<(Renderer renderer, int materialIndex)> _targets =
            new List<(Renderer, int)>();
        private MaterialPropertyBlock _mpb;
        private float _amount;
        private bool _cached;

        private void Awake()
        {
            _mpb = new MaterialPropertyBlock();
            CacheTargets();
        }

        private void CacheTargets()
        {
            _cached = true;
            _targets.Clear();
            var renderers = GetComponentsInChildren<Renderer>(true);
            foreach (var r in renderers)
            {
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    if (mats[i] != null && mats[i].shader != null && mats[i].shader.name == CrackleShaderName)
                        _targets.Add((r, i));
                }
            }
            if (_targets.Count > 0) ApplyAmount(0f); // start fully off
        }

        private void Update()
        {
            if (_targets.Count == 0 || _amount <= 0f) return;
            _amount = Mathf.MoveTowards(_amount, 0f, (_peakAmount / _flareDecay) * Time.deltaTime);
            ApplyAmount(_amount);
        }

        private void ApplyAmount(float amount)
        {
            for (int i = 0; i < _targets.Count; i++)
            {
                var (r, index) = _targets[i];
                if (r == null) continue;
                r.GetPropertyBlock(_mpb, index);
                _mpb.SetFloat(CrackleAmountId, amount);
                r.SetPropertyBlock(_mpb, index);
            }
        }

        // --- IAbilityFeedback: the ONE call this presenter reacts to ----------------------------------------

        /// <summary>Bolt Striker threw a lightning bolt (its basic/ultimate strike) → flare the crackle.</summary>
        public void OnLightningStrike(Vector3 from, Vector3 to, LightningStrikeFlags flags)
        {
            if (!_cached) CacheTargets();
            if (_targets.Count == 0) return;
            _amount = _peakAmount;
            ApplyAmount(_amount);
        }

        // --- IAbilityFeedback: everything else is inert for this presenter ----------------------------------
        public void OnSingleTargetHit(Vector3 from, Vector3 to) { }
        public void OnAreaOfEffect(Vector3 center, float radius) { }
        public void OnRangedImpactBurst(Vector3 from, Vector3 to, float burstRadius) { }
        public void OnGroundPatch(Vector3 center, float radius, float duration) { }
        public IZoneVisual BeginZone(float bandMinZ, float bandMaxZ) => null;
        public void OnChainJump(Vector3 from, Vector3 to) { }
        public void OnArmorBreak(Transform target, float duration) { }
        public void OnVulnerability(Transform target, float duration) { }
        public void OnApexImpact(Vector3 center, float radius, ApexVfxStyle style) { }
        public void OnComboFrozenLightning(Vector3 center) { }
        public void OnFireballImpact(Vector3 from, Vector3 to, float burstRadius) { }
        public void OnCombustion(Vector3 center, float radius) { }
        public void OnSpreadingFlame(Vector3 from, Vector3 to) { }
        public IFireZoneVisual BeginFireWall(float bandMinZ, float bandMaxZ) => null;
        public void OnTracer(Vector3 from, Vector3 to, float intensity, bool sustained) { }
        public void OnPierceImpact(Vector3 point) { }
        public void OnArmorShred(Transform target, int stacks, int maxStacks, float duration) { }
        public void OnMinigunSpinUp(Vector3 at, float intensity) { }
    }
}
