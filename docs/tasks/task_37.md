\# Task 37 — Dual-Hero Runtime Support (Part 2: Hub Team Selection)



> Read `CLAUDE.md` in full, and review Task 14 (hub scene, equip-management UI), Task 25/26 (gear detail panel,

> as a UI pattern precedent for hub-side panels), and Task 36 (dual-hero runtime, currently hardcoded/debug-fixed

> to Frost Warden + Bolt Striker) before starting. This task replaces Task 36's hardcoded hero pair with a real

> hub-scene selection flow. Hero-slot unlock progression (gating how many heroes can be selected behind a wave

> milestone) is intentionally deferred — for now, all existing heroes are selectable with no unlock gate, but the

> selection data model should not assume exactly two heroes forever, since a future task will add a slot cap tied

> to progression.



\## Goal



Add a team-selection screen/panel to the hub scene where the player picks which heroes (from all heroes currently

in the project) to bring into the next run, and starts the run with that selection. The run then launches with

exactly the chosen heroes active, using Task 36's dual-hero runtime (generalized to N heroes rather than a fixed

pair of two).



\## Scope



\### 1. Selection UI

\- New hub-scene panel/screen listing all available heroes (currently Frost Warden and Bolt Striker, but the UI

&#x20; should iterate over whatever heroes exist rather than hardcoding two slots — new heroes added later should

&#x20; appear automatically with no UI code changes).

\- Each hero entry is selectable/deselectable (e.g. toggle button or checkbox-style card), showing at minimum the

&#x20; hero's name and icon (reuse whatever icon reference pattern already exists from hero-select, Task 05, or the

&#x20; gear panel's icon-slot pattern from Task 25 if no hero icon display currently exists — flag if hero icons

&#x20; aren't wired up yet and a placeholder is needed).

\- No maximum selection count is enforced yet (deferred to the future slot-unlock task) — but a \*\*minimum of 1\*\*

&#x20; hero must be selected to start a run; block run start with a clear message if zero are selected.

\- A "Start Run" action from this panel passes the selected hero set into the run, replacing Task 36's hardcoded

&#x20; pair.



\### 2. Runtime Generalization

\- Task 36's dual-`HeroRuntime` setup under `GameSession` should generalize to \*\*N\*\* `HeroRuntime` instances (where

&#x20; N is however many heroes the player selected), not remain hardcoded to exactly two. Verify autonomous combat,

&#x20; shared currency/XP, the combined level-up card pool, and the apex/ultimate UI from Task 36 all correctly scale

&#x20; to N instances rather than assuming exactly 2 — this may mean adjusting loop/array assumptions from Task 36 if

&#x20; any were written expecting a fixed pair.

\- A run with only 1 hero selected should work correctly (effectively today's original single-hero behavior, just

&#x20; arrived at through the new selection flow rather than a separate code path).



\### 3. Data Model for Future Slot-Gating

\- Structure the selection screen and underlying "which heroes exist / which are currently selectable" data so

&#x20; that a future task can add a `maxSelectableHeroes` value (increased via progression, e.g. wave milestones) and

&#x20; gate selection against it, without needing to redesign this screen — e.g. expose hero availability as a simple

&#x20; list/query the UI reads, rather than hardcoding "show exactly these heroes" in the UI layer itself. Document

&#x20; briefly where that future gate would plug in, but do not implement the gate itself in this task.



\## Out of Scope (do not implement)

\- Hero-slot unlock progression / wave-milestone gating (separate, future task — this task explicitly leaves all

&#x20; heroes selectable with no cap)

\- Cross-hero combo apex talents

\- Equip/gear management changes (Task 14/25/26 remain as-is; this is a separate screen)



\## Acceptance Criteria

\- \[ ] Hub scene has a team-selection panel listing all existing heroes, each independently toggleable

\- \[ ] Newly added heroes (in future tasks) appear in this list automatically with no UI code change required

\- \[ ] At least 1 hero must be selected to start a run; clear feedback if the player tries to start with zero

\- \[ ] Selected hero set correctly launches into the run, replacing Task 36's hardcoded pair

\- \[ ] Dual/multi-hero runtime (combat, shared currency/XP, combined level-up pool, UI) correctly generalizes to N

&#x20;     selected heroes, tested with both 1 hero and 2 heroes

\- \[ ] Underlying data model exposes hero availability in a way a future slot-cap gate can plug into without

&#x20;     reworking this screen (documented, not implemented)

\- \[ ] No SO asset is mutated at runtime

\- \[ ] No static `Instance` patterns introduced

\- \[ ] Full playtest: select only Frost Warden, run, confirm correct single-hero behavior; select both heroes, run,

&#x20;     confirm Task 36's dual-hero behavior still works arrived at through this new screen



\## Reviewer Notes

Flag as blocking if:

\- Hero count is still hardcoded/assumed as exactly 2 anywhere in the runtime/UI path

\- New heroes added later would require UI code changes to appear in the selection list

\- Run can be started with zero heroes selected

