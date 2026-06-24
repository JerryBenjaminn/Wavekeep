\# Task 28 — Bugfix: Ultimate Charge Accrues During Shop/Level-Up Pause



> Read `CLAUDE.md` in full, and review Task 21 (ultimate resource/charge bar) and whatever existing pause

> mechanism the shop (Task 06/09) and level-up flow (Task 07) already use to halt gameplay while waiting on player

> input.



\## Bug



The ultimate charge bar continues to fill while the Shop UI or Level-Up card UI is open. Both of these moments

already pause general gameplay (enemies/wave progression halt while waiting on a player choice), but ultimate

charge accrual is not respecting that same pause state — it keeps ticking regardless of whether waves are

currently active.



\## Expected Behavior



Ultimate charge should only accrue while a wave is actively in progress. It must not increase while:

\- The Shop UI is open

\- The Level-Up card selection UI is open

\- Any other state where wave/enemy progression is already halted (if such a state exists — check for it)



\## Investigation Step (do first)



Before fixing, identify how the existing pause behavior is implemented — i.e. what gates `WaveSpawner`/enemy

movement during Shop/Level-Up. Determine whether there is already a single shared pause flag/event (e.g. on

`GameSession` or via `EventBus`) that other systems already check, or whether Shop/Level-Up each pause things

locally without a shared signal.



\- If a shared pause signal already exists: have ultimate charge accrual check/subscribe to that same signal,

&#x20; consistent with how other paused systems already behave. This is the preferred fix.

\- If no shared signal exists (each pause point currently halts things ad hoc/locally): introduce one shared

&#x20; "gameplay active" flag or event on `GameSession`/`EventBus` that Shop-open and Level-Up-open both set, and have

&#x20; ultimate charge accrual (and ideally any other time-based system that should also be pausing but currently isn't)

&#x20; read from it. Flag in your report if other systems are found to have the same bug, but only fix ultimate charge

&#x20; in this task unless told otherwise.



\## Scope



\- Fix ultimate charge accrual to stop while Shop or Level-Up UI is open, resuming correctly when the player closes

&#x20; either and waves resume.

\- Do not change ultimate charge accrual rate, cap, or any other ultimate-related balance value — this is a pause

&#x20; bug, not a balance change.



\## Out of Scope (do not implement)

\- Fixing other systems found to have the same pause-respecting bug (report them, but don't fix unless asked)

\- Any change to how the Shop or Level-Up UI itself opens/closes/pauses gameplay



\## Acceptance Criteria

\- \[ ] Ultimate charge bar does not increase while Shop UI is open

\- \[ ] Ultimate charge bar does not increase while Level-Up card UI is open

\- \[ ] Ultimate charge accrual resumes correctly once the player closes either UI and waves are active again

\- \[ ] No change to charge rate, cap, or ultimate ability behavior itself

\- \[ ] Report (in PR/summary) whether any other time-based system was found with the same unaddressed pause bug



\## Reviewer Notes

Flag as blocking if:

\- Fix is implemented as a special-case check duplicated in the ultimate charge code instead of using/establishing

&#x20; a shared pause signal

\- Any balance value (charge rate, cap) was changed as part of this fix

