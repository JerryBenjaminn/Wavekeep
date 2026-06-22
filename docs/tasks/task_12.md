# Task 12 — Gear & Artifact Core System (Data, Equip, Persistence)

> Read `CLAUDE.md` in full (especially the updated §6 note on Gear/Artifact), and review the Task 01–11 implementations before starting. This task builds the foundational gear system: item data, rarity tiers, equip slots, stat application to heroes, and save/load persistence. It does NOT include loot drop tables (Task 13) or a hub/management UI (Task 14) — for this task, granting and equipping gear is done via debug triggers/inspector, same pattern as Task 04's debug-key upgrade granting before the real UI existed.

## Goal

By the end of this task: `GearItemSO`/`ArtifactItemSO` data models exist with rarity tiers, a hero can have items equipped across 6 slots (Helmet/Body/Hands/Legs/Feet/Artifact), equipped item stats correctly modify the hero's effective stats (feeding into the existing `AbilityRuntime`/`HeroRuntime` stat pipeline), and the player's gear inventory + equip loadout persist to disk between sessions.

## Scope

### 1. Data Model
- `GearSlot` enum: `Helmet`, `Body`, `Hands`, `Legs`, `Feet`, `Artifact`.
- `Rarity` enum: `Common`, `Uncommon`, `Rare`, `Epic`, `Legendary`, `Unique` (ascending power).
- `GearItemSO`: `itemName`, `icon`, `slot` (`GearSlot`, excluding `Artifact`), `rarity`, a list of stat modifiers (e.g. `{ statType, value }` — reuse or mirror the modifier shape from existing `AbilityUpgradeLevel`/`TagInteractionRule` if there's a clean fit, otherwise define a small dedicated `StatModifier` type; document your choice).
- `ArtifactItemSO`: `itemName`, `icon`, `rarity`, and an effect (can be a stat modifier like gear, or a more unique effect akin to a hero-exclusive upgrade — keep it simple for this task, a stat modifier is enough; unique artifact behaviors can follow later once the pattern is proven).
- Stat types to support at minimum: damage modifier, cooldown modifier, range modifier (mirroring what `AbilityRuntime` already understands from Task 04) — extend the existing modifier-application pipeline rather than building a parallel one.

### 2. Equip System
- `HeroLoadout` (or similar): per-hero record of which `GearItemSO`/`ArtifactItemSO` is equipped in each of the 6 slots (nullable per slot — empty is valid).
- Equipping an item in a slot already holding an item replaces it (returns the previous item to inventory, doesn't destroy it).
- `HeroRuntime`/`AbilityRuntime`'s existing stat computation (base → level → tag-interaction modifiers, per CLAUDE.md §3.8) gains a further step: equipped gear/artifact stat modifiers apply on top, in a documented, consistent order with the existing modifier stack.

### 3. Inventory & Persistence
- `GearInventory`: the player's full owned collection of `GearItemSO`/`ArtifactItemSO` instances (not equipped — just owned). Since items are SO-referenced data rather than fully unique instances with rolled stats (decide and document: are all instances of a given `GearItemSO` identical, i.e. inventory just needs item-ID + count, or do individual drops need unique rolled values? — simplest for this task is identical-by-definition items, just track ownership/count; unique rolled stats per drop can be a documented future refinement once loot tables exist).
- Persist `GearInventory` contents and each hero's `HeroLoadout` to disk (e.g. JSON via `Application.persistentDataPath`) — load on game start, save on relevant changes (equip/unequip, and whenever gear is granted via this task's debug trigger).
- This is the project's first persistence requirement — keep the save format simple and versionable (e.g. a wrapper object with a `saveVersion` field) so it's easy to extend later without breaking existing saves.

### 4. Debug Granting & Equipping (Stand-in for Task 13/14)
- Add a debug trigger (key press or editor menu) that grants a sample `GearItemSO`/`ArtifactItemSO` of varying rarity to `GearInventory`, and another that equips/unequips a given item to a given hero's slot — enough to verify the full pipeline without building real UI yet.

## Out of Scope (do not implement)
- Loot drop tables / enemies dropping gear (Task 13)
- Hub/main-menu equip management UI (Task 14)
- Unique per-drop rolled stats (documented future refinement if needed)
- Artifact unique/non-stat-modifier effects beyond simple stat modifiers
- Editor authoring tools for gear content (future, separate from this task)

## Acceptance Criteria
- [ ] `GearSlot`, `Rarity`, `GearItemSO`, `ArtifactItemSO` implemented per the above
- [ ] `HeroLoadout` correctly tracks per-hero, per-slot equipped items, with replace-not-destroy behavior
- [ ] Equipped item stat modifiers correctly apply to the hero's effective stats, integrated into the existing `AbilityRuntime` modifier pipeline (not a parallel calculation)
- [ ] `GearInventory` correctly tracks owned items; persists to disk and reloads correctly on next launch
- [ ] `HeroLoadout` persists to disk and reloads correctly on next launch
- [ ] Save format includes a version field
- [ ] Debug triggers can grant gear and equip/unequip it for verification
- [ ] No SO asset is mutated at runtime (gear/artifact definitions are templates; ownership/equip state lives in `GearInventory`/`HeroLoadout`)
- [ ] No static `Instance` patterns introduced
- [ ] Full playtest: grant a gear item via debug trigger → equip it → start a run → hero's ability output reflects the equipped item's stat bonus → close and reopen the game → gear inventory and equip loadout are unchanged

## Reviewer Notes
Flag as blocking if:
- Equipped gear stats are applied via a separate calculation path instead of extending the existing `AbilityRuntime` modifier pipeline
- Save/load uses a fragile format with no version field, or saves/loads silently fail without error handling
- Equipping over an occupied slot destroys the previously equipped item instead of returning it to inventory
- Gear/Artifact SO assets are mutated at runtime instead of acting as read-only templates
