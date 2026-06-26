\# Task 51 — Pyromancer VFX: Fireball + Firewall + Burn Status Effects



> Read `CLAUDE.md` in full, and review Task 44 (frost status shader — reuse the same workflow for a fire-status

> equivalent), Task 45 (Frost Bolt Burst/Frost Zone VFX, as the structural precedent), and Task 48 (Pyromancer's

> lines) before starting. Use hand-written ShaderLab/HLSL and/or simple Particle Systems, not Shader Graph.

> Palette: warm reds/oranges/yellows, visually distinct from Frost Warden's blue/white and Bolt Striker's gold.



\## Goal



Add visual feedback for Pyromancer's two abilities and its signature Burn status effect: a fireball projectile +

impact for Fireball (Basic), a full-arena wall of flame for Firewall (Ultimate), and a persistent fire-surface

effect on Burning enemies (parallel to Task 44's frost-status shader, but for fire).



\## Scope



\### 1. Burn Status Shader (parallel to Task 44)

\- A persistent per-enemy shader effect showing an ember/fire-licking surface pattern on Burning enemies — reuse

&#x20; Task 44's technical approach (per-enemy material parameter, correct pooling reset, independent state per

&#x20; enemy) but with a fire-themed pattern (e.g. flickering orange/red noise-driven glow rather than ice crystals).

\- Intensity should scale visibly with current Burn stack count (Stacking Embers, Task 48 Line 3) — more stacks

&#x20; should look more intense, not just longer-lasting.



\### 2. Fireball (Basic)

\- \*\*Projectile\*\*: a fast-traveling fireball with a trailing ember/smoke particle trail.

\- \*\*Impact\*\*: a small fire burst on hit, applying the Burn status shader above.

\- \*\*Combustion\*\* (Line 4, if upgraded): the detonation-on-Burn-expiry should have its own distinct small AoE

&#x20; fire-burst effect, scaled to its current tier's radius.

\- \*\*Spreading Flame\*\* (Line 2, if upgraded): when Burn spreads to a new target on death, show a brief traveling

&#x20; ember/spark effect connecting the dying enemy to the newly-ignited one.



\### 3. Firewall (Ultimate)

\- \*\*Activation\*\*: a sweeping wall-of-fire effect establishing the full-arena-width band (same geometry as Frost

&#x20; Zone, Task 33), visually reading as a literal wall of flame rather than a ground patch.

\- \*\*Ambient\*\*: persistent flame/heat-shimmer visual for its duration, intense enough to clearly read as "high

&#x20; damage zone" (Firewall is high-DoT by design) without obscuring enemies.

\- \*\*Inferno Surge\*\* (Line 8, if upgraded): a visible burst/flare across the wall synced to its actual tick timing.

\- \*\*Wildfire Spread\*\* (Line 7, if upgraded): the smoldering patch left behind after Firewall ends should render

&#x20; as a distinctly cooling-down version of the wall effect (e.g. dimmer embers, no full flame), for its correct

&#x20; remaining duration.



\## Out of Scope (do not implement)

\- Apex/combo apex VFX for Pyromancer (separate task, following Task 47's pattern)

\- Other heroes' VFX

\- Audio



\## Acceptance Criteria

\- \[ ] Burn status shows a persistent, per-enemy ember/glow effect that scales visibly with current stack count

\- \[ ] Pooled enemies correctly reset Burn VFX state on reuse

\- \[ ] Fireball shows a fast projectile + impact burst; Combustion and Spreading Flame have their own distinct

&#x20;     visual moments matching current tier values

\- \[ ] Firewall shows a clear wall-of-fire activation across the full arena width, persistent ambient flame for

&#x20;     its duration, and (if upgraded) a synced Inferno Surge flare and a distinct cooling-ember Wildfire Spread

&#x20;     after-effect

\- \[ ] No SO asset is mutated at runtime

\- \[ ] Full playtest: confirm visuals scale correctly with current line tiers, confirm no VFX leftovers persist

&#x20;     incorrectly across casts or pooled enemy reuse



\## Reviewer Notes

Flag as blocking if:

\- VFX intensity/timing uses hardcoded values instead of reading actual current tier values

\- Burn and Frost status shaders are visually similar enough to be confused at a glance

