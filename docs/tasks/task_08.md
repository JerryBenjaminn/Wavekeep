# Task 08 — Ability Feedback Indicators & Win/Loss Screens

> Read `CLAUDE.md` in full, and review the Task 01–07 implementations before starting. This task adds two independent, separable pieces of polish: (1) simple visual feedback for basic attack and ultimate execution so damage is visible during play, and (2) end-of-run screens for victory and defeat, consuming the `RunEndedEvent` that Task 02 has been publishing since wave logic was built but that nothing has consumed yet.

This is explicitly still placeholder-tier visual work per CLAUDE.md §6 ("placeholder art is fine until loop is validated") — simple, clear, and functional, not final art.

## Part A — Ability Feedback Indicators

### Goal
When the basic ability fires or the ultimate executes, the player sees something happen — a visible projectile/beam/effect from caster to target(s), distinct enough between basic and ultimate, and ideally distinct enough between single-target and AoE execution (per `AbilityDefinitionSO.targetingType`, Task 04) that the range/targeting issue noted during Task 06 playtesting can finally be diagnosed visually.

### Scope
1. **Basic ability (single-target) indicator:** a simple visual line/projectile from the hero capsule to the resolved target enemy at the moment of `AbilityRuntime.Execute`. A `LineRenderer` flash (brief duration, e.g. 0.1–0.15s) or a simple moving projectile primitive are both acceptable — pick whichever is simpler given the existing `AbilityExecutionContext`/targeting resolution, and document your choice.
2. **Ultimate (AoE) indicator:** a visible radius indicator (e.g. an expanding ring/sphere primitive, or a flat circle on the ground matching the AoE radius from `AbilityDefinitionSO`) at the moment of ultimate execution, scaled to the actual radius value used — this should make it easy to visually confirm the AoE is hitting (or not hitting) enemies at its edges.
3. **Hook point:** add the visual trigger at the same point `AbilityRuntime.Execute` already resolves targets and calls `TakeDamage` — do not duplicate targeting logic in the visual code; the indicator should reflect the actual resolved target(s)/radius, not a separate approximation. This is the detail that will let you actually diagnose the range issue noted earlier.
4. **Range/targeting diagnostic:** once the indicator is in, specifically verify the previously-noted issue (basic attack seeming to miss enemies near the wall's edges) and report what you find — e.g. is the "nearest in range" check using the wrong reference point, is the range value too small relative to wall width, etc. Fix it if the cause is clear and cheap to fix; otherwise report findings for a follow-up decision.

## Part B — Win/Loss Screens

### Goal
`RunEndedEvent` (publishing since Task 02, "victory" when the last configured wave clears, "defeat" when wall HP reaches 0) finally gets a UI consumer. The run pauses on a result screen instead of continuing to spawn or sitting silently.

### Scope
1. **`RunEndScreen` (Scripts/UI):** subscribes to `RunEndedEvent` via `EventBus`. On trigger, pauses gameplay (reuse the same pause approach as Task 07's card picker — confirm it's consistent, since both need gameplay paused while UI is interactive) and shows a simple Canvas screen.
2. **Victory variant:** shows "Victory" (or similar), final stats if trivial to surface (e.g. wave reached, currency earned — keep minimal, no need for a stats breakdown screen), and a "Play Again" / "Return to Hero Select" button.
3. **Defeat variant:** shows "Defeat" (or similar), same minimal stats, same restart option.
4. **Restart flow:** "Play Again" should cleanly reset the run state — re-trigger hero select (Task 05) or directly restart with the same hero (your call, document it), reset `WaveSpawner` to wave 1, reset `WallRuntime` HP, reset `CurrencyManager`/`XPManager`/`UpgradeInventory`/`ConsumableInventory` to fresh-run defaults. Audit what state needs resetting across Tasks 02–07 and confirm nothing leaks between runs (e.g. old upgrades persisting into a "fresh" run would be a bug).

## Out of Scope (do not implement)
- Final VFX/particle polish — primitives and simple shapes are enough
- Detailed end-of-run stats/breakdown screens
- Persisting best-run stats between sessions
- New ability types or targeting types beyond what Task 04 already supports

## Acceptance Criteria
- [ ] Basic ability execution shows a visible single-target indicator matching the actually-resolved target
- [ ] Ultimate execution shows a visible AoE radius indicator matching the actual radius value from `AbilityDefinitionSO`
- [ ] Indicators are driven from the same resolution point as the real damage application — no separate/duplicated targeting logic
- [ ] The previously-noted wall-edge range issue has been investigated and either fixed or clearly diagnosed with findings reported
- [ ] `RunEndScreen` correctly shows Victory on last-wave-clear and Defeat on wall-destroyed, matching `RunEndedEvent`'s result data
- [ ] Gameplay correctly pauses during the end screen using the same pause approach as Task 07, confirmed not to block the end screen's own UI input
- [ ] "Play Again" cleanly resets all per-run state (wave index, wall HP, currency, XP/level, upgrade inventory, consumable inventory) with no leakage from the previous run
- [ ] No SO asset is mutated at runtime
- [ ] No static `Instance` patterns introduced

## Reviewer Notes
Flag as blocking if:
- Visual indicators are computed from a separate approximate targeting check rather than the actual resolved target/radius used for damage
- Restart leaves any stale state from the previous run (most common bug here — check `UpgradeInventory`/`ConsumableInventory`/`XPManager` level carefully)
- The end-screen pause mechanism conflicts with or duplicates Task 07's pause logic instead of reusing it
