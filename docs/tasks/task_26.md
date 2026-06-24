\# Task 26 — Gear Detail Panel: Stat Comparison



> Depends on Task 25 (gear detail panel must exist first). Read `CLAUDE.md` and review Task 12 (equip slots) before

> starting.



\## Goal



When the detail panel (Task 25) is showing an unequipped inventory item, display a stat-by-stat comparison against

whatever item — if any — is currently equipped in that item's target slot, so the player can see at a glance

whether the inspected item is an upgrade.



\## Scope



\### 1. Comparison Display

\- For each stat row already shown in the Task 25 panel, add a comparison indicator showing the difference versus

&#x20; the currently equipped item in the same slot (e.g. a "+5" or "-3" style delta next to the stat value).

\- Use a clear visual distinction for positive vs negative deltas (e.g. color difference) consistent with whatever

&#x20; simple convention is easiest to apply with existing UI styling — implementer's choice, document it.

\- If the slot currently has nothing equipped, comparison shows the inspected item's full stat values as the

&#x20; delta (i.e. comparing against a baseline of zero), rather than hiding the comparison.

\- If a stat exists on the inspected item but not on the currently equipped item (or vice versa), treat the missing

&#x20; side as zero for that stat in the delta calculation.



\### 2. Scope Limited to Inventory Items

\- Comparison only applies when inspecting an unequipped inventory item (comparing it against what's equipped).

&#x20; When the panel is showing an already-equipped item (opened by clicking one of the six equip slots directly, per

&#x20; Task 25), no comparison is shown — there's nothing meaningful to compare it against itself.



\## Out of Scope (do not implement)

\- Comparison between two arbitrary inventory items (only equipped-vs-inspected)

\- Any change to the equip/unequip logic itself — this task only changes what the panel displays



\## Acceptance Criteria

\- \[ ] Inspecting an unequipped item shows a per-stat delta against the currently equipped item in that slot

\- \[ ] Deltas are visually distinguishable as positive or negative

\- \[ ] Empty slot (nothing equipped) correctly shows full stat values as the delta rather than omitting comparison

\- \[ ] Stats present on only one of the two compared items are treated as zero on the missing side

\- \[ ] Inspecting an already-equipped item (opened from a slot) shows no comparison

\- \[ ] No SO asset is mutated at runtime



\## Reviewer Notes

Flag as blocking if:

\- Comparison logic duplicates stat-reading code instead of reusing whatever stat-access pattern Task 25 already

&#x20; established

