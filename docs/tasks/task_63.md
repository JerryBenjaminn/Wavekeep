\# Task 063 — Balance Tuning Pass: Difficulty Curve, Content Extension, Boss Identity, XP/Gear Tuning



> Status: Ready for implementation

> Depends on: Task 061 (Balance Audit report, /docs/balance/audit\_001.md), Task 062 (critical bug fixes — must be completed first, this task assumes GameTier is live and LootTable\_Regular drops correctly)



\---



\## Instructions to Claude Code (read first)



1\. Read CLAUDE.md in full before starting, even if read in a prior session.

2\. Read this entire task file before writing any code.

3\. Do not expand scope. Implement exactly what's specified; flag suggestions separately instead of acting on them.

4\. Flag, don't guess, on:

&#x20;  - Any mismatch between this task's assumptions (built on Task 061's audit findings) and current actual data.

&#x20;  - Any conflict with locked decisions in CLAUDE.md §2/§3.

&#x20;  - If a target formula below produces an obviously broken result somewhere in the curve (e.g. negative values, divide-by-zero at wave 1) — flag and propose a fix rather than implementing broken math.

5\. Do not touch ScriptableObject template assets at runtime — this is editor-time data tuning, not runtime mutation.

6\. At the end of your response, summarize all changed values/curves in a short table, and confirm the game still runs without errors across the new wave range.



\---



\## 0. Context



Task 061's audit identified the wave difficulty curve as non-monotonic and unintentional, content stopping around wave 20 (leaving hero-slot unlocks at wave 30/50 unreachable), EvilGod's stats matching basic trash enemies, a front-loaded XP curve, and a shallow gear rarity spread. This task implements deliberate fixes for all of these based on developer-approved design direction: \*\*checkpoint-style difficulty spikes\*\* (not a smooth curve), \*\*content extended further\*\* (not unlock thresholds compressed), and EvilGod redesigned as a \*\*slow, heavy-hitting tank boss\*\*.



\---



\## 1. Scope



\### 1.1 Wave Difficulty Curve — Checkpoint Spike Redesign



\- Replace the current non-monotonic `StatMultiplier` progression with a deliberate \*\*stepped/checkpoint curve\*\*: difficulty should ramp gradually within a "block" of waves, then jump noticeably at specific checkpoint waves, then resume gradual ramping at the new (higher) baseline. No wave-to-wave value should ever decrease — monotonic non-decreasing growth is a hard requirement here, since the audit found drops between consecutive waves and that must not recur.

\- Suggested structure (adjust precisely as needed to fit existing curve-evaluation code, but preserve this shape): gradual +X% per wave within a block, with a larger step-multiplier applied at each checkpoint wave (e.g. every 5th wave, consistent with the existing "boss every 5 waves" cadence already in place per the audit). Checkpoint waves should feel like a deliberate spike (noticeably harder than the wave immediately before), not a continuation of the gradual ramp.

\- Extend this curve out to \*\*wave 60\*\* (see §1.2 for why), ensuring the formula doesn't blow up or produce absurd numbers at the high end — sanity-check the projected stat values at waves 30, 45, and 60 specifically and flag if any look unreasonable relative to wave 1–20's existing values.



\### 1.2 Content Extension to Support Meta-Progression



\- Hero-slot unlocks at wave 15/30/50 currently can't be reached because content stops \~wave 20. Extend `GameTier`'s wave list so meaningful, playable content exists through at least \*\*wave 55-60\*\* — this doesn't require new enemy types or new mechanics, but does require the existing `WaveConfigSO` progression (enemy composition, spawn rate/timing) to continue scaling sensibly across the extended range using the existing Skeleton/EvilGod roster, consistent with the new curve from §1.1.

\- This is primarily a data-extension task (more `WaveConfigSO` entries following the established pattern), not a new-systems task — flag if you find the current wave-authoring approach doesn't scale cleanly to "just add more entries" (e.g. if it's been hand-tuned per wave with no reusable pattern) rather than force-fitting a bad pattern further.



\### 1.3 EvilGod Boss — Full "Slow Heavy Tank" Identity Pass



Building on Task 062's stopgap fix, give EvilGod a deliberate identity:

\- \*\*Movement speed\*\*: reduce to noticeably slower than Skeleton's (e.g. \~50-60% of Skeleton's speed) — the boss should read as lumbering/heavy, not just a bigger Skeleton.

\- \*\*HP\*\*: keep the existing \~200 HP baseline as the wave-1-equivalent boss appearance value, but ensure it scales with the same checkpoint curve from §1.1 at each subsequent boss appearance (every 5 waves).

\- \*\*Contact/attack damage\*\*: increase substantially relative to Skeleton — a heavy tank boss should hit hard enough that taking an unmitigated hit feels meaningfully punishing (suggest roughly 4-6x Skeleton's per-hit damage at equivalent wave, tune by feel if this is too high/low once tested in-game).

\- \*\*Currency yield\*\*: increase to reflect boss-kill significance — suggest roughly 8-12x a basic Skeleton's yield at equivalent wave, on top of its already-confirmed boss-exclusive Legendary/Unique loot table access.

\- Do not change EvilGod's attack animation timing/Animator setup (Task 055) — stat values only.



\### 1.4 XP Curve Flattening



\- The audit found the player reaching level 6-7 in wave 1 against a linear `10 + 5·level` curve — this front-loads progression too fast. Replace with a curve that grows faster at higher levels (e.g. a mild exponential or quadratic curve such as `10 + 5·level + 2·level²`, or equivalent — pick a concrete formula, document it, and project it across levels 1-20 in your summary so the shape is verifiable) so early levels still come quickly (maintaining early-game satisfaction) but the curve doesn't let the player blow through 6+ levels in a single early wave.

\- Cross-check this against §1.1/1.2's extended wave range — the XP curve and the extended content length should result in a reasonable number of total level-ups across a full run (developer to sanity-check by playtesting after this task, not a hard number to hit now).



\### 1.5 Gear Rarity Spread



\- Current spread (Common 1.1× → Unique 1.6× across six tiers) is too shallow per the audit. Widen the spread so each tier feels like a meaningful step up — suggest a curve roughly like: Common 1.0×, Uncommon 1.15×, Rare 1.35×, Epic 1.6×, Legendary 2.0×, Unique 2.6× (adjust precisely as needed, but the key requirement is each successive tier should feel like a clear, not marginal, upgrade — roughly 15-30% relative jump between adjacent tiers, growing larger at the top end).

\- Resolve the audit's flagged ambiguity about multiplicative vs. additive stacking across a full 6-slot Legendary loadout — confirm and document which stacking model is actually implemented, and report the resulting full-Legendary-loadout power multiplier explicitly in your summary so the developer can sanity-check whether it's reasonable (the audit found this could swing between \~3x and \~12x depending on stacking method, which is a large enough spread that it needs to be explicitly confirmed, not left ambiguous).



\### 1.6 LootTable\_Regular Weight Distribution (Refinement)

\- Task 062 applied an even-distribution placeholder fix to the previously-broken all-zero weights. This task should replace that placeholder with a deliberate weighting that favors lower rarities more heavily for regular (non-boss) enemies — e.g. a steep falloff such as Common 60%, Uncommon 25%, Rare 10%, Epic 4%, Legendary 1%, Unique 0% (Unique remains boss-exclusive per existing locked design). Adjust precisely as needed but the direction (heavily common-weighted for regular enemies) is the requirement.



\---



\## 2. Out of Scope



\- New enemy types, new abilities, new mechanics — this task only tunes/extends existing systems' numbers and wave-data entries.

\- Settings/UI changes.

\- Further model/VFX/audio work.

\- Hero-slot unlock wave thresholds themselves (15/30/50 stay as-is per developer's "extend content" decision) — only the content reaching that far is in scope.



\---



\## 3. Acceptance Criteria



\- \[ ] `StatMultiplier` (or equivalent) progression is monotonic non-decreasing across the full extended wave range, with clear checkpoint spikes at boss waves (every 5th wave) rather than a smooth curve.

\- \[ ] `GameTier`'s wave list extends through at least wave 55-60 with sensible enemy composition scaling, using existing Skeleton/EvilGod roster.

\- \[ ] EvilGod boss stats updated per §1.3 (speed, HP scaling, damage, currency) and clearly distinct from Skeleton's profile at every wave it appears.

\- \[ ] XP curve replaced with a documented, faster-than-linear-growth formula; old vs. new curve compared in the response summary across levels 1-20.

\- \[ ] Gear rarity tier multipliers widened per §1.5; stacking model (multiplicative vs. additive) explicitly confirmed and reported with the resulting full-Legendary 6-slot power multiplier.

\- \[ ] `LootTable\_Regular` weights replaced with a deliberate common-favoring distribution, Unique still boss-exclusive.

\- \[ ] Game runs without errors across the full new wave range (spawner doesn't break, no null refs from new wave entries).

\- \[ ] Response summary includes a concise table of all key before/after values changed in this task.

