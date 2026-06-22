# Task 11 — Hero-Exclusive Upgrades & Status Effects

> Read `CLAUDE.md` in full (especially the updated §3.8), and review the Task 01–10 implementations before starting. This task adds a per-hero exclusive upgrade pool drawn alongside the existing generic pool, and adds a small fixed set of status effects (Freeze, Slow, Burn) that upgrades can apply to enemies on hit.

## Goal

By the end of this task, each `HeroDefinitionSO` has its own exclusive upgrade pool that the Task 07 card picker draws from alongside the generic pool, at least one hero-exclusive upgrade per existing hero demonstrates a status effect (e.g. a Frost-themed hero's exclusive upgrade applies Freeze on ultimate hit), and `EnemyRuntime` correctly handles Freeze/Slow/Burn as generic, stackable-or-not (decide and document) status effects rather than one-off booleans.

## Scope

### 1. `HeroDefinitionSO` — Exclusive Upgrade Pool
- Add a list field, e.g. `exclusiveUpgrades : List<UpgradeDefinitionSO>`, to `HeroDefinitionSO`.
- Author at least 2 hero-exclusive `UpgradeDefinitionSO` per existing hero (you have two heroes from Task 05 — so at least 4 new exclusive upgrades total), distinct from the generic pool.

### 2. `StatusEffectType` & Application Data
- Add `StatusEffectType` enum: `Freeze` (movement speed → 0, or near-0, for duration), `Slow` (movement speed reduced by a percentage for duration), `Burn` (damage over time for duration).
- Extend `UpgradeDefinitionSO` (or add a small companion struct referenced by it) with optional status-effect application data: `statusEffectType`, `magnitude` (e.g. slow %, burn damage/tick), `duration`.
- Extend `TagInteractionRule` similarly if status effects should also be triggerable via tag interactions (per CLAUDE.md §3.8) rather than only direct upgrade ownership — your call which path(s) you support, document the decision; supporting it via direct upgrade ownership only is acceptable for this task if tag-triggered status effects add real complexity without a concrete use case yet.

### 3. `EnemyRuntime` Status Effect State
- Add a generic status-effect tracking structure on `EnemyRuntime` (e.g. a small list of active `{ type, remainingDuration, magnitude }` entries) — not separate booleans per effect type.
- Movement speed calculation must factor in active `Freeze`/`Slow` effects (multiplicatively or via override — document which, and how multiple simultaneous effects of different types combine, e.g. does Freeze override Slow, or do they stack additively/multiplicatively).
- `Burn` ticks damage via the existing `TakeDamage` path on an interval while active — reuse existing death/pool-release logic, don't duplicate it.
- Decide and document stacking behavior for repeated application of the *same* effect type (e.g. does re-applying Slow refresh duration, extend it, or do nothing if already active) — pick the simplest reasonable rule for MVP.

### 4. Apply Status Effects from Abilities
- When an ability hit carries upgrade-applied status-effect data (per §2), `AbilityRuntime.Execute` applies it to the hit target(s) via the new `EnemyRuntime` status API, alongside existing damage application.
- Author the Frost-themed example explicitly: one hero's exclusive upgrade causes their ultimate to apply Freeze on hit (exact magnitude/duration is a tunable placeholder — document chosen values).

### 5. Card Picker Integration
- Update Task 07's `LevelUpCardPicker` draw logic to pull from the union of the generic pool and the active hero's `exclusiveUpgrades` list, rather than the generic pool alone. No other change to the existing card UI/selection flow should be needed — confirm and document.

## Out of Scope (do not implement)
- More than 3 status effect types (Freeze/Slow/Burn cover the requested examples; more can be added later following the same pattern)
- Visual indicators for active status effects (e.g. a frost overlay on a frozen enemy) — can be added in a future visual-polish pass
- Tag-triggered status effects, unless trivial given the existing tag-interaction system (see §2)
- Status effects on the hero/wall (this task is enemy-targeting only)

## Acceptance Criteria
- [ ] `HeroDefinitionSO.exclusiveUpgrades` exists; each existing hero has at least 2 exclusive upgrades authored
- [ ] `StatusEffectType` (Freeze/Slow/Burn) implemented with generic state tracking on `EnemyRuntime`, not per-effect booleans
- [ ] Movement speed correctly reflects active Freeze/Slow effects; combination/stacking behavior is documented and predictable
- [ ] Burn correctly ticks damage via the existing `TakeDamage` path without duplicating death/pool-release logic
- [ ] At least one hero-exclusive upgrade applies a status effect on hit (the Frost/Freeze example), verified visually or via debug log
- [ ] `LevelUpCardPicker` draws from the union of generic + hero-exclusive pools; the active hero's exclusive upgrades can appear in card choices, other heroes' exclusives cannot
- [ ] No SO asset is mutated at runtime (status effect runtime state lives on `EnemyRuntime`, never written to `UpgradeDefinitionSO`)
- [ ] No static `Instance` patterns introduced
- [ ] Full playtest: Play → level up enough times to see hero-exclusive upgrades appear in card choices → pick the Freeze-applying upgrade → trigger ultimate on an enemy → enemy visibly stops/slows for the effect duration → resumes normal movement after

## Reviewer Notes
Flag as blocking if:
- Status effects are implemented as separate hardcoded booleans (`isFrozen`, `isSlowed`, `isBurning`) with duplicated timer logic instead of a shared generic structure
- A hero's exclusive upgrades can be drawn while playing a different hero
- Burn damage bypasses `EnemyRuntime.TakeDamage`/existing death handling
- Status effect modifiers are hardcoded per-hero in `EnemyRuntime`/`AbilityRuntime` rather than driven by `UpgradeDefinitionSO` data
