\# Task 060 — Fix Frost/Burn Status VFX on Skeleton + EvilGod Models



> Status: Ready for implementation

> Depends on: Task 044 (Frost Slow/Freeze status shader), Task 051 (Pyromancer Burn-status shader), Task 054 (Skeleton model integration), Task 055 (EvilGod model integration)



\---



\## Instructions to Claude Code (read first)



1\. Read CLAUDE.md in full before starting, even if read in a prior session.

2\. Read this entire task file before writing any code.

3\. Do not expand scope. Implement exactly what's specified; flag suggestions separately instead of acting on them.

4\. Flag, don't guess, on:

&#x20;  - Any mismatch between this task's "Pre-confirmed State" section and what you actually find in the project.

&#x20;  - Any conflict with locked decisions in CLAUDE.md §2/§3.

&#x20;  - If the root cause differs meaningfully between Skeleton and EvilGod (e.g. one is a renderer-reference issue, the other a shader-compatibility issue), document both separately rather than assuming a single shared fix covers both.

5\. Do not touch ScriptableObject template assets at runtime.

6\. At the end of your response, summarize the root cause found, what was fixed, and what (if anything) needs manual Editor action/verification.



\---



\## 0. Context / Pre-confirmed State (do not re-verify, treat as given)



\- Frost status VFX (Task 044) and Burn status VFX (Task 051) both worked correctly on the original capsule placeholder for enemies.

\- Since swapping to the real Synty models (Skeleton in Task 054, EvilGod in Task 055), neither Frost nor Burn status visuals appear on hit/application, even though the gameplay logic (slow/freeze, DoT damage) is confirmed still functioning correctly — this is purely a visual regression.

\- Other VFX (impact points, death effects) were already explicitly re-anchored to bones during Tasks 054/055 and are confirmed working — this task is specifically about the Frost/Burn \*\*status-effect shader/material system\*\*, which is a separate mechanism from the one-shot impact/death VFX.



\---



\## 1. Goal



Diagnose and fix why the Frost and Burn status-effect visuals no longer render on the Skeleton and EvilGod models, restoring the same visual feedback that existed on the capsule placeholder.



\## 2. Scope



\### 2.1 Diagnosis (do this first, for both models separately)



Investigate and identify the actual root cause — likely one or more of:

\- A `Renderer`/`Material` component reference in the Frost/Burn status-effect script still points to the old capsule's renderer (now removed/disabled), rather than the new model's renderer(s).

\- A material-slot-index assumption (e.g. swapping `materials\[0]`) that doesn't match the new model's actual material slot count/order — Synty models may have multiple material slots (e.g. separate body/armor/weapon materials) where the capsule had exactly one.

\- A shader-compatibility issue: the status-effect shader expects specific properties/UVs/vertex data present on the capsule's material/shader that aren't present (or are structured differently) on the new model's Synty shader.

\- Confirm whether the issue is identical for Skeleton and EvilGod, or different — report both findings even if you find a single shared fix resolves both.



\### 2.2 Fix



\- Update renderer/material references to correctly target the new models' actual renderer(s) and material slot(s) — prefer making this lookup robust (e.g. find by component on the model at runtime/init, or an explicit `\[SerializeField]` assigned per-prefab) rather than hardcoding an index that could silently break again for the next enemy model.

\- If the root cause is shader-property/UV incompatibility: extend or adapt the Frost/Burn shader so it works correctly against the new models' existing materials/shaders, preserving the original visual look (mist/crystallization for Frost, spreading/stacking burn look for Burn) as closely as possible. If a perfect visual match isn't feasible without a larger rework, implement the closest reasonable equivalent and flag the discrepancy rather than silently shipping a degraded look without mention.

\- If multiple material slots exist on the new models (e.g. separate body vs. armor materials), the status effect should visually apply across all relevant slots, not just one, unless there's a clear reason (flagged) to limit it.



\### 2.3 Pooling Compatibility Check (§3.5)



\- Verify the fix still resets correctly on pooled enemy reuse — a recycled Skeleton/EvilGod must not spawn with a leftover Frost/Burn visual state from its previous life. This was presumably already handled generically in Tasks 044/051's pooling-reset logic; just confirm it still holds after this fix, don't rebuild it.



\---



\## 3. Out of Scope



\- Any new status effect types beyond Frost and Burn.

\- Visual redesign/improvement of the Frost/Burn look beyond restoring (or closely approximating) what worked on the capsule.

\- Applying this fix preemptively to enemy models that don't exist yet (future enemy types) — fix Skeleton and EvilGod now; the underlying robustness improvement in §2.2 should reduce the chance of recurrence, but this task doesn't need to test against hypothetical future models.



\---



\## 4. Acceptance Criteria



\- \[ ] Frost status visual (mist/crystallization) correctly appears on the Skeleton model when frozen/slowed.

\- \[ ] Frost status visual correctly appears on the EvilGod model when frozen/slowed.

\- \[ ] Burn status visual correctly appears on the Skeleton model when burning.

\- \[ ] Burn status visual correctly appears on the EvilGod model when burning.

\- \[ ] Underlying gameplay logic (slow %, freeze threshold, DoT damage) is unchanged — this task is visual-only.

\- \[ ] Pooled re-spawn of either enemy type doesn't show a leftover Frost/Burn visual from a previous life.

\- \[ ] Root cause is clearly explained in the response summary, including whether it was the same cause for both models or different.

