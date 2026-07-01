\# Task 72 — Diagnose: GearDebugController Inputs Not Firing



> Read CLAUDE.md in full before starting. Read this whole task file before doing anything.



\## Instructions to Claude Code



\- This is a diagnosis-and-fix task, not a feature task. Don't expand scope into Hub UI work.

\- Read the full task before coding. Flag ambiguity instead of guessing.

\- End with a summary of root cause found and what was fixed.



\## Problem



Jerry ran the `Wavekeep/Setup Task 71 (Gear Economy)` menu command and saved the scene, in both the

gameplay scene and the Hub scene. In Play mode, pressing S/F/O/K (the Task 71 `GearDebugController`

debug keys) produces \*\*no effect and no Console output at all\*\* — not even an error or warning. This is

true in both scenes.



\## Investigate



Total silence (not even a log line) suggests the input is never reaching the handler at all, rather than

the handler running and failing silently. Check, in order:



1\. \*\*Is `GearDebugController` present, enabled, and active in the scene hierarchy\*\* in both scenes that

&#x20;  were tested? Confirm the GameObject and component are active, not just present in the asset.

2\. \*\*Input System vs legacy `Input` class mismatch.\*\* Per CLAUDE.md §3.6, the project uses Unity's Input

&#x20;  System package with an abstraction layer for gameplay input. Check what `GearDebugController` actually

&#x20;  uses to read S/F/O/K — if it uses the legacy `Input.GetKeyDown` API but the project's Player Settings

&#x20;  "Active Input Handling" is set to "Input System Package (New)" only, \*\*legacy Input calls are silently

&#x20;  no-ops\*\* and this exact symptom (total silence, no errors) occurs. This is the most likely cause given

&#x20;  the symptom.

3\. \*\*Focus / EventSystem stealing input\*\*, if any UI Canvas with an `EventSystem` is capturing keyboard

&#x20;  focus and the debug controller's input read happens in a context where it doesn't receive events.

4\. \*\*Script execution order / disabled by another system\*\* — confirm nothing disables the component at

&#x20;  runtime (e.g. a Hub/gameplay mode toggle).

5\. Confirm `\_gearEconomyConfig`/`\_gearAffixConfig` are actually wired on the relevant scene's

&#x20;  `GameSessionBootstrap` (per Task 71's flag #3) — note this would cause salvage/forge to no-op, but

&#x20;  should NOT cause total silence/no logs if the input itself is being read; rule this in or out separately

&#x20;  from the input issue.



\## Fix



\- If it's an Input System mismatch: bring `GearDebugController`'s key reads in line with however the rest

&#x20; of the project reads input (per CLAUDE.md §3.6's input abstraction), rather than introducing a one-off

&#x20; legacy `Input` usage. Don't change the project's global Input Handling setting unless that's clearly the

&#x20; intended fix — flag if changing that setting would affect other systems.

\- If it's something else entirely, fix the actual root cause and document it.



\## Acceptance Criteria



\- \[ ] Root cause identified and stated clearly in the implementation summary (not just "it works now").

\- \[ ] Pressing S/F/O/K in Play mode in the gameplay scene produces visible Console output and the expected

&#x20;     salvage/forge/overflow/log behavior from Task 71.

\- \[ ] Same confirmed in the Hub scene (independent of the separate, already-flagged config-wiring gap —

&#x20;     if config wiring is also missing in the Hub scene, note it but that's expected per Task 71's flag #3,

&#x20;     not something to fix in this task).

\- \[ ] No change to gameplay-critical input handling beyond what's needed to fix the debug controller.



\## Reviewer Notes



Flag as blocking if:

\- The fix changes global Input System project settings without flagging the impact on other input

&#x20; consumers.

\- The root cause wasn't actually identified (e.g. "added more logging and it started working" without

&#x20; explaining why).

