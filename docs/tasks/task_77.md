\# Task 77 — Developer Tool: Full Progression Reset



> Read `CLAUDE.md` in full before starting. Read this whole task file before coding.

> This is a developer-tooling task only — no gameplay logic, no new systems, no UI visible to players.

> This file describes outcomes, not code.



\## Goal



Add a Unity Editor menu command that wipes all persistent progression data back to a true first-launch

state, so the developer can test the new-player experience without manually hunting down save files.

This is a testing convenience tool, not a player-facing feature.



\## Scope



\- Add a menu item under `Wavekeep/` (e.g. `Wavekeep/Debug/Reset All Progression`) that when run:

&#x20; - Deletes or clears all persistent gear data (owned instances, loadouts, Salvage Dust) — the save

&#x20;   format introduced in Task 67 (v2).

&#x20; - Deletes or clears all meta-progression data (hero slot unlocks, any wave-clear records or unlock

&#x20;   thresholds from Tasks 61–64).

&#x20; - Resets any other cross-run persistent state the project currently saves to disk — investigate what

&#x20;   `SaveManager` (or equivalent) actually persists and make sure nothing is missed. List everything

&#x20;   that was found and reset in the implementation summary.

&#x20; - Logs clearly to the Console what was wiped and confirms completion (e.g. "Wavekeep: full

&#x20;   progression reset complete — gear save, meta-progression, \[X] cleared").

\- The command should work correctly whether run in Edit mode or Play mode, and should not require the

&#x20; game to be running to function.

\- If any save data is stored via `PlayerPrefs` in addition to file-based saves, clear the relevant keys

&#x20; too — don't assume everything is file-based.



\## Out of Scope (do not implement)



\- Selective/partial resets (e.g. "reset gear only" or "reset meta only") — one full wipe is enough

&#x20; for now; granular resets can be added later if needed.

\- Any player-facing reset option in-game.

\- Resetting Unity Editor settings, project settings, or SO asset values — only runtime save data.

\- Resetting in-run state (Currency, XP, current wave) — those are already per-run and don't persist.



\## Acceptance Criteria



\- \[ ] `Wavekeep/Debug/Reset All Progression` (or equivalent path) appears in the Unity menu.

\- \[ ] Running it wipes all persistent cross-run data: gear instances, loadouts, Dust, hero unlocks,

&#x20;     and any other found persistent state — confirmed by the implementation summary listing every

&#x20;     cleared data source.

\- \[ ] After running the command, launching the game presents a true fresh-start state with no gear,

&#x20;     no unlocked hero slots beyond the default, and zero Dust.

\- \[ ] A clear Console log confirms what was wiped.

\- \[ ] No gameplay code, save format, or SO asset was modified.



\## Reviewer Notes



Flag as blocking if:

\- Any known persistent data source was not cleared (e.g. PlayerPrefs keys left behind, a save file

&#x20; not deleted).

\- The tool modifies SO assets or project settings instead of only runtime save data.

