# Task 18 — Increase Ability Range to Cover Spawn Points

> Read `CLAUDE.md` in full, and review the Task 02 (`WaveSpawner`/spawn markers) and Task 04 (`AbilityRuntime` targeting/range) implementations before starting. This is a small, targeted fix: enemies are currently untargetable by the basic ability and ultimate for a moment after spawning, until they've walked partway toward the wall, because ability range doesn't reach the spawn line.

## Goal

Enemies are targetable by both basic ability and ultimate immediately on spawn, with no dead zone between the spawn markers and the current effective range.

## Scope

### 1. Diagnose Current Range vs. Spawn Distance
- Measure (via debug log or in-editor inspection) the actual distance from the hero/wall position to the far-side spawn markers (Task 02), and compare against the current `AbilityDefinitionSO.range` value(s) for the basic ability and ultimate (Task 04/05's hero assets).
- Confirm this is purely a range-value tuning issue and not a targeting-logic bug (e.g. confirm the "nearest in range" check itself is correct, per the wall-edge investigation already done in Task 08 — this task assumes that logic is sound and only the range *value* needs increasing).

### 2. Increase Range Values
- Increase `range` on the basic ability and ultimate `AbilityDefinitionSO` assets (for all existing heroes from Task 05) so it comfortably covers the full distance from hero/wall to the spawn line, with a small margin so enemies are targetable the instant they spawn.
- Apply consistently across all existing hero ability assets — don't fix it for one hero and leave another under-ranged.
- If AoE radius (ultimate) and single-target range use different fields, confirm both are adjusted appropriately for their respective targeting type.

## Out of Scope (do not implement)
- Changes to targeting logic itself (nearest-in-range selection, AoE resolution) — this task is a value tuning fix only
- Changes to enemy movement/spawn positions
- New abilities or upgrades

## Acceptance Criteria
- [ ] Basic ability can target and hit an enemy at the instant it spawns (no dead zone)
- [ ] Ultimate can hit enemies at the instant they spawn, consistent with its AoE radius/targeting type
- [ ] Fix applied consistently across all existing hero ability assets
- [ ] No SO asset is mutated at runtime (this is an authoring-time value change to existing assets, not a runtime mutation — values are edited as data, same as any other tuning pass)
- [ ] Full playtest: Play → watch a fresh wave spawn → basic attack/ultimate hits enemies immediately at spawn, not after they've already advanced partway to the wall

## Reviewer Notes
Flag as blocking if:
- The fix is implemented as a code-level range override/hack rather than simply correcting the data values on existing `AbilityDefinitionSO` assets
- Only some heroes' abilities were fixed
