# Task 07 — Level-Up Flow (Upgrade Card Picker)

> Read `CLAUDE.md` in full, and review the Task 01–06 implementations before starting. This task replaces Task 04's debug-key upgrade granting (1/2/3) with the real player-facing flow: when `XPLevelUpEvent` fires, the game pauses and presents upgrade cards drawn from the shared `UpgradeDefinitionSO` pool for the player to pick from.

## Goal

By the end of this task, leveling up (via `XPManager`'s existing `XPLevelUpEvent`, Task 03) pauses the run and shows 2–3 upgrade cards drawn from the shared pool; picking one adds it to `UpgradeInventory` (Task 04) exactly as the debug keys did, then resumes the run. No more reliance on the 1/2/3 debug keys for normal play (keep them available behind a debug flag if useful for testing, but the real flow must not require them).

## Scope

### 1. Upgrade Pool Expansion
- Task 04 authored a small set of test `UpgradeDefinitionSO` assets to prove the tag-interaction system. Expand this to a proper pool — at least 6–8 distinct upgrades across the existing `UpgradeTag` set, so card draws feel meaningfully varied. Reuse Task 04's tag/effect structure; no new fields needed unless you find a concrete gap.
- Consider whether any upgrades should be hero-specific-only (i.e. only appear in the draw pool if they'd have a `TagInteractionRule` match with the current hero's abilities) versus universal. This isn't required for MVP, but if it's trivial given the existing tag system, it's a nice touch — don't force it if it adds real complexity.

### 2. `LevelUpCardPicker` (Scripts/UI or Scripts/Economy — document choice)
- Subscribes to `XPLevelUpEvent` via `EventBus`.
- On trigger: pauses relevant gameplay (wave spawning, ability ticking — decide the cleanest pause mechanism given existing systems; a simple global "is paused" flag checked by `WaveSpawner`/`AbilityRuntime.Tick` is fine, document where you added the check).
- Draws 2–3 random `UpgradeDefinitionSO` from the pool (avoid offering an upgrade the player already owns at max relevance if you've implemented stacking limits — otherwise simple random draw is fine for MVP) and presents them via UI.
- On player selection: adds the chosen upgrade to `UpgradeInventory` (same call Task 04's debug keys used), closes the card UI, resumes gameplay.
- If `XPLevelUpEvent` fires multiple times in quick succession (per Task 03's multi-level-up-in-one-gain handling), queue subsequent card picks rather than dropping them or showing them simultaneously — confirm how Task 03 publishes multiple level-up events and handle the queue accordingly.

### 3. Card UI
- Simple Canvas-based screen (reuse existing UI patterns): 2–3 cards showing upgrade name, tag(s), short effect description, and an "Obtain" button per card (naming convention can match the reference screenshots discussed earlier in this project if you want consistency, but no requirement to match exactly).
- No fancy art — consistent with the project's placeholder-first approach.

### 4. Debug Key Behavior
- Keep the 1/2/3 debug keys functional behind an easily toggleable debug flag (e.g. a bool on `HeroAbilityController`/`HeroRuntime` or a `#if UNITY_EDITOR` guard) for quick testing, but the real card-picker flow must work standalone without them.

## Out of Scope (do not implement)
- Reroll mechanics (seen in the reference screenshots) — simple draw-and-pick is enough for MVP, reroll can be added later if desired
- Card UI visual polish/art
- Hero-specific exclusive upgrades beyond the optional nice-to-have in §1
- Persisting upgrade choices between runs

## Acceptance Criteria
- [ ] At least 6–8 distinct `UpgradeDefinitionSO` assets exist in the shared pool
- [ ] `XPLevelUpEvent` correctly triggers the card picker, pausing wave spawning and ability ticking while cards are shown
- [ ] 2–3 cards are drawn and displayed with correct name/tag/effect info per upgrade
- [ ] Selecting a card adds the correct `UpgradeDefinitionSO` to `UpgradeInventory`, matching the existing debug-key behavior exactly (same code path)
- [ ] Multiple level-ups from one large XP gain correctly queue multiple card picks rather than dropping or overlapping them
- [ ] Gameplay correctly resumes after card selection
- [ ] Debug keys (1/2/3) still work behind a toggle for testing, but are not required for normal play
- [ ] No SO asset is mutated at runtime
- [ ] No static `Instance` patterns introduced
- [ ] Full playtest: Play → kill enemies → level up → game pauses → cards shown → pick one → game resumes → picked upgrade's tag-interaction effect is active (verify via existing Task 04 method, e.g. console log of ability damage change)

## Reviewer Notes
Flag as blocking if:
- Card selection uses a different code path than the existing debug-key `UpgradeInventory.Add` call, risking divergent behavior
- Multiple simultaneous level-ups cause dropped, skipped, or overlapping card picks
- Pausing is implemented by actually stopping `Time.timeScale` globally if that would also break UI interaction needed to pick a card — confirm the pause mechanism doesn't block the card UI itself from responding to input
