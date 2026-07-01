# Task 74 — Implementation Summary: Hub UI Mass Salvage (Multi-Select + Salvage by Rarity)

> QoL layer on the Task 71 salvage backend + Task 73 Hub inventory UI. No backend changes — mass salvage loops
> the existing `GearManager.Salvage(instanceId)` per item and reuses the Task 73 confirm modal.

## What changed

### `HubController` (UI logic)
- **Multi-select state** — a `HashSet<GearInstance> _salvageSelection` holds the current selection by instance
  identity. Every `RefreshInventory` first prunes it to live inventory membership
  (`RemoveWhere(!Inventory.Contains)`), so an item that gets equipped or salvaged elsewhere silently drops out.
- **Selectable inventory rows** (`BuildInventoryRow`) — each row is now `[checkbox][clickable label]`. The checkbox
  (`ToggleSelection`) adds/removes the item from the selection (guarded to inventory membership); the label still
  opens the Task 25 detail panel — independent concepts, like the Task 37 hero rows. Selected rows get a brighter
  fill + a ✓ checkbox.
- **Rarity quick-filters** (`BuildRarityFilters` / `OnRarityFilter`) — one button per rarity tier, built live with
  per-tier owned counts. Clicking a tier adds ALL eligible (= all inventory) items of that tier to the selection,
  or removes them if the whole tier is already selected. Button fill reflects state: green = all selected,
  olive = some, neutral = none, dark/disabled = none owned.
- **Live selection summary** (`UpdateSelectionUI`) — a label shows `Selected: N items — +M Dust`, recomputed from
  the selection on every change; the Mass Salvage button shows its count and disables at zero; Clear disables at zero.
- **Mass Salvage confirm** (`OnMassSalvage`) — snapshots the selection, shows total count + total Dust in the shared
  Task 73 confirm modal, then loops `GearManager.Salvage(item.ItemId)` per item (each re-checked for inventory
  membership as a safety net). Clears the selection, closes the detail panel if it was showing a salvaged item, and
  refreshes inventory + Dust. `ClearSelection` empties the set without salvaging.

### `Task74MassSalvageSetup` (editor, additive + idempotent)
- Reflows the Task 73 `InventoryScroll` bottom offset up to make room, and builds the rarity-filter bar, selection
  label, and Mass Salvage / Clear buttons at the bottom of the Hub's RightColumn; wires the four new
  `HubController` fields.
- Chained into `Task14SceneSetup` (after `Task73HubEconomySetup.BuildAndWire`) so a Hub rebuild never drops it.
  Standalone menu: **`Wavekeep/Setup Task 74 (Mass Salvage)`**.

## Flagged behavior decisions
1. **Equipped items are HIDDEN, not shown-disabled.** They already live in loadouts, not the inventory list, so
   they never get a row — structurally unselectable via every path (checkbox, rarity filter). The `ToggleSelection`
   guard + the `RefreshInventory` prune are belt-and-suspenders on top of that structural exclusion.
2. **Rarity filter is a toggle-select aid, not a one-click destructive action.** It only populates/clears the shared
   selection; salvage always goes through the same confirm step as manual selection.
3. **Manual deselection "breaks" a tier's all-selected highlight but not the toggle itself.** Deselecting one item
   of a fully-selected tier turns that filter's fill from green→olive; re-clicking the filter re-selects the whole
   tier (since it's no longer fully selected). This is the intended, predictable behavior.
4. **Per-salvage persistence.** Each `Salvage` call persists (Task 71 behavior) so a batch writes the save once per
   item. Kept as-is to honor "no backend changes / reuse `Salvage`"; a future thin `SalvageMany` wrapper could save
   once if this ever matters for large batches.

## Acceptance criteria
- Multi-select individual items + confirm a mass salvage. ✓
- Quick-select by one or more rarity tiers, combinable with manual selection. ✓
- Equipped items never includable through any path. ✓ (structural — not in inventory list + guarded)
- Single confirm shows total count + total Dust for any mix of selection methods. ✓
- Confirming awards the summed rarity-based yield and removes all selected items. ✓ (reuses `Salvage` per item)
- No change to single-item salvage, Forge, or overflow. ✓

## Setup
Run **`Wavekeep/Setup Task 74 (Mass Salvage)`** from the Hub (after Task 14/73), and save. A full
`Setup Task 14` rebuild also applies it in order.
