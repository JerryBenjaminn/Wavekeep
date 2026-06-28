\# Task 064 — Fix: EvilGod Wave-5 Boss Damage Too High for Early-Game Power Level



> Status: Ready for implementation

> Depends on: Task 063 (balance tuning pass — §1.1 checkpoint curve, §1.3 EvilGod damage multiplier)



\---



\## Instructions to Claude Code (read first)



1\. Read CLAUDE.md in full before starting, even if read in a prior session.

2\. Read this entire task file before writing any code.

3\. Do not expand scope. Implement exactly what's specified; flag suggestions separately instead of acting on them.

4\. Flag, don't guess, on:

&#x20;  - The actual computed damage value EvilGod currently deals at wave 5, after both the checkpoint StatMultiplier (§1.1) and EvilGod's own per-hit damage multiplier (§1.3) are applied — report this number explicitly so the developer can see exactly how it compares to wall HP / typical early-game survivability.

&#x20;  - Any conflict with locked decisions in CLAUDE.md §2/§3.

5\. Do not touch ScriptableObject template assets at runtime.

6\. At the end of your response, summarize the before/after damage values at wave 5, and confirm wave 1-4 difficulty is unaffected by this fix.



\---



\## 0. Context / Pre-confirmed State



\- Developer playtested 5 full runs after Task 063. Wave 1-4 difficulty feels correctly tuned. Wave 5 boss (EvilGod's first appearance) deals lethal/excessive damage, killing the player quickly — confirmed as a damage problem specifically (not a DPS-shortfall/can't-kill-boss-in-time problem).

\- At this point in a run, the player has exactly one hero unlocked (hero slots 2-4 unlock at wave 15/30/50) and gear is still mostly Common-tier (per the developer's testing: \~5 runs in, still no Artifact-slot item, mostly Commons elsewhere).

\- The likely root cause: EvilGod's per-hit damage multiplier (4-6x Skeleton's damage, set in Task 063 §1.3) is being applied multiplicatively on top of the wave-5 checkpoint StatMultiplier (§1.1), compounding into a damage value far higher than intended for a single-hero, low-gear power level.



\---



\## 1. Goal



Reduce EvilGod's effective damage output at its first appearance (wave 5) to a survivable-but-still-threatening level for a single-hero, low-gear player, without softening the wave 1-4 trash-mob difficulty or removing the boss's intended "heavy hitter" identity entirely.



\## 2. Scope



\### 2.1 Diagnose Actual Compounding



\- Calculate and report the exact current wave-5 EvilGod per-hit damage value, showing both contributing factors separately (checkpoint StatMultiplier value at wave 5, and EvilGod's own damage multiplier), so it's clear how they compound.



\### 2.2 Fix Options (pick the cleanest, flag if a different one is needed)



Apply one of the following (prefer the first if it cleanly resolves the issue without further side effects):



\- \*\*Option A — Reduce EvilGod's own multiplier\*\*: lower EvilGod's per-hit damage multiplier from the current 4-6x range to something in the 2-3x range relative to Skeleton, re-tested against the same wave-5 checkpoint StatMultiplier, so the compounded result lands at a survivable-but-punishing level for a single under-geared hero.

\- \*\*Option B — Decouple boss damage from the full checkpoint multiplier\*\*: if Option A alone doesn't resolve it cleanly (e.g. the checkpoint multiplier itself is the larger contributor), consider applying boss-specific damage scaling on its own curve, separate from trash-mob `StatMultiplier`, so future boss waves (10, 15, 20...) don't inherit runaway compounding as the checkpoint curve grows. Flag this as a larger structural change if you go this route, since it affects how all future boss waves scale, not just wave 5.

\- Whichever option is used, the fix must hold up reasonably at later boss waves too (10, 15, 20) — don't just patch wave 5 in isolation; verify the same fix doesn't make wave 10/15/20 bosses trivially weak or still disproportionately strong. Report projected boss damage at waves 5, 10, 15, 20 in the summary.



\### 2.3 Preserve Boss Identity and Wave 1-4 Tuning



\- Do not touch trash-mob (Skeleton) damage/HP/StatMultiplier values for waves 1-4 — confirmed working correctly by the developer.

\- EvilGod should still feel meaningfully harder-hitting than Skeleton at the same wave — don't reduce the multiplier so far that the boss loses its "heavy tank" identity from Task 063 §1.3. The goal is survivable-but-threatening, not trivial.



\---



\## 3. Out of Scope



\- Wave 1-4 trash-mob tuning — confirmed correct, do not touch.

\- EvilGod's HP, currency yield, or movement speed — only the damage multiplier (and, if needed per Option B, its scaling curve) is in scope.

\- Any further content extension or XP/gear changes from Task 063 — unrelated to this fix.



\---



\## 4. Acceptance Criteria



\- \[ ] Wave-5 EvilGod's effective per-hit damage is reported with exact before/after numbers, showing both contributing factors.

\- \[ ] New damage value results in a survivable encounter for a single-hero, low/no-gear player at wave 5 (developer to confirm via playtesting after this fix).

\- \[ ] Boss damage scaling checked and reported at waves 5, 10, 15, 20 to confirm no runaway compounding recurs at later boss waves.

\- \[ ] Wave 1-4 trash-mob values unchanged.

\- \[ ] EvilGod still reads as meaningfully harder-hitting than Skeleton at equivalent waves — not trivialized.

