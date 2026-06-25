\# Task 41 — HUD: Display Current Wave



> Read `CLAUDE.md` in full, and review Task 22/40 (in-game stat HUD) before starting. Small, standalone addition.



\## Goal



Add the current wave number to the in-game HUD, alongside the existing XP and Currency display, so the player can

see run progress at a glance. This is needed as groundwork for the wave-based progression tasks that follow.



\## Scope



\- Add a "Wave X" (or similar) label to the HUD (Task 22/40's repositioned right-side panel), reading the live

&#x20; current wave value from whatever existing source `WaveSpawner`/`DifficultyTierSO` progression already tracks —

&#x20; no new wave-tracking state, just a UI read of existing data.

\- Position it clearly alongside XP/Currency, consistent with the panel's existing layout conventions from Task 40.



\## Acceptance Criteria

\- \[ ] HUD displays the current wave number, updating correctly as waves progress

\- \[ ] No new wave-tracking logic introduced — reads existing `WaveSpawner` state

\- \[ ] No layout regression to the Task 40 HUD fix

