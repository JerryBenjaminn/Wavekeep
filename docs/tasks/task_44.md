\# Task 44 — Prototype: Frost Status VFX Shader (Slow vs. Freeze)



> Read `CLAUDE.md` in full, and review Task 19 (Frost Warden status-effect state machine on `EnemyRuntime`) before

> starting. This is a deliberately scoped, single-effect prototype task — its purpose is to validate the

> shader/VFX workflow before committing to a larger gameplay-feedback pass. Use hand-written ShaderLab/HLSL

> (.shader files), not Shader Graph, for editability and predictable iteration.



\## Goal



Add a clear, readable visual indicator on enemies for Frost Warden's Slow and Freeze status effects, with two

distinct intensity levels so the player can visually tell at a glance whether an enemy is merely slowed or fully

frozen/stunned. Slow should look like a light, ambient frost mist effect (Elden Ring-style subtle cold aura);

Freeze should look noticeably stronger and unmistakable — a clear visual escalation, not just a deeper version of

the same tint.



\## Scope



\### 1. Shader

\- Write a hand-authored `.shader` (ShaderLab + HLSL) applied to enemy materials, controlled by a single

&#x20; intensity-style parameter (e.g. `\_FrostAmount`, range 0–1) plus a discrete mode flag/parameter distinguishing

&#x20; Slow vs. Freeze tiers (your call on exact parameter design — document it).

\- \*\*Slow tier\*\*: subtle bluish-white mist/fog effect around or on the enemy — low-intensity tint plus a light

&#x20; particle-like noise pattern (can be achieved in-shader via a moving noise texture sample, or paired with a

&#x20; lightweight Particle System if that's simpler/more effective — your call, document the approach taken).

\- \*\*Freeze tier\*\*: a clearly stronger effect — heavier blue/white tint, a crystallization-style pattern (e.g.

&#x20; Voronoi-noise-based crack/ice pattern) covering more of the surface, and ideally a brief transition "snap"

&#x20; moment when freeze first applies (e.g. a short scale-pulse or flash on the moment of transition) so the player

&#x20; unmistakably notices the exact moment an enemy gets frozen.

\- Keep the shader reasonably simple/stylized rather than attempting physically-accurate ice refraction — this

&#x20; fits the project's current placeholder-art stage and is easier to iterate on.



\### 2. Runtime Integration

\- Hook the shader's parameters to the existing status-effect state machine on `EnemyRuntime` (Task 19) — when an

&#x20; enemy enters Slow, apply the mist-tier parameters; when it enters Freeze/Stun, apply the stronger tier; when

&#x20; the status ends, transition the parameters back to 0 (a brief fade-out is preferable to an instant cut, for

&#x20; readability).

\- This must work correctly with multiple enemies independently — each enemy's material/shader instance tracks

&#x20; its own status state, no shared/global parameter that would affect all enemies at once.

\- Confirm this works correctly with the existing `EnemyPoolManager` (Task 01/§3.5) — pooled enemies must have

&#x20; their frost shader parameters correctly reset when recycled, so a previously-frozen enemy doesn't spawn back in

&#x20; already showing frost VFX.



\## Out of Scope (do not implement)

\- VFX/shaders for any other ability, hero, or status effect (Burn, Stun outside of Frost, etc.) — this task is

&#x20; scoped to Frost Slow/Freeze only, as a workflow test

\- Main Menu, audio, or any other gameplay-feedback work

\- Final art-quality polish — the goal is a clearly readable, reasonably good-looking first pass, not a final

&#x20; shipped-quality effect



\## Acceptance Criteria

\- \[ ] Slow status produces a subtle, readable frost-mist visual on the affected enemy

\- \[ ] Freeze status produces a clearly stronger, unmistakable visual distinct from Slow, with a noticeable

&#x20;     transition moment when freeze first applies

\- \[ ] Effect correctly clears (with a brief fade rather than an instant cut) when the status ends

\- \[ ] Multiple simultaneously-affected enemies each show correct, independent VFX state

\- \[ ] Pooled enemy reuse correctly resets frost VFX state — no leftover effect on a freshly-spawned enemy

\- \[ ] No SO asset is mutated at runtime

\- \[ ] Full playtest: apply Slow to several enemies, confirm mist effect; apply Freeze (e.g. via Hard Freeze),

&#x20;     confirm clear visual escalation and transition moment; let effects expire and confirm clean fade-out;

&#x20;     verify pooled enemies don't inherit leftover VFX



\## Reviewer Notes

Flag as blocking if:

\- Freeze and Slow are visually too similar to distinguish at a glance

\- A shared/global shader parameter is used instead of per-enemy state, causing all enemies to show the same

&#x20; effect simultaneously

\- Pooled enemies show leftover frost VFX from a previous life cycle

