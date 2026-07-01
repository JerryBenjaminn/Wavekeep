# Task 73 — Implementation Summary: Hub UI Overhaul (Inventory, Salvage, Artifact Forge)

> Gear redesign part 5. Builds the real Hub UI for the Task 67/68/71 backend. No backend logic changed — the
> UI only calls existing `GearManager`/`GearGenerator` APIs (and reads the economy SO for pre-confirm numbers).

## What changed

### `HubController` (UI logic)
The existing inventory list / item detail (implicit + affixes) / equip-unequip already met those criteria.
Added:
- **Dust counter** (`RefreshDust`) — shown top-right of the Hub and inside the Forge screen.
- **Salvage** (`OnDetailSalvage`) — a Salvage button in the detail panel, enabled **only for owned, unequipped
  items** (equipped items live in a loadout, not inventory → button greyed/disabled, no path to attempt it).
  Label previews the Dust yield; a shared confirm modal shows `+N Dust` before committing; after salvage the
  item's gone, the detail closes, inventory + Dust refresh.
- **Artifact Forge** (`OpenForge`/`BuildForgeRows`/`OnForgeRarity`) — a modal listing every rarity Common→Unique
  with its Dust cost; unaffordable tiers show a disabled greyed "Need Dust" button. Picking a rarity → confirm →
  `GearManager.ForgeArtifact(rarity)` (deterministic, Dust-only, no RNG). Rows re-evaluate affordability after a craft.
- **Overflow resolution** (`RefreshOverflowAffordance`/`OpenOverflow`/`OnClaimOverflow`/`OnSalvageOverflow`) —
  a "Overflow (N)" button that only appears when the buffer is non-empty, opening a modal that lists pending
  items with **Claim** and **Salvage** per item. Claiming at capacity shows clear red feedback
  (`Inventory full (X/Cap). Salvage something first.`) instead of failing silently. Salvaging from overflow uses
  the same confirm modal. Panel auto-closes when the buffer empties.
- **Shared confirm modal** (`AskConfirm`/`OnConfirmYes`/`CloseConfirm`) — reused by salvage and forge; rendered
  above any other open modal via `SetAsLastSibling`.

### `Task73HubEconomySetup` (editor, additive + idempotent)
- Builds the Dust label, the "ARTIFACT FORGE" / "Overflow (N)" buttons, the Salvage button (reflowing Task 25's
  Equip/Unequip into a centered 3-button row), and the Forge / Overflow / Confirm modals; wires all new
  `HubController` fields + the `_economyConfig` reference.
- **Fixes the Task 71 flag #3 gap**: wires the Hub bootstrap's `_gearEconomyConfig` + `_gearAffixConfig`, so
  salvage/forge actually work in the Hub (without these, `GearManager` no-ops them).
- Chained into `Task14SceneSetup` (after the Task 25/43 panels) so a Hub rebuild never drops it. Standalone menu:
  **`Wavekeep/Setup Task 73 (Hub Gear Economy)`**.

## Acceptance criteria
- Hub bootstrap configs wired; salvage/forge work from the Hub UI. ✓ (`BuildAndWire`)
- Full inventory as per-instance rows with implicit + affixes. ✓ (pre-existing detail panel)
- Equip/unequip per hero through the UI. ✓ (pre-existing)
- Salvage a non-equipped item, see Dust awarded. ✓
- Equipped items not salvageable (disabled, no path). ✓
- Forge with per-rarity Dust cost incl. Unique; unaffordable clearly indicated; confirm before spend; no RNG. ✓
- Overflow visible + resolvable (claim/salvage). ✓
- Claim-at-capacity → clear feedback. ✓
- No backend/data logic changed. ✓
- Debug keys remain (gameplay scene `GearDebugController`, harmless). ✓

## FLAGGED

1. **The UI reads `GearEconomyConfigSO` directly for pre-confirm numbers.** `GearManager` exposes no read-only
   preview of salvage yield / forge cost (`Salvage` returns the yield only *after* removing the item;
   `ForgeArtifact` checks cost internally; only `Capacity` is public). To show "+N Dust" / per-rarity costs
   *before* confirming, `HubController` reads the same economy SO the bootstrap uses. This is the UI reading a
   data asset (idiomatic, not a backend change), but it's a **parallel reference** — display could drift from
   actual if a *different* asset were wired. The Task 73 setup wires the **same** asset to both, so they match.
   *Suggested future cleanup (not done, to respect "no backend changes"): add `GearManager.PreviewSalvageYield`
   / `PreviewForgeCost` so the UI needn't hold its own config ref.*
2. **Inherited Task 71 behaviors surfaced in UI:** the inventory cap is a soft *drop* gate (forge + unequip can
   exceed it); forge output goes straight to inventory (bypasses the cap); equipped-unsalvageable is structural.
   The UI reflects these rather than re-implementing them.
3. **Detail-panel reflow chaining:** the Salvage button is added under Task 25's detail panel and reflows its
   Equip/Unequip. If someone re-runs *Setup Task 25 standalone after* Task 73, it rebuilds the detail panel and
   drops the Salvage button (same additive-chaining caveat the project already documents for Tasks 25/43/39).
   Re-run Task 73 (or a full Task 14 rebuild, which chains both in order) to restore it.

## Setup
Run **`Wavekeep/Setup Task 71 (Gear Economy)`** (authors the economy config) if not already, then
**`Wavekeep/Setup Task 73 (Hub Gear Economy)`** from the Hub, and save. (A full `Setup Task 14` rebuild also
applies everything in order.)
