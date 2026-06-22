# Task 14 — Hub Scene & Equip Management UI

> Read `CLAUDE.md` in full, and review the Task 01–13 implementations before starting. This task adds a dedicated hub/main-menu scene where the player manages gear/artifacts and equip loadouts across heroes, replacing Task 12's debug grant/equip triggers as the real player-facing flow. This scene is the natural future home for other cross-run management as the project grows (per CLAUDE.md §6 note).

## Goal

By the end of this task: launching the game lands on a Hub scene (not directly into hero-select/gameplay); the Hub shows the player's `GearInventory` contents, lets them pick a hero and view/edit that hero's `HeroLoadout` across all 6 slots, and provides a way to proceed into a run (leading into the existing Task 05 hero-select flow, or replacing it if the Hub's hero-picking already covers that — decide and document which).

## Scope

### 1. Scene & Flow Structure
- New `Hub` scene, set as the game's startup scene (or reached immediately after a minimal splash, if one already exists — check current build settings/scene load order and adjust).
- Decide and document how this relates to Task 05's existing hero-select screen: either (a) the Hub absorbs hero selection — picking a hero in the Hub and pressing "Start Run" goes straight to gameplay, or (b) the Hub is a separate prior step and Task 05's hero-select still runs afterward. Prefer (a) if it's a clean merge given the existing hero-select implementation — avoid making the player pick a hero twice in two different screens.
- "Start Run" (or equivalent) transitions to the gameplay scene with the selected hero and its current `HeroLoadout` applied.

### 2. Gear Inventory Display
- List/grid view of `GearInventory` contents (Task 12/13), grouped or sortable by slot type and/or rarity (simple grouping is enough — no need for advanced filtering/search this task). Show item name, rarity (e.g. as a color-coded label/text tag, consistent with placeholder-first UI), and slot type.
- This can be a plain scrollable list — no need for elaborate grid/card art.

### 3. Equip Screen (Per Hero)
- For the selected hero: show all 6 slots (Helmet/Body/Hands/Legs/Feet/Artifact) with currently equipped item (or "Empty") per slot.
- Clicking a slot opens a simple filtered view of inventory items valid for that slot (gear items matching slot type, or all artifacts for the Artifact slot), letting the player equip one — calls into Task 12's existing equip logic (replace-not-destroy behavior must be preserved, no new equip code path).
- Unequip option per slot (returns item to inventory, same underlying call Task 12 already supports).
- Switching which hero is being viewed/edited in the Hub correctly shows that hero's own `HeroLoadout`, independent of other heroes' loadouts.

### 4. Persistence Integration
- All inventory/equip changes made in the Hub go through Task 12's existing `GearInventory`/`HeroLoadout` APIs (no new parallel mutation path), so existing save/load continues to work unchanged — confirm this explicitly rather than assuming it.

## Out of Scope (do not implement)
- Advanced inventory filtering/search/sorting beyond basic slot/rarity grouping
- Comparing equipped vs. inventory item stats side-by-side (a nice future addition, not required now)
- Visual polish/art for the Hub (placeholder UI, consistent with the rest of the project)
- Selling/disenchanting unwanted gear (a future economy sink, not in scope here)

## Acceptance Criteria
- [ ] Game launches into the Hub scene
- [ ] Hub displays full `GearInventory` contents in a basic grouped/sortable list
- [ ] Player can select a hero and view/edit that hero's full 6-slot `HeroLoadout` from the Hub
- [ ] Equip/unequip in the Hub calls Task 12's existing logic exactly (no parallel equip path), preserving replace-not-destroy behavior
- [ ] Switching the viewed hero correctly isolates each hero's own loadout
- [ ] "Start Run" correctly transitions into gameplay with the selected hero and its current loadout applied, without requiring a duplicate hero pick (decide and document how this merges with or replaces Task 05's hero-select)
- [ ] Save/load (Task 12) continues to work correctly with Hub-driven changes — verified by equipping something, closing/reopening the game, and confirming it's still equipped
- [ ] No SO asset is mutated at runtime
- [ ] No static `Instance` patterns introduced
- [ ] Full playtest: Launch game → land on Hub → see dropped gear from a previous play session (Task 13) → equip a few items on a hero → start a run → confirm the equipped stats are active in gameplay (per Task 12's existing verification method) → finish or quit the run → return to Hub with loadout intact

## Reviewer Notes
Flag as blocking if:
- Equip/unequip in the Hub uses a different code path than Task 12's existing logic, risking divergent or duplicated state
- The player is forced to pick a hero twice (once in the Hub, once in a separate leftover Task 05 hero-select screen) without a documented reason
- Hub changes don't correctly persist (e.g. equipping in the Hub doesn't survive a restart, while Task 12's debug trigger did) — would indicate the Hub bypassed the real persistence path
