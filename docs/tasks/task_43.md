\# Task 43 — Apex \& Combo Apex Discovery Codex



> Read `CLAUDE.md` in full, and review Task 29/31 (Frost Warden apex talents), Task 35 (Bolt Striker apex

> talents), Task 38 (Frozen Lightning combo apex), and Task 12 (persistence pattern) before starting. This task

> adds a Vampire-Survivors-style discovery system: apex talents and combo apex talents are hidden/unknown the

> first time they're achievable, and become permanently revealed (including their exact unlock requirements) in

> a hub-scene Codex once the player first successfully unlocks them in any run.



\## Goal



The first time a player unlocks any given apex talent or combo apex talent (in any run, ever), that talent

becomes permanently "discovered" — recorded in persistent save data alongside gear/hero-slot progression. A new

Codex screen in the hub lets the player review all discovered talents, including which lines/apexes are required

to get them, so they can plan future runs around what they've learned. Undiscovered talents remain hidden/shown

only as "???" until first discovered. Additionally, when a talent is unlocked \*\*for the first time ever\*\* during

a run, the player gets an in-game notification distinct from any future unlock of that same talent (which would

just be the normal apex-ready indicator from Task 32).



\## Scope



\### 1. Persistent Discovery State

\- New persistent save data (same pattern as Task 12/42): a set of discovered talent IDs, covering both

&#x20; `ApexTalentDefinitionSO` (Task 29) and `ComboApexTalentDefinitionSO` (Task 38) entries.

\- The moment any apex or combo apex unlocks during a run (per its existing unlock-condition logic), check if its

&#x20; ID is already in the discovered set — if not, add it permanently and treat this as a "first discovery" event

&#x20; for that run (see UI below). If it's already discovered, the talent still unlocks/functions normally, just

&#x20; without the first-discovery notification.



\### 2. Hub Codex Screen

\- New hub-scene screen/panel ("Codex") listing every `ApexTalentDefinitionSO` and `ComboApexTalentDefinitionSO`

&#x20; that currently exists in the project.

\- For each entry:

&#x20; - If discovered: show its name, description, and its exact unlock requirement (e.g. "Frozen Ground Tier 3 +

&#x20;   Deepening Frost Tier 3" for a single-hero apex, or "Remorseless Winter + Lethal Surge" for a combo apex).

&#x20; - If not yet discovered: show only "???" — no name, description, or requirement revealed.

\- The Codex should iterate over whatever apex/combo apex assets exist rather than hardcoding entries, so future

&#x20; additions (new heroes' apexes, new combo apexes) appear automatically once they're discovered.



\### 3. In-Game First-Discovery Notification

\- When an apex or combo apex talent unlocks for the very first time ever (i.e. it was not already in the

&#x20; discovered set before this run), show a distinct in-game notification (e.g. a banner/popup naming the newly

&#x20; discovered talent), separate from the existing per-run apex-ready cooldown indicator (Task 32), since that

&#x20; indicator already communicates "this is now active this run" — this notification specifically communicates

&#x20; "you have permanently learned a new combo for future runs."

\- Subsequent unlocks of an already-discovered talent (in this or future runs) do not trigger this notification —

&#x20; only the existing Task 32 cooldown/active indicator applies as normal.



\## Out of Scope (do not implement)

\- Hero slot unlock progression (Task 42, separate)

\- Any visual/animation polish beyond a clear, readable notification and Codex entry

\- Retroactively crediting discoveries for talents the player may have already unlocked in past runs before this

&#x20; system existed — starting fresh from an empty discovered set is acceptable, flag if this is a concern



\## Acceptance Criteria

\- \[ ] Discovered talent IDs persist across runs using the existing save-data pattern

\- \[ ] Codex screen lists all apex/combo apex talents, showing full detail for discovered ones and "???" for

&#x20;     undiscovered ones, with no hardcoded entry list (new talents appear automatically)

\- \[ ] First-ever unlock of any apex/combo apex triggers a distinct first-discovery notification and permanently

&#x20;     records it as discovered

\- \[ ] Subsequent unlocks of an already-discovered talent do not re-trigger the first-discovery notification

\- \[ ] Codex updates immediately after returning to hub following a run with a new discovery

\- \[ ] No SO asset is mutated at runtime



\## Reviewer Notes

Flag as blocking if:

\- Codex hardcodes specific talent entries instead of iterating over existing SO assets

\- First-discovery notification fires on every unlock instead of only the first time ever

\- Discovery state is tracked per-run instead of persisting permanently

