using System.Collections.Generic;
using Wavekeep.Data;
using Wavekeep.Runtime;

namespace Wavekeep.Core
{
    /// <summary>
    /// Task 38: per-run resolver for cross-hero combo apexes (<see cref="ComboApexTalentDefinitionSO"/>),
    /// owned by <see cref="GameSession"/> (NOT a static singleton — CLAUDE.md §3.5). It holds the run's
    /// configured combo list plus the active-hero <see cref="HeroRegistry"/>, and answers the two questions
    /// the ability runtime asks per hit — entirely data-drively, never branching on a specific hero/ability:
    /// <list type="bullet">
    /// <item>"Is the ability I just hit with the PRIMER of an unlocked passive combo?" → prime the target.</item>
    /// <item>"Is it the CONSUMER of an unlocked passive combo?" → amplify if the target is primed.</item>
    /// </list>
    /// A combo counts as unlocked only when BOTH of its referenced single-hero apexes are currently unlocked
    /// across the active heroes — so a combo apex lights up exactly when the player has both heroes' required
    /// apex live in the same run (Task 38 unlock rule). Matching is by the apexes' <c>Ability</c> asset, the
    /// same <see cref="AbilityDefinitionSO"/> the apex's runtime instance wraps.
    /// </summary>
    public sealed class ComboApexState
    {
        private readonly IReadOnlyList<ComboApexTalentDefinitionSO> _combos;
        private readonly HeroRegistry _heroes;

        public ComboApexState(IReadOnlyList<ComboApexTalentDefinitionSO> combos, HeroRegistry heroes)
        {
            _combos = combos;
            _heroes = heroes;
        }

        /// <summary>The configured combos for this run (may be empty). The HUD iterates these and shows the
        /// ones currently <see cref="IsUnlocked"/>.</summary>
        public IReadOnlyList<ComboApexTalentDefinitionSO> Combos => _combos;

        /// <summary>True when BOTH of the combo's referenced apexes are currently unlocked across active heroes
        /// (Task 38). A combo with a missing reference never unlocks.</summary>
        public bool IsUnlocked(ComboApexTalentDefinitionSO combo)
        {
            if (combo == null || combo.PrimingApex == null || combo.ConsumingApex == null) return false;
            return AnyHeroHasApex(combo.PrimingApex) && AnyHeroHasApex(combo.ConsumingApex);
        }

        /// <summary>If <paramref name="ability"/> is the PRIMING apex's ability of an unlocked PASSIVE combo,
        /// returns true and the prime window (seconds) to mark the hit target with. Active combos are ignored
        /// (no priming model). Used by <c>AbilityRuntime</c> on the priming apex's hit.</summary>
        public bool TryGetPrimeWindow(AbilityDefinitionSO ability, out float windowSeconds)
        {
            windowSeconds = 0f;
            if (ability == null || _combos == null) return false;
            for (int i = 0; i < _combos.Count; i++)
            {
                var combo = _combos[i];
                if (combo == null || combo.TriggerType != ComboApexTriggerType.Passive) continue;
                // Task 50: only prime-based effects (Frozen Lightning's AmplifyConsume + Shatter's detonate)
                // mark targets; the other effect types are not prime/consume so they never prime.
                if (!IsPrimeEffect(combo.EffectType)) continue;
                if (combo.PrimingApex == null || combo.PrimingApex.Ability != ability) continue;
                if (!IsUnlocked(combo)) continue;
                windowSeconds = combo.PrimeWindowSeconds;
                return true;
            }
            return false;
        }

        /// <summary>If <paramref name="ability"/> is the CONSUMING apex's ability of an unlocked PASSIVE combo,
        /// returns true and the damage multiplier to apply against a primed target. Active combos are ignored.
        /// Used by <c>AbilityRuntime</c> on the consuming apex's hit.</summary>
        public bool TryGetConsumeMultiplier(AbilityDefinitionSO ability, out float multiplier)
        {
            multiplier = 1f;
            if (ability == null || _combos == null) return false;
            for (int i = 0; i < _combos.Count; i++)
            {
                var combo = _combos[i];
                if (combo == null || combo.TriggerType != ComboApexTriggerType.Passive) continue;
                // Task 50: only Frozen Lightning's amplify-on-consume model consumes via the consuming apex's
                // own hit. Shatter (also prime-based) detonates via TryGetShatterDetonation instead, so it is
                // excluded here — a Bullet Storm hit must NOT be treated as a plain ×multiplier consume.
                if (combo.EffectType != ComboEffectType.AmplifyConsume) continue;
                if (combo.ConsumingApex == null || combo.ConsumingApex.Ability != ability) continue;
                if (!IsUnlocked(combo)) continue;
                multiplier = combo.ConsumeDamageMultiplier;
                return true;
            }
            return false;
        }

        /// <summary>Task 50 (Shatter): if any unlocked combo detonates on a Physical hit to a primed target,
        /// return true with the detonation AoE radius + damage multiplier (×the triggering shot's damage). The
        /// caller checks the target is primed/Physical and consumes the prime — this only reports the params.</summary>
        public bool TryGetShatterDetonation(out float radius, out float multiplier)
        {
            radius = 0f;
            multiplier = 0f;
            if (_combos == null) return false;
            for (int i = 0; i < _combos.Count; i++)
            {
                var combo = _combos[i];
                if (combo == null || combo.EffectType != ComboEffectType.ShatterDetonate) continue;
                if (!IsUnlocked(combo)) continue;
                radius = combo.EffectRadius;
                multiplier = combo.ConsumeDamageMultiplier;
                return true;
            }
            return false;
        }

        /// <summary>Task 50 (Frostburn): the Burn-tick damage multiplier for a target currently under Frost CC,
        /// or 1 if no Frostburn combo is unlocked. Continuous (re-evaluated every tick by EnemyRuntime) — not a
        /// consumed prime. Returns the strongest unlocked multiplier.</summary>
        public float FrostburnBurnMultiplier()
        {
            float best = 1f;
            if (_combos == null) return best;
            for (int i = 0; i < _combos.Count; i++)
            {
                var combo = _combos[i];
                if (combo == null || combo.EffectType != ComboEffectType.FrostburnTick) continue;
                if (!IsUnlocked(combo)) continue;
                if (combo.ConsumeDamageMultiplier > best) best = combo.ConsumeDamageMultiplier;
            }
            return best;
        }

        /// <summary>Task 50 (Chain Combustion): if unlocked, return the seconds a Bolt Striker chain-jump adds to
        /// an already-Burning target's Burn (and the caller adds one Stacking-Embers stack). False if none.</summary>
        public bool TryGetChainCombustion(out float burnExtendSeconds)
        {
            burnExtendSeconds = 0f;
            if (_combos == null) return false;
            for (int i = 0; i < _combos.Count; i++)
            {
                var combo = _combos[i];
                if (combo == null || combo.EffectType != ComboEffectType.ChainCombustion) continue;
                if (!IsUnlocked(combo)) continue;
                burnExtendSeconds = combo.BurnExtendSeconds;
                return true;
            }
            return false;
        }

        /// <summary>Task 50 (Incendiary Rounds): if unlocked, return the BASE Burn (per-tick + duration) a
        /// Marksman pierce applies to each target beyond the first. The caller scales it by the held Smoldering
        /// Wound tier. False if no Incendiary Rounds combo is unlocked.</summary>
        public bool TryGetIncendiaryPierce(out float burnPerTick, out float burnDuration)
        {
            burnPerTick = 0f;
            burnDuration = 0f;
            if (_combos == null) return false;
            for (int i = 0; i < _combos.Count; i++)
            {
                var combo = _combos[i];
                if (combo == null || combo.EffectType != ComboEffectType.IncendiaryPierce) continue;
                if (!IsUnlocked(combo)) continue;
                burnPerTick = combo.IgniteBurnPerTick;
                burnDuration = combo.IgniteBurnDuration;
                return true;
            }
            return false;
        }

        // Task 50: prime-based effects mark/consume a per-enemy prime; the rest are passive rules with no prime.
        private static bool IsPrimeEffect(ComboEffectType effect) =>
            effect == ComboEffectType.AmplifyConsume || effect == ComboEffectType.ShatterDetonate;

        private bool AnyHeroHasApex(ApexTalentDefinitionSO apex)
        {
            var heroes = _heroes != null ? _heroes.Heroes : null;
            if (heroes == null) return false;
            for (int i = 0; i < heroes.Count; i++)
            {
                if (heroes[i] != null && heroes[i].IsApexUnlocked(apex)) return true;
            }
            return false;
        }
    }
}
