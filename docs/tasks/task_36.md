\# Task 36 — Dual-Hero Runtime Support (Part 1: Combat + Progression)



> Read `CLAUDE.md` in full, and review §3.5 (GameSession, no static singletons), Task 05 (HeroRuntime), Task 07

> (level-up flow), Task 21 (ultimate charge/cooldown), Task 29/31/35 (upgrade lines, apex talents), and Task 32

> (UI built to support 2+ simultaneous apex cooldown indicators) before starting. This task adds runtime support

> for two heroes fighting simultaneously in a single run. Hub-side team selection UI is a separate, later task —

> for this task, assume the active pair (Frost Warden + Bolt Striker) is hardcoded or set via a simple debug

> entry point, since the proper selection UI doesn't exist yet.



\## Goal



Support two heroes active simultaneously in combat, each acting autonomously (auto-attacking the nearest enemy

with their Basic ability, casting Ultimate on cooldown either automatically or on player command depending on a

toggle), sharing one Currency pool and one XP pool, drawing level-up cards from the combined set of both heroes'

upgrade lines, and each independently progressing toward their own apex talents.



\## Scope



\### 1. Dual HeroRuntime Instances

\- `GameSession` holds two `HeroRuntime` instances instead of one, assembled at run start from whichever two heroes

&#x20; are active for this run (Frost Warden + Bolt Striker for now, per the hardcoded/debug entry point above).

\- Each `HeroRuntime` ticks its own Basic/Ultimate/apex cooldowns independently — reuse the existing per-hero tick

&#x20; logic from Task 05/21/29, just instantiated twice rather than once. No shared mutable state between the two

&#x20; instances beyond what's explicitly designed below (currency, XP, level-up pool).



\### 2. Autonomous Combat Behavior

\- Both heroes auto-target and auto-attack with their Basic ability — implement a simple "nearest enemy in range"

&#x20; targeting rule for both, consistent with existing single-target/AoE range logic per hero's Basic ability.

\- Ultimate casting: add an \*\*auto-ultimate toggle\*\* (global, or per-hero — your call, document which) that, when

&#x20; enabled, casts that hero's Ultimate automatically as soon as it's off cooldown and a valid target exists; when

&#x20; disabled, Ultimate requires explicit player input as it does today for a single hero. Propose the clearest

&#x20; implementation for two simultaneously-active heroes (e.g. one input casts "next ready ultimate," or each hero

&#x20; has its own bound input) — flag your reasoning and chosen approach for review rather than guessing silently,

&#x20; since this is a new interaction pattern not covered by existing single-hero design.

\- Apex talents continue to trigger automatically on cooldown exactly as before (Task 29/31/35) — unaffected by the

&#x20; auto-ultimate toggle, which only concerns player-controlled Ultimates.



\### 3. Shared Currency and XP

\- `CurrencyManager` and `XPManager` (Task 03) remain single, shared instances under `GameSession` — both heroes'

&#x20; kills feed the same pools, exactly as the existing event-driven pipeline (`OnEnemyKilled` → currency/XP reward)

&#x20; already supports without modification, regardless of which hero landed the kill.

\- Shop (Task 06/09) continues to operate on the single shared Currency pool with no per-hero targeting needed —

&#x20; confirm this requires no changes, since consumable effects already apply to whichever `HeroRuntime` the

&#x20; consumable's effect logic is written to target (flag if any existing consumable effect assumes exactly one

&#x20; `HeroRuntime` exists and needs updating to target the correct hero or both).



\### 4. Combined Level-Up Card Pool

\- `LevelUpCardPicker` (Task 07/29) draws candidate lines from \*\*both\*\* active heroes' not-yet-maxed upgrade lines

&#x20; combined into one pool (16 lines total: 8 from Frost Warden, 8 from Bolt Striker), rather than one hero's 8.

\- Each card continues to display its skill-source badge (Task 32) — extend that badge/label so it's also clear

&#x20; \*which hero\* a card belongs to, not just Basic vs Ultimate (e.g. hero name or hero-colored accent in addition to

&#x20; the existing Basic/Ultimate badge), since the player now needs to distinguish 4 categories (Frost Warden Basic/

&#x20; Ultimate, Bolt Striker Basic/Ultimate) at a glance.

\- Each hero's lines and apex unlock conditions remain entirely independent — Frost Warden's apex unlock checks

&#x20; only Frost Warden's own line tiers, same for Bolt Striker, exactly as designed in Task 29.



\### 5. UI Accommodation

\- Extend the existing cooldown/charge UI (Task 21/32) to show both heroes' Ultimate charge state and both heroes'

&#x20; potential apex cooldown indicators simultaneously — Task 32 was already built to support at least 2 simultaneous

&#x20; apex indicators, so this should mostly be a matter of actually populating both rather than redesigning the

&#x20; display; flag if Task 32's layout needs adjustment to also fit two separate Ultimate charge bars (one per hero)

&#x20; alongside the apex indicators.



\## Out of Scope (do not implement)

\- Hub-scene team selection UI (choosing which two heroes to bring before a run) — separate task, this task assumes

&#x20; the pair is fixed/debug-selected

\- Hero-slot unlock progression (separate task)

\- Cross-hero combo apex talents (e.g. "Frozen Lightning") — still deferred, this task only proves two heroes can

&#x20; run side by side, not shared/combo abilities between them

\- Manual player movement/positioning control beyond whatever already exists — no new positioning mechanic for

&#x20; having two heroes on screen unless current single-hero positioning logic breaks with two present (fix only if

&#x20; broken, don't redesign)



\## Acceptance Criteria

\- \[ ] Two `HeroRuntime` instances run simultaneously under one `GameSession`, each with independent Basic/

&#x20;     Ultimate/apex cooldown state

\- \[ ] Both heroes auto-attack with their Basic ability against the nearest valid target

\- \[ ] Auto-ultimate toggle works as designed and documented; manual ultimate casting (toggle off) still functions

&#x20;     per-hero

\- \[ ] Apex talents continue triggering automatically and independently per hero, unaffected by the ultimate

&#x20;     toggle

\- \[ ] Currency and XP pools remain single and shared; both heroes' kills contribute correctly

\- \[ ] Level-up cards draw from the combined 16-line pool; each card clearly indicates both hero and skill source

\- \[ ] Each hero's apex unlock condition is evaluated only against that hero's own lines

\- \[ ] UI displays both heroes' Ultimate charge and any unlocked apex cooldowns simultaneously without breaking

&#x20;     layout

\- \[ ] No SO asset is mutated at runtime

\- \[ ] No static `Instance` patterns introduced

\- \[ ] Full playtest: run with Frost Warden + Bolt Striker active together, confirm both auto-attack

&#x20;     independently, confirm level-up cards draw from both heroes, confirm both heroes can independently reach

&#x20;     their own apex talents within one run



\## Reviewer Notes

Flag as blocking if:

\- A new static/global reference is introduced to let one hero's code reach the other instead of going through

&#x20; `GameSession`

\- Level-up draw silently favors one hero's lines over the other due to an implementation shortcut

\- Apex unlock logic for one hero accidentally checks the other hero's line progress

