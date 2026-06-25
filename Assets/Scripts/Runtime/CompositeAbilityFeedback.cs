using UnityEngine;
using Wavekeep.Abilities;

namespace Wavekeep.Runtime
{
    /// <summary>
    /// Task 45: fans one ability-feedback call out to several <see cref="IAbilityFeedback"/> sinks, so the
    /// hero can run BOTH the generic diagnostic presenter (Task 08 beam/ring) and the frost VFX presenter
    /// (Task 45) without either knowing about the other. Each sink reacts only to the calls it implements
    /// (the others no-op), so the right visual fires per ability with no parallel trigger path.
    ///
    /// <see cref="BeginZone"/> returns the first non-null handle (the frost presenter's), since only one
    /// sink ever owns a persistent zone visual.
    /// </summary>
    public sealed class CompositeAbilityFeedback : IAbilityFeedback
    {
        private readonly IAbilityFeedback[] _sinks;

        public CompositeAbilityFeedback(params IAbilityFeedback[] sinks)
        {
            _sinks = sinks ?? System.Array.Empty<IAbilityFeedback>();
        }

        public void OnSingleTargetHit(Vector3 from, Vector3 to)
        {
            for (int i = 0; i < _sinks.Length; i++) _sinks[i]?.OnSingleTargetHit(from, to);
        }

        public void OnAreaOfEffect(Vector3 center, float radius)
        {
            for (int i = 0; i < _sinks.Length; i++) _sinks[i]?.OnAreaOfEffect(center, radius);
        }

        public void OnRangedImpactBurst(Vector3 from, Vector3 to, float burstRadius)
        {
            for (int i = 0; i < _sinks.Length; i++) _sinks[i]?.OnRangedImpactBurst(from, to, burstRadius);
        }

        public void OnGroundPatch(Vector3 center, float radius, float duration)
        {
            for (int i = 0; i < _sinks.Length; i++) _sinks[i]?.OnGroundPatch(center, radius, duration);
        }

        public IZoneVisual BeginZone(float bandMinZ, float bandMaxZ)
        {
            for (int i = 0; i < _sinks.Length; i++)
            {
                var handle = _sinks[i]?.BeginZone(bandMinZ, bandMaxZ);
                if (handle != null) return handle;
            }
            return null;
        }

        public void OnLightningStrike(Vector3 from, Vector3 to, LightningStrikeFlags flags)
        {
            for (int i = 0; i < _sinks.Length; i++) _sinks[i]?.OnLightningStrike(from, to, flags);
        }

        public void OnChainJump(Vector3 from, Vector3 to)
        {
            for (int i = 0; i < _sinks.Length; i++) _sinks[i]?.OnChainJump(from, to);
        }

        public void OnArmorBreak(Transform target, float duration)
        {
            for (int i = 0; i < _sinks.Length; i++) _sinks[i]?.OnArmorBreak(target, duration);
        }

        public void OnVulnerability(Transform target, float duration)
        {
            for (int i = 0; i < _sinks.Length; i++) _sinks[i]?.OnVulnerability(target, duration);
        }

        public void OnApexImpact(Vector3 center, float radius, ApexVfxStyle style)
        {
            for (int i = 0; i < _sinks.Length; i++) _sinks[i]?.OnApexImpact(center, radius, style);
        }

        public void OnComboFrozenLightning(Vector3 center)
        {
            for (int i = 0; i < _sinks.Length; i++) _sinks[i]?.OnComboFrozenLightning(center);
        }
    }
}
