\# Task 39 — Bugfix: Gear Detail Panel Fails to Open After Task 38



> Read `CLAUDE.md` in full, and review Task 25/26 (gear detail panel) and Task 38 (Frozen Lightning combo apex)

> before starting. This is a regression investigation — Task 38 has no direct logical connection to the gear

> panel, so the root cause is likely an indirect side effect (e.g. a shared event, an input-routing change, a

> silent exception breaking initialization, or an editor/compile issue), not an intentional logic change.



\## Bug



Clicking a gear/artifact item in the hub scene (inventory or equipped slot) no longer opens the gear detail panel

(Task 25/26). This worked correctly before the Task 38 implementation and broke afterward, with no gear-related

code intentionally touched in Task 38.



\## Investigation Steps (do first, before attempting any fix)



1\. Check the Unity console for any compile errors, runtime exceptions, or warnings when the hub scene loads or

&#x20;  when a gear item is clicked — a silent exception during panel initialization or the click handler is the most

&#x20;  likely cause and should be checked first.

2\. Diff or review what Task 38 actually changed — even though it targeted apex/combo logic, check whether it

&#x20;  touched anything shared/global (e.g. `EventBus` subscription patterns, `GameSession` initialization order,

&#x20;  any UI-input-routing code, or any base class/interface that the gear panel's click handling also relies on).

3\. Confirm whether the hub scene itself still loads correctly and other hub UI (equip slots rendering, inventory

&#x20;  list rendering) still functions — narrow down whether this is isolated to the panel's open action specifically,

&#x20;  or a broader hub-scene issue that happens to be most visible here.

4\. Check whether the click handler that's supposed to open the panel (Task 25) is even being invoked at all

&#x20;  (e.g. add a temporary debug log if needed during investigation) versus being invoked but failing silently

&#x20;  inside the panel's own open/populate logic.



\## Scope



\- Identify and fix the root cause so the gear detail panel reliably opens on click, exactly as it did before

&#x20; Task 38, with no regression to Equip/Unequip button behavior (Task 25) or stat comparison (Task 26).

\- Do not modify any Task 38 (Frozen Lightning) gameplay logic unless the investigation concretely shows that code

&#x20; is the cause — if Task 38 is confirmed as the cause, fix the interaction without removing or weakening Task 38's

&#x20; intended behavior.



\## Out of Scope (do not implement)

\- Any new gear panel features

\- Any balance changes to Frozen Lightning or other apex talents



\## Acceptance Criteria

\- \[ ] Root cause identified and documented in the report (not just "fixed it," explain what was actually broken)

\- \[ ] Clicking any inventory item or equipped slot reliably opens the gear detail panel again

\- \[ ] Equip/Unequip buttons and stat comparison (Task 25/26) function correctly after the fix

\- \[ ] Task 38's Frozen Lightning behavior is confirmed still working correctly after the fix

\- \[ ] No new static `Instance` patterns introduced as part of the fix



\## Reviewer Notes

Flag as blocking if:

\- The fix papers over the symptom (e.g. wrapping the click handler in a try/catch that swallows the real error)

&#x20; without identifying and addressing the actual root cause

\- Task 38's combo apex behavior is broken or weakened as a side effect of the fix

