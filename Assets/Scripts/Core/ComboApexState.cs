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
                if (combo.ConsumingApex == null || combo.ConsumingApex.Ability != ability) continue;
                if (!IsUnlocked(combo)) continue;
                multiplier = combo.ConsumeDamageMultiplier;
                return true;
            }
            return false;
        }

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
