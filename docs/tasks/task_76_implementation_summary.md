# Task 76 — Implementation Summary: Per-Rarity Stat Ranges + Affix Roll Tooltips

> Gear balance pass. Replaces each affix's single flat `[min,max]` with per-rarity ranges (Common..Legendary) so a
> higher rarity is always strictly better, and adds a simple "Range: X–Y" hover tooltip in the Hub item-detail.
> No economy/drop-rate changes.

## What changed

### Data shape (`AffixDefinitionSO` + `AffixRarityRange`)
- New `AffixRarityRange` struct (`min`/`max`, own file) and `AffixDefinitionSO._rarityRanges[]` (index 0=Common ..
  4=Legendary). Accessors `MinValueFor(rarity)` / `MaxValueFor(rarity)` / `MidValueFor(rarity)` / `HasRangeFor`.
- The old flat `_minValue`/`_maxValue` are **kept only as a fallback** (used when a rarity's range is unauthored/zero)
  so nothing hard-breaks pre-setup. The public `MinValue`/`MaxValue`/`MidValue` accessors were **removed** so no
  caller can accidentally draw from the flat range — generation/reroll/debug all now go through the per-rarity API.
- Unique is exempt: it never rolls from ranges (hand-authored fixed affixes via `BuildUniqueAffixes`).

### Generation + reroll consistency
- `GearGenerator.RollAffixes` (drops) and `RollAdditionalAffixes` (Task 75 upgrade) now roll
  `Random.Range(def.MinValueFor(rarity), def.MaxValueFor(rarity))` for the item's / upgraded rarity.
- `GearManager.RerollAffix` (Task 75) rolls within the per-rarity range for the item's **current** rarity.
- `GearDebugController` debug spawn uses `def.MidValueFor(rarity)`.

### Authored ranges (`Task76AffixRangeSetup`, tunable source of truth + validator)
Common / Uncommon / Rare / Epic / Legendary, non-overlapping:
- **Sharpened** (DamageFlat): 2–4 / 5–8 / 9–13 / 14–20 / 21–30
- **Empowered** (Damage×): 1.05–1.08 / 1.09–1.14 / 1.15–1.21 / 1.22–1.30 / 1.31–1.45
- **Emberforged** (Damage×): 1.04–1.07 / 1.08–1.12 / 1.13–1.18 / 1.19–1.26 / 1.27–1.38
- **Farsight** (Range×): 1.04–1.07 / 1.08–1.12 / 1.13–1.18 / 1.19–1.26 / 1.27–1.40
- **Lucky** (Luck): 1–2 / 3–4 / 5–7 / 8–11 / 12–16
- **Swift** (Cooldown×, *lower is better*): 0.90–0.95 / 0.83–0.89 / 0.75–0.82 / 0.66–0.74 / 0.55–0.65
The setup authors these onto the 6 affix assets, validates the no-overlap invariant, and is chained into
`Task67GearSetup` so re-authoring affixes re-applies them. The assets are also committed with the ranges populated.
Menu: **`Wavekeep/Setup Task 76 (Affix Rarity Ranges)`**.

### Tooltip (`HubController.BuildAffixRangeRows` + `GearStatInfo.RangeTooltip`)
- The detail panel appends a "— Affix rolls —" section: one hoverable row per rolled affix (`Name: value`) with a
  `Range: X–Y` tooltip (the per-rarity range for the item's **current** rarity), formatted in the affix's units via
  the existing `TooltipPresenter`/`TooltipTrigger`. Aggregated STATS + the Task 26 comparison are untouched above it.
- Unique → rows shown **without** a range tooltip (empty tooltip text → `TooltipTrigger` shows nothing).
- Zero-affix items → no section built → nothing to hover.

## Flagged decisions
1. **`CooldownMultiplier` is "lower is better".** The literal "each tier's max < next tier's min" only makes sense
   for higher-is-better stats. Swift's ranges therefore **descend** by rarity (Legendary = lowest multiplier = most
   reduction), and the validator checks the invariant in the semantically-correct direction (each tier's min > next
   tier's max). This honors the task's core intent — "higher rarity must always beat lower, no overlap" — rather
   than a numeric-only reading that would make Legendary cooldown the *worst*. Flagged for review.
2. **Flat range retained as fallback** (not deleted) so a mis-run setup degrades gracefully instead of rolling 0;
   the code always *prefers* the per-rarity range, so normal operation never uses the flat value.
3. **Tooltip placed on a per-affix breakdown appended to the always-visible detail panel** (not the Task 75 Modify
   modal), so it's a true hover (no click needed) and satisfies the zero-affix / Unique exclusions naturally.
4. **Range text is verbose but clear** (`Range: +8% Damage – +18% Damage`) — reuses `AffixLabel` for both ends
   rather than a new number-only formatter; text-only, no bar/visual.

## Acceptance criteria
- Every normal affix has non-overlapping per-rarity ranges (higher strictly beats lower). ✓ (authored + validated)
- Drops/Forge always roll within the correct per-rarity range. ✓
- Reroll draws from the current rarity's per-rarity range. ✓
- Hovering an affix shows "Range: X–Y" for that affix at the item's current rarity. ✓
- No range tooltip on Unique or zero-affix items. ✓
- Ranges tunable in `AffixDefinitionSO` without code. ✓
- No change to drop rates/economy/other systems. ✓

## Setup
Run **`Wavekeep/Setup Task 76 (Affix Rarity Ranges)`** (or re-run `Setup Task 67`, which now chains it). The 6 affix
assets are already committed with ranges populated, so this is only needed if affixes are re-authored or ranges retuned.
