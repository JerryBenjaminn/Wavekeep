# Task 20 — Fix Frost Bolt Burst Targeting (Ranged Impact-AoE, Not Caster-Centered AoE)

> Read `CLAUDE.md` in full, and review Task 04 (`AbilityRuntime`/`AbilityDefinitionSO`/targeting types) and Task 19 (Frost Warden kit) before starting. This task fixes a targeting bug: Frost Warden's basic ability currently behaves as an AoE centered on the caster (hitting everything in radius around Frost Warden itself), when it should behave as a ranged attack that travels to/targets the nearest enemy and explodes in a small AoE centered on that impact point, applying the stacking Frost/Slow effect to whatever's caught in that smaller blast.

## Goal

Frost Bolt Burst selects the nearest valid target within range (like a single-target ability would), resolves its effect as a small-radius AoE centered on that target's position (not the caster's position), applying damage + Frost-stack to everything within that small blast radius — visually/conceptually a bolt that travels out and explodes on impact.

## Scope

### 1. Diagnose Current Behavior
- Confirm exactly how `AbilityRuntime.Execute` currently resolves `AreaOfEffect` targeting for Frost Bolt Burst (per Task 04's original single-target/AoE split) — it's very likely currently treating "AoE" as "radius around the caster" rather than "radius around a resolved target/impact point," which is correct for something like the Task 19 ultimate (arena-wide from caster) but wrong for a ranged bolt.

### 2. Add a Third Targeting Mode (or Correct Parameterization)
- Rather than only `SingleTarget`/`AreaOfEffect`, introduce what Frost Bolt Burst actually needs: a **targeted-impact AoE** — resolve a target point (nearest enemy's position, same selection logic as `SingleTarget`) within `range`, then apply AoE damage/effects within a smaller `aoeRadius` centered on that point, not the caster.
- Decide the cleanest way to express this given the existing `AbilityDefinitionSO.targetingType` enum: either add a new enum value (e.g. `TargetedAreaOfEffect`) that uses both `range` (max cast distance to find a target) and a separate `aoeRadius` (blast size at impact), or reinterpret existing `AreaOfEffect` to always mean "AoE centered on a resolved target point" and introduce a distinct mode for the Task 19 ultimate's "AoE centered on caster, covers everything" case instead — choose whichever requires the smaller, cleaner change given how Task 19's ultimate already relies on the current behavior. Document your choice clearly, since this affects two existing abilities (Frost Bolt Burst and Frost Zone) that must end up on the *correct* respective modes.
- Critically: **Frost Zone (the ultimate) must keep its current caster-centered/arena-wide behavior** — only Frost Bolt Burst (the basic) changes. Whatever parameterization you choose must not regress the ultimate.

### 3. Update Frost Bolt Burst's Data
- Update Frost Warden's basic `AbilityDefinitionSO` to use the corrected targeting mode: `range` defines how far it can reach to find a target (reuse Task 18's already-corrected range value so it still hits enemies immediately on spawn), `aoeRadius` (likely a new, smaller value, e.g. 2–2.5m, since the original 3m was tuned for a caster-centered blast covering a wide area near the hero — document your chosen value and reasoning) defines the blast size at the impact point.
- Confirm the Frost-stack application (Task 19) still applies correctly to all enemies caught in the new impact-centered blast, not just the originally-resolved nearest target — i.e. if 3 enemies are clustered near the targeted enemy, all 3 should take damage and gain a Frost stack, not just the one initially selected as the aim point.

### 4. Re-verify Tag Interaction
- Confirm Frost Bolt Burst's existing `TagInteractionRule` (matching generic `AoE` tag → +15% radius, per Task 19) still applies correctly to the new `aoeRadius` field rather than silently doing nothing because it was wired to a field that no longer drives the actual blast size.

## Out of Scope (do not implement)
- Visual projectile travel animation (a bolt visibly flying from caster to target) — this task fixes the *mechanical* targeting/damage resolution; visual travel time/projectile motion can be a future polish item if wanted, but isn't required for correctness here
- Changes to the ultimate's behavior (must remain unchanged, see §2)
- Changes to any other hero's abilities

## Acceptance Criteria
- [ ] Frost Bolt Burst no longer applies damage/effects to everything around the caster regardless of distance to the actual targeted enemy
- [ ] Frost Bolt Burst correctly resolves a nearest-target impact point within `range`, then applies AoE damage/Frost-stack within `aoeRadius` centered on that point
- [ ] Multiple enemies clustered near the impact point all correctly receive damage + Frost stack, not just the originally-aimed-at enemy
- [ ] Frost Zone (ultimate) behavior is unchanged — still arena-wide/caster-centered as designed in Task 19
- [ ] The `AoE`-tag `TagInteractionRule` on Frost Bolt Burst correctly still affects the actual blast radius used at runtime
- [ ] No SO asset is mutated at runtime
- [ ] No static `Instance` patterns introduced
- [ ] Full playtest: Play as Frost Warden → enemies far from the hero but within ability range are unaffected by basic attack until they're actually targeted/in blast range of a resolved target → basic attack visibly only affects enemies near its actual resolved impact point, not a zone around the hero → ultimate still affects the whole arena as before

## Reviewer Notes
Flag as blocking if:
- The fix is a special-cased check specifically for Frost Warden's basic ability instead of a generally usable targeting mode/parameterization that any future ability could also use
- Frost Zone's arena-wide behavior regresses as a side effect of this fix
- The AoE tag-interaction rule becomes a no-op after this change
