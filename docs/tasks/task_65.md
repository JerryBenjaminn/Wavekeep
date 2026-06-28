\# Task 065 — Bugfix: Enemies Already Attacking Wall Don't Respect Level-Up Pause



> Status: Ready for implementation

> Depends on: Task 028 (shared pause signal — original fix for ultimate-charge ticking during shop/level-up pause), Task 054/055 (Skeleton/EvilGod Attack→AttackRecovery loop)



\---



\## Instructions to Claude Code (read first)



1\. Read CLAUDE.md in full before starting, even if read in a prior session.

2\. Read this entire task file before writing any code.

3\. Do not expand scope. Implement exactly what's specified; flag suggestions separately instead of acting on them.

4\. Flag, don't guess, on:

&#x20;  - Whether the existing shared pause signal from Task 028 is the same mechanism that already gates enemy movement correctly — confirm this before reusing it, don't assume.

&#x20;  - Any conflict with locked decisions in CLAUDE.md §2/§3.

5\. Do not touch ScriptableObject template assets at runtime.

6\. At the end of your response, summarize the root cause found and the fix applied, and confirm both movement-pausing and attack-pausing now use the same signal/mechanism.



\---



\## 0. Context / Pre-confirmed State



\- Developer confirmed via playtesting: enemies still approaching the wall (moving) correctly freeze/pause during the level-up card-selection UI (and presumably shop UI too — confirm both). Enemies that have already reached the wall and are in the Attack/AttackRecovery loop (Task 054/055) continue attacking the wall during this same pause window — this is the bug.

\- Task 028 previously fixed a related issue (ultimate charge ticking during shop/level-up pause) using a shared pause signal. The movement-pausing behavior that already works correctly is presumably also gated by this same signal, or an equivalent one.

\- Likely root cause: once an enemy reaches the wall and enters the Attack/AttackRecovery Animator loop (§2.2 in Task 054), it no longer passes through the movement-update code path where the pause check apparently lives — the attack loop runs independently and was never wired to check the pause signal.



\---



\## 1. Goal



Make the enemy Attack/AttackRecovery loop respect the same pause signal that already correctly halts enemy movement, so enemies stop attacking the wall (and stop their Animator progressing) during level-up and shop UI pauses, consistent with already-paused approaching enemies.



\## 2. Scope



\### 2.1 Diagnosis



\- Locate the existing pause signal/mechanism from Task 028 and confirm it's the same one gating enemy movement correctly today.

\- Locate where the enemy's Attack-trigger re-firing logic lives (whatever code re-triggers the `Attack` parameter on each attack cycle once an enemy is in range of the wall, per Task 054 §2.2's AttackRecovery → Attack loop) and confirm it currently has no pause check.



\### 2.2 Fix



\- Gate the attack-cycle re-trigger logic with the same pause signal used for movement — when paused, the enemy should not re-fire the `Attack` trigger, and should not deal further wall damage via `OnAttackImpactFrame()` while paused.

\- Consider whether the Animator itself should also be paused (e.g. `Animator.speed = 0`) during this window for visual consistency with how movement freezing presumably looks (confirm how movement-pause currently looks visually — does the enemy model freeze in place, or does only the position stop updating while animation continues? Match whatever the existing movement-pause visual treatment is, for consistency) — flag if you're unsure which treatment is expected and pick the one that's visually consistent with currently-paused walking enemies.

\- Ensure this works correctly regardless of which exact point in the Attack/AttackRecovery cycle the enemy was in when the pause began (i.e. don't just block the trigger between cycles — if pause happens mid-Attack-animation, that swing should not deal damage if its impact frame fires after the pause begins).



\### 2.3 Resume Behavior



\- When the pause ends (level-up selection or shop closes), enemies already at the wall must correctly resume their attack loop from a sensible state — not skip ahead, not double-fire, and not get stuck permanently unable to attack again.



\---



\## 3. Out of Scope



\- Any change to how movement-pausing currently works — confirmed already correct, don't touch.

\- Any change to the pause signal's triggers (what opens/closes it) — only consuming it correctly from the attack-loop side.

\- Wave/difficulty balance — unrelated to this bug.



\---



\## 4. Acceptance Criteria



\- \[ ] Enemies in the Attack/AttackRecovery loop stop dealing wall damage and stop re-triggering attacks while the level-up card UI is open.

\- \[ ] Same behavior confirmed for the shop UI pause (if shop also uses this pause signal — confirm and test both).

\- \[ ] An attack mid-swing when pause begins does not deal damage if its impact frame would fire after the pause started.

\- \[ ] On resume, enemies correctly continue attacking without skipped cycles, double-damage, or permanently stuck state.

\- \[ ] Already-correct movement-pause behavior for approaching enemies is unaffected by this fix.

