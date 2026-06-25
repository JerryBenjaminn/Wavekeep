\# Task 42 — Meta-Progression: Hero Slot Unlocks via Wave Clears



> Read `CLAUDE.md` in full, and review Task 12 (gear/artifact persistence, as the precedent for cross-run saved

> progress) and Task 37 (hub team-selection panel, currently allowing unlimited hero selection) before starting.

> This task adds the persistent, cross-run meta-progression layer that gates how many hero slots are selectable

> in Task 37's panel, replacing its current uncapped behavior.



\## Goal



Hero slots unlock permanently (saved across runs, same persistence model as gear/artifact ownership) based on

clearing wave milestones in a single run:

\- Slot 1: always unlocked (default)

\- Slot 2: unlocks after clearing wave 15 in one run

\- Slot 3: unlocks after clearing wave 30 in one run

\- Slot 4: unlocks after clearing wave 50 in one run



"Clearing" a wave milestone means the player must reach and survive past that wave within a single continuous run

(not cumulative across multiple runs) — e.g. reaching wave 15 and dying immediately after does still count if the

wave itself was cleared; dying at wave 14 does not unlock slot 2 that run, but a later run reaching wave 15 will.



\## Scope



\### 1. Persistent Unlock State

\- New persistent save data (alongside existing gear/artifact JSON save from Task 12): tracks which hero slot

&#x20; unlocks have been achieved (e.g. `maxUnlockedHeroSlots`, an integer 1–4, or a per-milestone boolean set —

&#x20; implementer's choice, document it).

\- On run end (win or loss), check the highest wave cleared during that run against the milestone thresholds

&#x20; (15/30/50) and permanently raise `maxUnlockedHeroSlots` if a new milestone was reached, regardless of whether

&#x20; the run was ultimately won or lost overall — reaching wave 16 and then dying still counts as having cleared

&#x20; wave 15.



\### 2. Hub Team-Selection Gating

\- Task 37's team-selection panel now enforces a maximum selection count equal to the current

&#x20; `maxUnlockedHeroSlots` value, instead of allowing unlimited selection.

\- Heroes beyond the currently unlocked slot count should be visibly present but clearly locked/disabled in the UI

&#x20; (not hidden entirely) — e.g. grayed out with a "Reach wave 15/30/50 to unlock" style label — so the player

&#x20; always sees what's coming next as a goal.

\- If `maxUnlockedHeroSlots` increases mid-session (e.g. player just finished a run that unlocked slot 2 and

&#x20; returns to hub), the panel must reflect the new unlocked count immediately without requiring an app restart.



\## Out of Scope (do not implement)

\- Apex/combo-apex discovery codex (separate task)

\- Any change to hero roster size beyond the existing two heroes — slots 3/4 will simply have no third/fourth hero

&#x20; to assign yet; the unlock state should still track correctly even with fewer heroes than slots existing today

\- Any UI celebration/animation for unlocking a new slot beyond a clear, readable state change in the hub



\## Acceptance Criteria

\- \[ ] Hero slot unlock state persists across runs using the existing save-data pattern (Task 12)

\- \[ ] Slot 2/3/4 unlock correctly and permanently the first time a run clears wave 15/30/50 respectively, even if

&#x20;     that run is ultimately lost

\- \[ ] Hub team-selection panel enforces the current max selectable count and clearly shows locked slots with

&#x20;     their unlock requirement

\- \[ ] Newly unlocked slots are reflected in the hub immediately after returning from a run, no restart required

\- \[ ] Starting a run with only 1 hero (today's minimum) still works correctly regardless of unlock state

\- \[ ] No SO asset is mutated at runtime



\## Reviewer Notes

Flag as blocking if:

\- Slot unlocks are tracked as per-run-only state instead of persisting across runs

\- A run must be won outright (rather than simply reaching the wave) to count as having cleared a milestone

