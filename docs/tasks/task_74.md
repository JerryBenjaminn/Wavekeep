\# Task 74 — Hub UI: Mass Salvage (Multi-Select + Salvage by Rarity)



> Read `CLAUDE.md` in full, and read the Task 71 and Task 73 implementation summaries before starting —

> this task adds a quality-of-life layer on top of the existing single-item `Salvage` action and Hub

> inventory UI. No backend changes beyond what's explicitly described below. This file describes outcomes,

> not code.



\## Goal



Add a faster way to salvage multiple items at once from the Hub inventory screen, combining two

complementary interactions: manual multi-select with a "Mass Salvage" confirm, and a Diablo-style

"salvage by rarity" quick-filter that selects (or directly salvages) every eligible item of the chosen

tier(s).



\## Locked decisions for this task



\- Both interaction modes ship together: free multi-select (tap/click items individually, then confirm) AND

&#x20; a rarity quick-filter (pick one or more rarity tiers, e.g. "select all Common + Uncommon").

\- \*\*Equipped items can never be included in a mass-salvage selection\*\*, whether selected manually or via

&#x20; the rarity quick-filter. This is a hard rule, not just a UI suggestion — enforce it the same way single-

&#x20; item salvage already structurally prevents equipped items (Task 71: equipped instances live in

&#x20; loadouts, not inventory, so they're naturally excluded from any inventory-sourced selection).

\- The rarity quick-filter is a \*selection\* aid, not a separate one-click destructive action — selecting by

&#x20; rarity populates/extends the same selection set that manual multi-select uses, and both go through the

&#x20; same confirm step before anything is salvaged. Don't add a second, separate "instant salvage all X

&#x20; rarity" path that skips confirmation.

\- A single confirm step shows the total item count and total Dust to be awarded before committing,

&#x20; regardless of whether the selection came from manual taps, the rarity filter, or a mix of both.

\- No backend logic changes — this calls the existing `Salvage(instanceId)` per item (or a thin batch

&#x20; wrapper around it if that's cleaner) without altering salvage yield rules, equip checks, or persistence

&#x20; from Task 71.



\## Scope



\- Add multi-select state to the Hub inventory view from Task 73: each item can be toggled in/out of the

&#x20; current selection without leaving the screen.

\- Add rarity quick-filter controls (e.g. one toggle per rarity tier) that add/remove all currently-eligible

&#x20; (non-equipped) items of that tier from the selection. Combinable with manual selection — your call on

&#x20; exact interaction details (e.g. does re-clicking a rarity toggle deselect that tier, does manual

&#x20; deselection of one item from a filtered tier "break" that tier's toggle state) — flag your chosen

&#x20; behavior in the implementation summary.

\- Add a "Mass Salvage" confirm action that shows selected item count + total Dust reward, then performs

&#x20; the salvage for every selected item on confirm.

\- Equipped items must be excluded from being selected by any path (manual click, rarity filter) — your

&#x20; call whether to hide them from this particular list entirely or show them visibly disabled, but they

&#x20; must be structurally unselectable either way. Flag which you chose.

\- Update the selection/Dust-total display live as the player adds/removes items from the selection.



\## Out of Scope (do not implement)



\- Any change to per-item salvage Dust yield, the Forge, overflow handling, or any other Task 71/73

&#x20; behavior not directly about batching the salvage action.

\- Undo for a completed mass-salvage — treat it as final, consistent with single-item salvage today.

\- Sorting/filtering the inventory by anything other than the rarity quick-filter described above (e.g. no

&#x20; new slot-type filter) — keep this task scoped to the salvage workflow.



\## Acceptance Criteria



\- \[ ] Player can multi-select individual items in the Hub inventory and confirm a mass salvage.

\- \[ ] Player can quick-select by one or more rarity tiers, combinable with manual selection.

\- \[ ] Equipped items are never includable in any mass-salvage selection through any path.

\- \[ ] A single confirm step shows total item count and total Dust before committing, for any combination

&#x20;     of selection methods.

\- \[ ] Confirming awards the correct total Dust (sum of each selected item's rarity-based yield) and removes

&#x20;     all selected items from inventory.

\- \[ ] No change to single-item salvage, Forge, or overflow behavior.



\## Reviewer Notes



Flag as blocking if:

\- An equipped item can end up selected or salvaged through any path.

\- A rarity quick-filter action salvages immediately without going through the same confirm step as manual

&#x20; selection.

\- Per-item salvage yield logic was duplicated or altered instead of reused from Task 71.

