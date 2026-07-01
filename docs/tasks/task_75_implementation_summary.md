# Task 75 — Implementation Summary: Reroll-Affix + Upgrade-Rarity Dust Sinks

> Gear redesign part 6. Adds the two remaining Dust sinks to the Task 67/71 backend and surfaces them from the
> Task 73 Hub item-detail UI. Both are Hub-only, Dust-only, no-RNG-on-outcome, and never destroy/risk an affix.

## What changed

### Backend
- **`GearEconomyConfigSO`** — two new tunable cost arrays (indexed by the item's CURRENT rarity, Common..Unique):
  `_rerollAffixCostByRarity = {3,6,15,35,80,0}` and `_upgradeRarityCostByRarity = {15,40,90,200,0,0}`, with
  `RerollAffixCost(rarity)` / `UpgradeRarityCost(rarity)` accessors. Unique = 0 (not rerollable / Forge-only);
  Legendary upgrade = 0 (cap). Reroll < upgrade < forging fresh at the resulting tier (per the task's guidance).
- **`GearInstance`** — three `internal` ADD-or-REPLACE-only mutators: `ReplaceAffix(index, replacement)` (reroll —
  one slot, same type), `AppendAffixes(additions)` (upgrade — new slots only), `SetRarity(rarity)` (invalidates the
  derived-stat cache since the implicit scales with rarity). None ever drops an existing affix.
- **`GearGenerator.RollAdditionalAffixes(base, newRarity, existing)`** — rolls ONLY the extra affix slot(s) the
  higher rarity adds (`AffixCountFor(new) − existing.Count`), excluding affix definitions already on the item so
  upgraded items keep distinct affixes, reusing the same weighted-draw + range-roll as fresh generation. Returns
  the list to append.
- **`GearManager.RerollAffix(instanceId, affixIndex)`** — re-rolls one affix's VALUE within its
  `[MinValue, MaxValue]`, same type, spends the reroll cost, persists. No-op (returns false) on unowned / invalid
  index / Unique / unaffordable. Instance id unchanged.
- **`GearManager.UpgradeRarity(instanceId)`** — raises rarity one tier (hard-capped at Legendary), rolls + appends
  new slots, spends the upgrade cost, persists. No-op on Unique (Forge-only), Legendary (cap), unowned, or
  unaffordable. A belt-and-suspenders `next > Legendary` guard means the sink can never produce Unique.
- **`FindOwnedInstance`** — locates an owned instance in inventory, overflow, OR any hero's loadout, so reroll/
  upgrade work on equipped items too (they mutate the same reference the loadout holds → stats update live + persist).

### Hub UI (`HubController` + `GearStatInfo`)
- **Detail-panel "Modify" button** — enabled for any owned inspected item (unequipped or equipped on the active
  hero). Opens the **Modify modal**.
- **Modify modal** — title + live Dust, a single **Upgrade → [Next] (cost)** button (disabled with a clear label at
  Legendary / Unique), and a scroll of per-affix rows (`Name: value` + **Reroll (cost)** button; disabled for
  Unique / when unaffordable). Both actions route through the **shared Task 73 confirm modal** showing the cost
  before committing; on success Dust, the detail panel (rarity/name/stats), the loadout/inventory, and the modal
  all refresh live. `GearStatInfo.AffixLabel` formats each affix value by its stat type.

### Editor (`Task75GearMutationSetup`, additive + idempotent)
- Authors the two cost arrays on `GearEconomyConfig`, re-ensures the Hub bootstrap's `_gearEconomyConfig`, raises the
  detail panel's stat-scroll bottom to make room, builds the Modify button + modal, wires the new HubController
  fields. Chained into `Task14SceneSetup` after Task 73/74. Menu: **`Wavekeep/Setup Task 75 (Reroll + Upgrade Rarity)`**.

## Flagged decisions
1. **Error handling = no-op + `false`/log**, consistent with `Salvage` (returns 0) and `ForgeArtifact` (returns
   null). Callers (the UI) already gate the buttons, so these are defensive; nothing throws.
2. **Actions surfaced via a Modify MODAL launched from the detail panel** rather than crammed inline into the
   fixed-size detail panel — consistent with the existing Forge/Overflow modals and robust to resolution. Still
   "accessible from the item-detail UI with cost shown before committing".
3. **Reroll/upgrade allowed on EQUIPPED items** (not just inventory), since they mutate rather than remove. Salvage
   remains unequipped-only (unchanged).
4. **Hub-only is structural, not a runtime flag:** the backend methods live on the shared `GearManager`, but the
   only UI that calls them is the Hub's Modify modal — the gameplay scene has no path to them (same pattern as Task
   73 salvage/forge). No in-run entry point was added.
5. **Upgrade tops up to the new rarity's full affix count** if an item was somehow short of its tier's count
   (never removes) — normally the delta is exactly one slot per tier step.

## Acceptance criteria
- `RerollAffix` re-rolls only the targeted value in-range, spends Dust, persists, leaves others + rarity untouched. ✓
- `UpgradeRarity` raises one tier (max Legendary), spends Dust, appends new slots, keeps existing affixes, persists. ✓
- Neither action on a Unique; upgrade not on a Legendary. ✓ (guarded in backend AND disabled in UI)
- Both costs in `GearEconomyConfigSO`, tunable without code. ✓
- Accessible from the Hub item-detail UI with cost shown before committing. ✓
- Not reachable in-run. ✓ (Hub-only UI)
- No existing affix destroyed/replaced/rerolled as a side effect of upgrade. ✓
- Dust deducted and the balance reflected in the UI immediately. ✓ (`RefreshDust` across all labels)

## Setup
Run **`Wavekeep/Setup Task 71 (Gear Economy)`** first if the economy asset doesn't exist, then
**`Wavekeep/Setup Task 75 (Reroll + Upgrade Rarity)`** from the Hub, and save. A full `Setup Task 14` rebuild also
applies it in order.
