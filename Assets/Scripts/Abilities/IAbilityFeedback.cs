using UnityEngine;

namespace Wavekeep.Abilities
{
    /// <summary>
    /// Task 46: describes the intensity of a single lightning strike so the presenter can style it (a more
    /// intense flash, a colour shift, a heavier bolt) without the logic layer knowing any presentation.
    /// Combinable: e.g. an ultimate hit that also crits is <c>Ultimate | Crit</c>. Each flag corresponds to a
    /// state the runtime ALREADY resolves from current line data, so visuals can never disagree with mechanics.
    /// </summary>
    [System.Flags]
    public enum LightningStrikeFlags
    {
        None = 0,
        Ultimate = 1 << 0, // heavier base bolt + flash (single-target nuke vs. basic)
        Crit = 1 << 1,     // shared crit-flash treatment (Task 23 crit roll)
        Spike = 1 << 2,    // Overcharge bonus spike — the most intense variant
        Execute = 1 << 3   // Execute proc — distinct colour shift on the triggering hit
    }

    /// <summary>
    /// Sink for ability-execution visual feedback (Task 08 Part A). The pure-logic
    /// <c>AbilityRuntime</c> calls this at the EXACT point it resolves a target / radius and applies
    /// damage, passing the SAME values used for the real hit — so the visuals can never diverge from
    /// the actual targeting (a reviewer-blocking requirement). A MonoBehaviour presenter implements it;
    /// <c>AbilityRuntime</c> stays Unity-free and testable. A null feedback is a valid no-op (tests /
    /// headless).
    /// </summary>
    /// <summary>
    /// Task 47: the high-impact visual an apex/combo apex uses. The runtime derives this from each apex's
    /// existing data (palette from <c>AbilityVfxStyle</c>, shape from targeting + finisher flags) — no ability
    /// identity is hardcoded. The presenter renders each one larger/brighter than the Task 45/46 baseline and
    /// layers the shared screen-shake + flash "weight" treatment.
    /// </summary>
    public enum ApexVfxStyle
    {
        FrostNova,        // Remorseless Winter — a large, elaborate ice burst on the frozen target
        FrostShockwave,   // Permafrost Eruption — an expanding ring filling the ACTUAL AoE radius
        LightningStorm,   // Thunderstorm — a flurry of converging bolts
        LightningExecute  // Lethal Surge — a heavy single execution flash
    }

    public interface IAbilityFeedback
    {
        /// <summary>A single-target hit: a beam from <paramref name="from"/> (caster) to the resolved
        /// target at <paramref name="to"/>.</summary>
        void OnSingleTargetHit(Vector3 from, Vector3 to);

        /// <summary>An AoE hit centered at <paramref name="center"/> (caster) using the ACTUAL
        /// <paramref name="radius"/> that was tested against enemies.</summary>
        void OnAreaOfEffect(Vector3 center, float radius);

        // --- Task 45: Frost Warden ability VFX. Called at the SAME resolution points as the gameplay,
        // always passing the ACTUAL resolved values (blast radius scaled by Wider Burst, Frozen Ground's
        // current radius/duration, the zone's real band + pulse cadence) so visuals never diverge from
        // mechanics. Implementations that don't render frost simply no-op these. ---

        /// <summary>A ranged impact effect: a projectile travelling <paramref name="from"/> → <paramref
        /// name="to"/>, then a crystallization burst at the impact scaled to <paramref name="burstRadius"/>
        /// (the ability's actual current AoE/blast radius).</summary>
        void OnRangedImpactBurst(Vector3 from, Vector3 to, float burstRadius);

        /// <summary>A persistent ground decal (Frozen Ground) at <paramref name="center"/> of
        /// <paramref name="radius"/>, self-fading over <paramref name="duration"/> seconds — visually
        /// distinct from the one-shot impact burst.</summary>
        void OnGroundPatch(Vector3 center, float radius, float duration);

        /// <summary>Begin the persistent Frost Zone visual over the full-width band on Z in
        /// [<paramref name="bandMinZ"/>, <paramref name="bandMaxZ"/>], returning a handle the zone drives
        /// (pulse flashes + teardown). Returns null when no frost presenter is present.</summary>
        IZoneVisual BeginZone(float bandMinZ, float bandMaxZ);

        // --- Task 46: Bolt Striker electrical VFX. Driven from the SAME single-target execution points as
        // the gameplay; the flags/positions/durations are the ACTUAL resolved values (crit/spike/execute
        // state, chain-jump count, Piercing/Overload debuff durations). Non-lightning presenters no-op these. ---

        /// <summary>A fast gold lightning strike <paramref name="from"/> → <paramref name="to"/>, styled by
        /// <paramref name="flags"/>. Called once per ACTUAL strike, so Multi-Strike shows one flash per hit.</summary>
        void OnLightningStrike(Vector3 from, Vector3 to, LightningStrikeFlags flags);

        /// <summary>A Chain Lightning jump bolt from <paramref name="from"/> (the previous target) to a
        /// secondary target at <paramref name="to"/> — thinner/dimmer than the main strike. Called once per
        /// ACTUAL jump, so the rendered count matches the current Chain Lightning tier.</summary>
        void OnChainJump(Vector3 from, Vector3 to);

        /// <summary>Show the Piercing Bolt Armor-reduction debuff on <paramref name="target"/> for
        /// <paramref name="duration"/>s — gold jagged fracture lines (distinct from frost's blue Voronoi
        /// and from the Overload indicator).</summary>
        void OnArmorBreak(Transform target, float duration);

        /// <summary>Show the Overload generic-vulnerability debuff on <paramref name="target"/> for
        /// <paramref name="duration"/>s — a pulsing gold ring aura (distinct from Piercing Bolt's fracture
        /// lines, since it is a different mechanic).</summary>
        void OnVulnerability(Transform target, float duration);

        // --- Task 47: apex / combo apex high-impact VFX. Called once per apex cast from the SAME execution
        // points as the gameplay (role == Apex), and exactly on Lethal Surge's prime-consumption for the
        // combo — never on a separate timer. Carry the ACTUAL resolved centre/radius. ---

        /// <summary>An apex talent fired: render <paramref name="style"/> at <paramref name="center"/> sized to
        /// <paramref name="radius"/> (the real AoE radius for area apexes), clearly exceeding Basic/Ultimate
        /// intensity, and apply the shared screen-shake + flash weight treatment.</summary>
        void OnApexImpact(Vector3 center, float radius, ApexVfxStyle style);

        /// <summary>Frozen Lightning (combo apex) resolved on a primed target at <paramref name="center"/>:
        /// a frost-blue freeze moment immediately followed by a gold lightning strike (both palettes), with the
        /// strongest weight treatment. Fired from the exact prime-consumption point, not a timer.</summary>
        void OnComboFrozenLightning(Vector3 center);
    }
}
