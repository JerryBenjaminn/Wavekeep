\# Task 81 — Full-Run Balance Validation \& Tuning (Post-Task-80 Audit)



\## Instructions to Claude Code



\- Read `CLAUDE.md` in full before starting.

\- Read this whole task file before doing anything.

\- Read `audit\_001.md`, `shop\_balance\_analysis\_002.md`, and the Task 63, 64, 78, 79, 80

&#x20; implementation summaries before starting — this task builds on all of them.

\- \*\*This is an analysis-first, tuning-second task.\*\* Produce a written report first (§2), then

&#x20; implement only the fixes explicitly listed in §3. Do not expand scope into enemy content,

&#x20; audio, or UI.

\- Flag ambiguity instead of guessing on anything not explicitly resolved below.

\- End with an implementation summary covering what was changed and what needs Jerry's playtest

&#x20; validation.



\## 1. Context



Task 80 removed the shop's stat-boosting potions — which audit\_002 identified as the dominant

late-game power source (a base-10 hit reaching \~1,000 via uncapped crit compounding). The

shop now provides one-wave utility effects only. No balance validation has been run since

Task 80 shipped. In-run power now comes only from:

\- Ability upgrade lines (capped at T3 per line)

\- Apex talents (2 per hero + combo apexes in dual-hero runs)

\- Persistent gear (gear affixes: Damage/Cooldown/Luck only — Range is effectively dead)



Enemy HP scales up to ×7 by wave 60. The hypothesis is that late game (waves 30–60) may

have flipped from trivially easy to likely unbeatable on a fresh save.



Additionally, two open flags from the Fable 5 full-project analysis were never resolved:

\- \*\*Card-picker pool exhaustion:\*\* what happens at wave 45+ when all upgrade lines are T3

&#x20; and both apexes are taken? If the card picker has no valid candidates, it may present

&#x20; empty cards, crash, or silently break the level-up flow.

\- \*\*Task 36 shared UpgradeInventory flag:\*\* dual-hero runs share one upgrade pool, meaning

&#x20; both heroes' TagInteractionRules read the same pooled upgrade set. It was flagged at

&#x20; Task 36 but never confirmed resolved — verify the current behavior and document it.



\## 2. Analysis deliverable (write this first, before touching any values)



Produce a written report covering:



1\. \*\*Post-Task-80 DPS vs. enemy HP curve:\*\* For a typical wave 1, 15, 30, 45, 60 enemy,

&#x20;  calculate or estimate expected hero DPS (using current ability values + T3 upgrade

&#x20;  lines + a representative gear set at Common, Rare, and Epic tier) against current

&#x20;  enemy HP at each checkpoint. Identify where, if anywhere, the curve flips to

&#x20;  unbeatable on a fresh save (no gear) vs. a geared save (full Epic loadout).



2\. \*\*Wall pressure assessment:\*\* With stat-boost potions gone, does the current wall HP,

&#x20;  enemy damage, and boss damage create meaningful pressure at checkpoints (waves 5, 10,

&#x20;  15, 20, 30, 40, 50, 60)? Or is wall survival trivial/impossible at any point?

&#x20;  Reference Task 80's self-flagged placeholder values for boss utility items.



3\. \*\*Shop utility relevance:\*\* Given the current wall pressure findings, do the 7 utility

&#x20;  items (Repair, Reinforced Repair, Barricade, Aegis, Tar Field, Glacial Choke, Flash

&#x20;  Freeze) create meaningful picks at boss waves, or does wall pressure being too low/high

&#x20;  make every pick feel irrelevant or mandatory?



4\. \*\*Card-picker pool exhaustion:\*\* Trace the card-picker logic (Task 07's

&#x20;  `LevelUpCardPicker`). What does it currently do when the upgrade pool is exhausted?

&#x20;  Document the exact behavior — does it crash, present empty cards, loop, or have a

&#x20;  defined fallback? Identify approximately which wave this occurs on given the current

&#x20;  XP curve (Task 63's quadratic scaling).



5\. \*\*Task 36 UpgradeInventory flag:\*\* Read the current dual-hero upgrade flow. Is the

&#x20;  shared pool intentional (both heroes benefit from all upgrades drawn) or a bug (one

&#x20;  hero's upgrade pick shouldn't affect the other's tag interactions)? Document current

&#x20;  behavior factually — don't fix it yet, just confirm what it does.



6\. \*\*Concrete tuning recommendations:\*\* Based on findings 1–3, list specific changes to

&#x20;  fix the curve — e.g. checkpoint StatMultiplier adjustments, wall HP changes, boss

&#x20;  damage changes, shop item magnitude/duration changes. Be specific: "reduce wave 30

&#x20;  checkpoint multiplier from X to Y" not "nerf the mid-game."



\## 3. What to implement (after the report is reviewed — implement all of these)



After producing the §2 report, implement the following. Do not wait for Jerry's sign-off

between report and implementation — the report IS the sign-off checklist for these

specific items:



\### 3a. Card-picker pool exhaustion fix

\- If the card-picker currently crashes, presents empty cards, or has undefined behavior

&#x20; when the upgrade pool is exhausted: fix it. Acceptable fallbacks in priority order:

&#x20; (1) offer a small flat stat bonus (Damage/Cooldown) as a filler card so the level-up

&#x20; flow never breaks, (2) skip the level-up screen entirely if truly nothing is available.

&#x20; Do not add new upgrade content — this is a robustness fix, not a content task.

\- Document the fix and chosen fallback in the implementation summary.



\### 3b. Task 36 UpgradeInventory behavior — document only, do not change

\- Write a clear one-paragraph description of what the current dual-hero shared pool

&#x20; actually does and whether it matches CLAUDE.md's intent. Flag as a known design

&#x20; decision (if intentional) or a known bug (if not), so it can be resolved in a future

&#x20; task. Do not change the behavior in this task.



\### 3c. Post-Task-80 balance tuning

\- Apply the concrete tuning recommendations from §2.6 — checkpoint multipliers, wall HP,

&#x20; boss damage, shop item values — to bring the wave 1–60 curve to a beatable-but-

&#x20; challenging state on both fresh save and a full-Epic-gear save.

\- All tunable values must remain in their existing SOs/configs (WaveConfigSO,

&#x20; DifficultyTierSO, GearEconomyConfigSO, ConsumableDefinitionSO) — no hardcoded values.

\- Do not touch ability values, upgrade line values, or gear affix ranges — those are

&#x20; hero-balance work that belongs in a future task.



\## Out of scope (do not implement)



\- Enemy variety or new enemy types — Task 82.

\- Hero animator parity — Task 84.

\- Audio content — Task 85.

\- Currency sink — deferred pending a future design decision.

\- RangeMultiplier split or Legs/Farsight affix remap — deferred (Fable 5 analysis noted

&#x20; a faster fix: swap the Legs implicit and pull Farsight from the pool as a data-only

&#x20; change; flag this as a one-hour task but do not do it here).

\- Task 36 fix — document only, fix deferred.



\## Acceptance Criteria



\- \[ ] §2 report produced covering all six points before any implementation.

\- \[ ] Card-picker no longer crashes, presents empty cards, or has undefined behavior when

&#x20;     the upgrade pool is exhausted on a long run.

\- \[ ] A fresh-save run (no gear) can reach at least wave 30 with reasonable effort.

\- \[ ] A geared-save run (full Epic loadout) can clear wave 60 without trivial

&#x20;     steamrolling — checkpoint walls should feel like walls, not instakills.

\- \[ ] Task 80's shop utility picks feel meaningful at boss waves — wall pressure exists

&#x20;     but is not so severe that every pick is forced (always repair) or irrelevant

&#x20;     (wall always full).

\- \[ ] All tuned values remain in SO/config assets, none hardcoded.

\- \[ ] Task 36 UpgradeInventory behavior is documented clearly in the implementation

&#x20;     summary with a design-decision-or-bug verdict.



\## Reviewer Notes



Flag as blocking if:

\- Tuning was applied without first producing the §2 analysis report.

\- Any tuned value was hardcoded outside its SO/config.

\- Ability values, upgrade line values, or gear affix ranges were changed.

\- The card-picker fix added new upgrade content instead of a robustness fallback.

