# Task 13 — Loot Tables & Gear Drops

> Read `CLAUDE.md` in full, and review the Task 01–12 implementations before starting. This task replaces Task 12's debug-grant trigger with real gameplay drops: enemies and bosses drop gear/artifacts based on a loot table, feeding into the existing `GearInventory` from Task 12.

## Goal

By the end of this task, regular enemies have a small chance to drop a Common/Uncommon/Rare item on death, bosses have a guaranteed-or-near-guaranteed drop, and the loot table a boss uses is determined per wave (not hardcoded to the boss type) — so early bosses (e.g. wave 10) drop at most Uncommon, while later bosses (e.g. wave 30+) can roll into Epic/Legendary/Unique. All drops correctly land in the Task 12 `GearInventory` — visible in-run via a minimal pickup/notification cue (no full hub UI yet, that's Task 14).

## Scope

### 1. `LootTableSO`
- New SO: a list of weighted entries, each `{ GearItemSO or ArtifactItemSO reference, weight, rarity (read from the item itself) }`, plus an overall drop-chance percentage for "does this enemy drop anything at all."
- Support rarity-range restriction per loot table (e.g. a regular-enemy loot table only weights Common/Uncommon/Rare entries; a boss loot table can include Epic/Legendary/Unique). Per CLAUDE.md's locked decision, regular enemies should not be able to roll Legendary/Unique — enforce this at the data level (the regular-enemy loot table simply doesn't contain those entries) rather than a hardcoded rarity-check in code, so it stays designer-tunable.

### 2. Enemy/Boss Loot Table Assignment
- Add a `lootTable : LootTableSO` reference field to `EnemyDefinitionSO` — nullable/optional, so not every regular enemy needs to drop something. Author one regular-enemy loot table (low overall drop chance, e.g. 5–10%, Common/Uncommon/Rare only).
- **Boss loot is wave-tiered, not boss-type-tiered:** since Task 10's boss waves currently reuse one `BossDefinitionSO` repeated every 10 waves, the loot table for a boss encounter must be determined by which wave it is, not baked into `BossDefinitionSO` itself (that would force every boss to drop the same tier forever, contradicting "later bosses drop better loot"). Add the boss loot table reference to the wave-level boss spawn data instead — i.e. wherever Task 10's `bossSpawn` field/override lives on `WaveConfigSO`, add a `bossLootTable : LootTableSO` field there. Author at least 2 boss loot tables: an early-tier one (e.g. for wave 10, max rarity Uncommon/Rare) and a later-tier one (e.g. for wave 30+, full range up to Legendary/Unique) — wired to different boss wave entries in the existing test `DifficultyTierSO` content from Task 10. Document how you extended/authored the wave-to-loot-table mapping so it stays easy to add more tiers (e.g. wave 50, 70) by adding new `WaveConfigSO` boss entries with their own table, no code changes.

### 3. Drop Resolution on Death
- On `EnemyRuntime.Die()` (existing path from Task 02/03), after existing currency/XP reward distribution, roll loot if a table is available: regular enemies use their `EnemyDefinitionSO.lootTable`; a boss uses the loot table associated with the current wave's boss spawn entry (per §2), not a table baked into the boss itself — pass the wave-resolved table down to the boss's `EnemyRuntime` instance at spawn time (e.g. via `AbilityExecutionContext`-style spawn parameters, consistent with how Task 10 already passes wave-specific data to spawned enemies).
- First check overall drop chance, then if it hits, weighted-random select one entry, then call `GearInventory.Add` (Task 12) with the resulting item.
- This must not modify or duplicate the existing currency/XP/pool-release logic in `Die()` — it's an additional step alongside it, reading from data, writing only to `GearInventory`.

### 4. Minimal Drop Feedback
- A lightweight, throwaway notification when a drop occurs (e.g. a brief on-screen text "Dropped: [Rare] Iron Helmet" or similar, consistent with the project's placeholder-first UI approach from Tasks 03/06) — not a full inventory screen, just confirmation that a drop happened and what it was, since there's no hub UI yet to browse the inventory.

## Out of Scope (do not implement)
- Hub/inventory browsing UI (Task 14)
- Unique per-drop rolled stats (still out of scope per Task 12)
- Pity timers / guaranteed-drop-after-N-kills mechanics (can be considered later if drop rates feel bad in practice)
- Loot table content beyond what's needed to prove the system (a handful of items per rarity tier is enough — full item roster authoring is ongoing/future work)

## Acceptance Criteria
- [ ] `LootTableSO` implemented with weighted entries and overall drop-chance
- [ ] Regular-enemy loot table is data-restricted to Common/Uncommon/Rare (no Legendary/Unique entries present, not a code-level filter)
- [ ] Boss loot table is determined per wave (via the wave's boss spawn entry), not baked into `BossDefinitionSO` — an early boss wave (e.g. wave 10) and a later boss wave (e.g. wave 30+) use different `LootTableSO` assets with different rarity ranges
- [ ] `EnemyRuntime.Die()` correctly rolls and applies loot (using the wave-resolved table for bosses) without modifying existing currency/XP/pool-release logic
- [ ] Drops correctly land in `GearInventory` (Task 12), verified by checking inventory contents after a play session
- [ ] Minimal on-screen feedback confirms when and what dropped
- [ ] Adding a new boss-tier loot table for a future wave (e.g. wave 50) requires only a new `WaveConfigSO`/loot table asset, no code changes
- [ ] No SO asset is mutated at runtime
- [ ] No static `Instance` patterns introduced
- [ ] Full playtest: Play → kill enough regular enemies to see at least one drop (Common/Uncommon/Rare) → defeat the wave-10 boss → see a low-tier drop (max Uncommon/Rare) → (if testing further) defeat a later-tier boss wave → see access to higher rarities → confirm all drops are present in `GearInventory` afterward and persist after closing/reopening the game

## Reviewer Notes
Flag as blocking if:
- Regular enemies can roll Legendary/Unique items (rarity restriction must be enforced via loot table data, not a forgotten code-level check that could be bypassed by editing the table)
- Boss loot tier is determined by boss type/`BossDefinitionSO` rather than by wave, making it impossible for later bosses to drop better loot than earlier ones without duplicating boss definitions
- Loot rolling duplicates or interferes with the existing currency/XP/pool-release logic in `EnemyRuntime.Die()`
- Drops bypass `GearInventory.Add` and write to inventory state through a separate path
