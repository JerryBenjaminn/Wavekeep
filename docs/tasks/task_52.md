\# Task 52 — Marksman VFX: Tracer Shots + Piercing Impacts + Minigun



> Read `CLAUDE.md` in full, and review Task 45/46/51 (VFX precedent for other heroes) and Task 49 (Marksman's

> lines) before starting. Use hand-written ShaderLab/HLSL and/or simple Particle Systems, not Shader Graph.

> Palette: metallic/kinetic — warm white/pale-orange tracers and gray/white spark/fracture effects, visually

> distinct from Frost Warden (blue/white), Bolt Striker (gold electrical), and Pyromancer (red/orange fire).

> Since Marksman's mechanical identity is deliberately plain ("just a bullet"), visual flair here comes from

> density, snappiness, and small kinetic details rather than an elemental effect.



\## Goal



Add visual feedback for Marksman's Basic attack and Minigun ultimate, emphasizing fast tracers, metallic impact

sparks, and a sense of sustained mechanical intensity during Minigun, plus a distinct visual for the Armor

Shredder debuff stack.



\## Scope



\### 1. Basic Attack

\- \*\*Shot\*\*: an instant, bright tracer line (not a slow-traveling projectile) from Marksman to the target, plus a

&#x20; brief muzzle-flash at the firing point.

\- \*\*Piercing Rounds\*\* (Line 1, if upgraded): the tracer should visibly continue through all pierced targets in a

&#x20; straight line (rather than stopping/disappearing at the first hit), with a small metallic spark-burst impact

&#x20; effect at each pierced enemy along the line — count of spark bursts should match the actual number of enemies

&#x20; pierced this shot.

\- \*\*Multishot\*\* (Line 3, if upgraded): multiple simultaneous tracers fanning out at the line's current tier's

&#x20; spread angle and shot count.



\### 2. Armor Shredder Debuff Indicator

\- A distinct visual on the target showing accumulated Armor Shredder stacks — e.g. thin gray/white fracture

&#x20; lines that visibly increase in density/coverage as stacks increase (1 stack = a couple of hairline cracks, max

&#x20; stacks = a heavily fractured look), distinct in shape/color language from Bolt Striker's Piercing Bolt

&#x20; indicator and Pyromancer's Burn glow (per Task 46/51's existing distinction requirement).

\- Indicator should correctly fade as stacks expire/refresh per Task 49's stack-duration rules.



\### 3. Minigun (Ultimate)

\- \*\*Activation\*\*: a clear ramp-up moment (e.g. brief spin-up flash) before sustained fire begins.

\- \*\*Sustained fire\*\*: a dense stream of tracers sweeping across the arena width, with a subtle heat-shimmer

&#x20; distortion effect building up around the firing point as the burst continues (purely visual, communicates

&#x20; "this weapon is working hard" without implying any overheat mechanic).

\- \*\*Brass/cartridge ejection\*\*: small particle bursts of ejected casings during firing, for added kinetic detail.

\- \*\*Faster Spin-Up / Heavy Rounds / Full Pierce\*\* (Lines 6/7/8, if upgraded): tracer density/brightness should

&#x20; visibly scale with current fire rate and damage tier values rather than staying fixed.



\## Out of Scope (do not implement)

\- Apex VFX for Marksman (separate task, following Task 47's pattern)

\- Other heroes' VFX

\- Audio

\- Any actual overheat/heat mechanic — the heat shimmer is purely cosmetic, no gameplay tie-in



\## Acceptance Criteria

\- \[ ] Basic attack shows an instant bright tracer + muzzle flash; Piercing Rounds shows the tracer continuing

&#x20;     through all pierced targets with one spark-burst impact per pierced enemy, matching actual pierce count

\- \[ ] Multishot shows the correct number of simultaneous tracers at the current tier's spread angle

\- \[ ] Armor Shredder indicator visibly scales in density with current stack count and is visually distinct from

&#x20;     Bolt Striker's and Pyromancer's debuff/status indicators

\- \[ ] Minigun shows a spin-up moment, sustained dense tracer stream with heat-shimmer and casing-ejection

&#x20;     particles, scaling visibly with current Faster Spin-Up/Heavy Rounds/Full Pierce tier values

\- \[ ] No SO asset is mutated at runtime

\- \[ ] Full playtest: confirm tracer/impact counts and Minigun intensity correctly reflect current upgrade tiers;

&#x20;     confirm Armor Shredder indicator is visually distinguishable from other heroes' debuff indicators



\## Reviewer Notes

Flag as blocking if:

\- Spark-burst impact count doesn't match the actual number of enemies pierced in a given shot

\- Armor Shredder's visual is easily confused with Piercing Bolt's or Burn's indicator

\- Heat shimmer is implemented as anything affecting actual gameplay timing/damage instead of being purely visual

