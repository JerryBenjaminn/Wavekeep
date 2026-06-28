\# Task 062 — Balance Audit Fixes: Critical Bugs + Cleanup



> Status: Ready for implementation

> Depends on: Task 061 (Balance Audit report, /docs/balance/audit\_001.md)



\---



\## Instructions to Claude Code (read first)



1\. Read CLAUDE.md in full before starting, even if read in a prior session.

2\. Read this entire task file before writing any code.

3\. Do not expand scope. Implement exactly what's specified; flag suggestions separately instead of acting on them.

4\. Flag, don't guess, on:

&#x20;  - Any mismatch between this task's assumptions and what you actually find in the project.

&#x20;  - Any conflict with locked decisions in CLAUDE.md §2/§3.

&#x20;  - If removing an "orphan" asset turns out to have a reference you didn't expect, stop and flag rather than force-deleting.

5\. Do not touch ScriptableObject template assets at runtime (n/a here, this is editor-time data cleanup, not runtime mutation).

6\. At the end of your response, summarize what was fixed/removed, and confirm the live game now actually runs on the intended tier.



\---



\## 0. Context



Task 061's audit found several issues that are bugs rather than balance decisions — these should be fixed before any deliberate balance tuning happens, since tuning the wrong (or broken) systems would be wasted effort. This task addresses only the items explicitly identified as bugs/structural problems, not the design/curve-shape items (those are Task 063).



\## 1. Scope



\### 1.1 Fix Live Tier Reference

\- The game currently runs on `TestTier` rather than the intended `GameTier` (which has an empty wave list and zero scene references per the audit). Investigate why — likely a scene reference or default-tier assignment somewhere points at `TestTier`.

\- Fix the reference so `GameTier` is the one actually used in gameplay, OR if `TestTier` was being actively used for legitimate ongoing testing and `GameTier` is genuinely meant to be built out fresh, flag this distinction clearly and ask which is intended before proceeding — don't silently switch the live tier without confirming `GameTier`'s wave list will be populated as part of Task 063 immediately after.



\### 1.2 Fix LootTable\_Regular Drop Weights

\- All entry weights in `LootTable\_Regular` are currently 0, meaning regular enemies never drop loot despite a non-zero overall drop chance. Fix the weights to non-zero values so loot actually drops — use a simple even distribution across entries as a placeholder if no specific weighting has been designed yet (Task 063 can refine the actual weight distribution as a balance decision).



\### 1.3 Remove Orphaned/Dead Assets

\- Remove or clearly mark as deprecated: the 3 unused abilities (including the lowercase `Lightningbolt`), \~2 unused upgrade lines, 1 unused apex, the dead `GameTier`/`BossGrunt`/`Goblin` references (only if confirmed truly unused after §1.1's fix — `GameTier` itself should NOT be deleted, only confirmed dead sub-references within/around it), and the duplicate `SharpElixir`/`GreaterWhetstone` damage-potion family (keep one, remove the duplicate — flag which one you kept and why if it's not obvious which is canonical).

\- Before deleting anything, do a reference search to confirm it's genuinely unused — if any "orphan" turns out to be referenced somewhere the audit missed, flag it instead of deleting.



\### 1.4 EvilGod Boss Baseline Stat Fix (Minimal)

\- The audit found EvilGod's contact damage (5) and currency yield (5) are identical to a basic Skeleton despite having 200 HP — this is clearly an unintentional leftover, not a balance choice (a boss shouldn't hit and pay like trash regardless of target balance philosophy). Apply a reasonable placeholder increase to damage and currency yield now (e.g. damage and currency roughly proportional to its HP multiple vs. Skeleton) so it's no longer broken — Task 063 will do the deliberate tuning pass for the boss's full "slow, heavy-hitting tank" identity, this is just a stopgap so the boss isn't actively broken in the meantime.



\---



\## 2. Out of Scope



\- Wave difficulty curve shape/StatMultiplier redesign — Task 063.

\- Content expansion (new waves beyond \~wave 20) — Task 063.

\- Gear rarity tier spread, XP curve redesign, shop pricing — Task 063.

\- Full EvilGod boss identity tuning (beyond the stopgap in §1.4) — Task 063.



\---



\## 3. Acceptance Criteria



\- \[ ] The live game runs on `GameTier` (or the confirmed-correct intended tier, per §1.1's flag-if-ambiguous clause), not `TestTier`.

\- \[ ] `LootTable\_Regular` has non-zero weights and enemies confirmed to drop loot in a test run.

\- \[ ] All confirmed-orphaned assets listed in §1.3 are removed or deprecated, with a reference-check performed before each deletion.

\- \[ ] EvilGod's damage and currency yield are no longer identical to Skeleton's — a reasonable placeholder increase is applied.

\- \[ ] No regressions: existing waves/abilities/gear that ARE in use still function correctly after cleanup.

