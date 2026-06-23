# Task 22 — In-Game Stat Panel

> Read `CLAUDE.md` in full, and review existing systems across Tasks 03/04/09/12/19 before starting. This task adds a toggleable in-game panel showing the player's current run stats — a debugging/transparency tool for the player, separate from the always-visible Task 03 currency/XP HUD.

## Goal

The player can open a panel (e.g. via a key press or a small UI button) during a run that shows current hero stats (effective damage/cooldown/range after all modifiers — level, tag-interactions, equipped gear, consumables), active upgrades owned, active consumable effects, and reroll count — giving visibility into the otherwise-invisible modifier stack.

## Scope

### 1. Stat Aggregation
- Add a read-only aggregation method/class that queries `AbilityRuntime` (basic + ultimate) for their final computed stats (post level/tag/gear/consumable modifiers, per the existing modifier pipeline from Tasks 04/09/12/19), rather than introducing a separate calculation — this panel must reflect the *actual* values abilities use, not a re-derived approximation.
- Pull `UpgradeInventory` contents (owned upgrades, Task 04/11), active `ConsumableInventory` effects (Task 06/09), and current reroll count (Task 09) for display.

### 2. Panel UI
- Simple toggleable Canvas panel (e.g. Tab key or a small HUD button) showing:
  - Basic ability: current damage, cooldown, AoE radius (or range)
  - Ultimate: current damage/DoT, cooldown, duration, slow %, current charge state (reuse Task 21's progress if convenient)
  - List of currently owned upgrades (name + tags)
  - List of active consumable effects with remaining duration if applicable
  - Current reroll count
- No need for fancy styling — a scrollable text/list panel is sufficient, consistent with the project's placeholder-first UI approach.

## Out of Scope (do not implement)
- Editing stats from the panel (read-only display only)
- Persisting panel open/closed state between sessions
- Historical stats (e.g. damage dealt this run) — current state only, not a stats-tracking system

## Acceptance Criteria
- [ ] Panel can be toggled open/closed during a run
- [ ] Displayed ability stats exactly match the actual values `AbilityRuntime` uses at execution time (verified by comparing panel display against a debug log of real execution values)
- [ ] Owned upgrades, active consumable effects, and reroll count are correctly displayed and update live as they change
- [ ] No SO asset is mutated at runtime
- [ ] No static `Instance` patterns introduced
- [ ] Full playtest: Play → open panel → note basic/ultimate stats → pick an upgrade or buy a consumable → panel reflects the change → values match actual in-game ability behavior

## Reviewer Notes
Flag as blocking if:
- The panel computes its own separate/approximate version of ability stats instead of reading the real values `AbilityRuntime` already computes
