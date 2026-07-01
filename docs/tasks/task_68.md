# Task 68 — Gear Redesign Part 2: Drop Generation Flow

> Read `CLAUDE.md` in full, and read `gear_redesign_001.md` (Task 66 analysis, §3) plus the Task 67
> implementation summary before starting — this task builds on the `GearInstance`/base/affix/config
> types introduced in Task 67. Don't redefine those types; use and extend them. This file describes
> outcomes, not code.

## Goal

Replace the current "loot table returns a pre-made finished item" drop flow with one that generates a
rolled `GearInstance` at drop-time: a slot, a rolled rarity, an implicit value, and a set of rolled affixes
appropriate to that rarity. This is the second half of proving the new gear pipeline before salvage, sinks,
or any UI investment.

## Locked decisions for this task

- Rarity is rolled as its own step, using the same Luck-weighted approach the loot tables already use
  today — don't introduce a second, conflicting weighting model.
- Affix count per rarity comes from the `GearGenerationConfigSO` introduced in Task 67 (Common = 0 …
  Unique = fixed/hand-authored, no random rolls for Unique).
- Affixes are drawn from the shared pool, respecting each affix's slot-eligibility, with no duplicate
  affix type on the same instance.
- **Range is excluded as a gear implicit/affix stat for this task and going forward**, until a future task
  resolves the underlying ability-side ambiguity (see §3 below) — implicits/affixes draw from
  Damage/Cooldown (+ Luck) only.
- Loot table entries change meaning: from "this entry IS a finished item" to "this entry references a slot
  (or a base), with rarity now resolved as a separate roll on top." Coordinate this carefully against the
  existing balance-owned drop weights (Tasks 61–64) — the intent is to preserve the *overall* drop-rate
  feel that's already been tuned, not to silently double-roll or flatten it. Flag if you find a case where
  preserving that feel isn't straightforward with the existing table data.
- If the design later makes Artifacts craft-only (a sink from a future task), that is NOT decided yet for
  this task — Artifacts should still be a normal droppable slot here. Don't pre-emptively remove them from
  loot tables.

## Scope

### 1. Fix the existing Range-based gear assets from Task 67

Investigation found that `RangeMultiplier` is inconsistently dead depending on an ability's
`TargetingType` — live (grows AoE/blast radius) on `AreaOfEffect`/`TargetedAreaOfEffect` abilities, dead
(arena geometry already saturates it) on `SingleTarget`/`AoE zone`/`PiercingLine` abilities, with no UI
signal telling the player which they have. `base_legs` (Greaves implicit) and `affix_farsight` (a
universal affix) both currently use `RangeMultiplier`, making them inert on most builds.

Before doing this task's main generation work, re-map these two existing assets to a stat that is live
across all targeting types (Damage and/or Cooldown, consistent with this task's locked Damage/Cooldown(+Luck)
implicit/affix decision). Do not touch `RangeMultiplier` itself or any ability/targeting code — this is a
gear-asset-only fix, scoped to the two assets named above. Note the change in your implementation summary.

### 2. Drop generation flow

- Update the drop-resolution flow (currently in `LootService`) so that when the existing drop-chance gate
  passes, the result is a freshly generated `GearInstance` rather than a reference to a pre-made SO:
  resolve a slot/base, roll a rarity using the existing Luck-weighted approach, resolve the implicit value
  for that base+rarity, and roll the appropriate number of affixes for that rarity from the shared pool.
- Update loot table data/authoring as needed so entries express "slot/base + weight" rather than
  "finished item reference" — adjust the minimum necessary to support this without re-doing the Task
  61–64 balance work; if existing table assets need re-authoring to fit the new entry shape, do that
  re-authoring but preserve the tuned weight *values* as closely as the new shape allows, and flag any
  case where an exact preservation isn't possible.
- Continue publishing the existing drop event (`GearDroppedEvent` or equivalent) with the newly generated
  instance, so downstream consumers (debug tooling now, Task 69's visual drops later) have something to
  hook into without this task needing to build any presentation.
- Granting the generated instance to inventory should go through the same `GearManager` path established
  in Task 67.

## Out of Scope (do not implement)

- Any visual representation of the drop (arena markers, toasts, summary panels) — Task 69.
- Inventory capacity cap, salvage, or any sink (reroll/upgrade/craft) — later tasks.
- Any Hub UI changes.
- Making Artifacts craft-only / removing them from loot tables — not decided yet, explicitly deferred.
- Re-tuning drop *rates* or rarity weights as a balance pass — this task adapts the existing tuned values
  to the new entry shape, it does not re-balance them.
- Splitting `RangeMultiplier` into separate acquisition-range / blast-radius fields, or any other change to
  ability/targeting code — that is a separate future task (see backlog note below); this task only
  re-maps the two named gear assets away from it.

## Backlog — not yet scheduled (context only, not part of this task)

Found during investigation: `RangeMultiplier` conflates single-target acquisition radius (always-satisfied
by arena geometry, effectively dead) and AoE/blast radius (live, scales 3 apex abilities + Frost Bolt
Burst). A future task should split this into two separately-named fields/modifier types in the ability
system so gear can cleanly target the live half (`BlastRadius`) without touching the inert path. This is
ability-system refactoring, not gear work, and is explicitly not part of this task.

## Acceptance Criteria

- [ ] `base_legs` and `affix_farsight` no longer reference `RangeMultiplier`; both now use a stat that is
      live regardless of an ability's `TargetingType`.
- [ ] Killing enemies in a test run produces freshly generated `GearInstance`s (verified via debug
      logging/tooling) with a slot, a rolled rarity, a correct implicit value for that base+rarity, and an
      affix count matching `GearGenerationConfigSO` for the rolled rarity.
- [ ] No duplicate affix type appears on the same generated instance, and all rolled affixes are eligible
      for the instance's slot.
- [ ] Unique-rarity rolls produce the fixed/hand-authored affix set, not random rolls.
- [ ] No newly generated instance uses `RangeMultiplier` as an implicit or affix stat.
- [ ] Overall drop rate / rarity distribution feel matches the existing Task 61–64 tuning as closely as the
      new entry shape allows; any unavoidable deviation is flagged in the implementation summary, not
      silently introduced.
- [ ] Generated instances are granted to inventory via the existing `GearManager` path and persist
      correctly through Task 67's save system.
- [ ] `GearDroppedEvent` (or equivalent) still fires with the generated instance, unchanged in how
      downstream code can consume it.
- [ ] `RangeMultiplier` itself, and all ability/targeting code, remain untouched.

## Reviewer Notes

Flag as blocking if:
- A second, parallel rarity-weighting system was introduced instead of reusing the existing Luck-weighted
  approach.
- Affix drawing allows duplicate affix types or ignores slot eligibility.
- Unique items receive randomly rolled affixes instead of a fixed/hand-authored set.
- Drop rates were re-balanced rather than adapted to the new entry shape.
- `RangeMultiplier`, ability code, or targeting code was modified beyond the two named asset re-maps.
- Any presentation/UI work was done in this task.