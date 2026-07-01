\# Task 76 — Gear Balance: Per-Rarity Stat Ranges + Affix Roll Tooltips



> Read `CLAUDE.md` in full, and read Task 67 (`AffixDefinitionSO`, `GearGenerationConfigSO`) and Task 75

> implementation summaries before starting. This task locks stat value ranges by rarity so higher rarity

> is always strictly better, and surfaces those ranges to the player via a simple tooltip. This file

> describes outcomes, not code.



\## Goal



Playtesting found that a Rare item can currently roll higher than a Legendary — rarity has no enforced

value boundaries. Fix this by introducing per-rarity value ranges on each affix (replacing the current

single `\[minValue, maxValue]` range), and show the active range to the player as a simple text tooltip

when hovering an affix in the Hub item-detail view.



\## Locked decisions for this task



\- \*\*No overlap between adjacent rarity tiers.\*\* A Common's maximum rolled value must be strictly less

&#x20; than an Uncommon's minimum, an Uncommon's maximum strictly less than a Rare's minimum, and so on up

&#x20; to Legendary. A higher rarity must always be capable of beating any lower rarity on the same affix —

&#x20; no exceptions for normal affixes.

\- \*\*Unique is exempt from the no-overlap rule.\*\* Unique affixes are hand-authored fixed values (not

&#x20; rolled), so they don't participate in the per-rarity range system. They can be whatever value fits

&#x20; the design.

\- \*\*Tooltip is simple text only:\*\* "Range: X–Y" (or equivalent) showing the min and max for the item's

&#x20; current rarity tier. No bar, no visual indicator of where in the range the rolled value landed.

\- The per-rarity ranges live in `AffixDefinitionSO` (the existing read-only template from Task 67) as a

&#x20; replacement for or extension of the current single `\[minValue, maxValue]` pair — your call on the

&#x20; exact data shape (e.g. a per-`Rarity` value array or a struct per tier), but it must remain a

&#x20; designer-tunable SO field, not hardcoded.

\- Existing generation logic in `GearGenerator` must be updated to draw `rolledValue` from the correct

&#x20; per-rarity range rather than the old flat range — no new generation path, just reading the right

&#x20; sub-range for the item's rarity.

\- Reroll-affix (Task 75) must also draw from the per-rarity range of the item's \*current\* rarity when

&#x20; rerolling a value — confirm this is consistent after the data shape change.



\## Scope



\### 1. Per-rarity value ranges on `AffixDefinitionSO`

\- Replace or extend the current flat `minValue`/`maxValue` with a per-rarity range structure covering

&#x20; Common through Legendary (Unique excluded, as above).

\- Author or update the existing affix assets to populate these ranges with values that respect the

&#x20; no-overlap rule. Exact numbers are your call — document your chosen ranges in the implementation

&#x20; summary and flag them for tuning. The key invariant to enforce: for every affix, every rarity tier's

&#x20; max < the next tier's min.

\- The ranges must be readable in the Hub UI (for the tooltip) without any runtime computation beyond

&#x20; a simple lookup.



\### 2. Generation + reroll consistency

\- Update `GearGenerator` to use the per-rarity range for the item's rolled rarity when assigning

&#x20; `rolledValue` at drop-time and at Forge-time.

\- Confirm `RerollAffix` (Task 75) uses the same per-rarity range for the item's \*current\* rarity —

&#x20; fix if not.



\### 3. Affix tooltip in Hub item-detail

\- When the player hovers (desktop) or long-presses (mobile, if applicable) an affix in the item-detail

&#x20; view (Task 73), show a simple tooltip: "Range: X–Y" where X and Y are the min/max for that affix at

&#x20; the item's current rarity tier.

\- Unique items: no range tooltip (values are fixed, not rolled).

\- Items with zero affixes (e.g. Common): no tooltip shown (nothing to hover).

\- Tooltip positioning and styling should be consistent with any existing tooltips in the Hub UI;

&#x20; if none exist yet, keep it minimal and functional.



\## Out of Scope (do not implement)



\- Visual roll-position indicator (bar/slider showing where in the range the rolled value landed) —

&#x20; explicitly rejected; text only.

\- Any change to rarity drop weights, salvage yields, Forge costs, or other economy values — balance

&#x20; pass only on affix value ranges.

\- Sorting or filtering inventory by roll quality.

\- Any in-run UI changes.



\## Acceptance Criteria



\- \[ ] Every normal affix asset has per-rarity ranges populated such that for each affix, every tier's

&#x20;     max is strictly less than the next tier's min (Common max < Uncommon min, etc. up to Legendary).

\- \[ ] Newly generated drops and Forge outputs always roll within the correct per-rarity range for their

&#x20;     rarity — a Rare can never roll a value that falls in the Epic range or above.

\- \[ ] Reroll-affix (Task 75) also draws from the correct per-rarity range for the item's current rarity.

\- \[ ] Hovering an affix in the Hub item-detail shows "Range: X–Y" matching that affix's range for the

&#x20;     item's current rarity.

\- \[ ] No range tooltip appears on Unique items or on items with zero affixes.

\- \[ ] Affix value ranges are tunable in `AffixDefinitionSO` without code changes.

\- \[ ] No change to drop rates, economy values, or any system outside affix value ranges and the tooltip.



\## Reviewer Notes



Flag as blocking if:

\- Any normal affix's rarity ranges overlap (Common max ≥ Uncommon min, etc.).

\- Generation or reroll draws from the old flat range instead of the per-rarity range.

\- Range values are hardcoded anywhere instead of read from `AffixDefinitionSO`.

\- Tooltip shows anything other than the simple "Range: X–Y" text (no bar/visual).

