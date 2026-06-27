\# Task 058 — Bolt Striker: Permanent Electric Crackle Emission Shader



> Status: Ready for implementation

> Depends on: Task 056 (Frost Warden permanent pulsing emission shader — related technique/precedent, but different visual approach), Task 046 (Bolt Striker Basic/Ultimate VFX — gold/yellow electricity flash, jump-bolts, crit-flash — color/style reference), CLAUDE.md note that all VFX/shader work is hand-written ShaderLab/HLSL + Particle System, not Shader Graph



\---



\## Instructions to Claude Code (read first)



1\. Read CLAUDE.md in full before starting, even if read in a prior session.

2\. Read this entire task file before writing any code.

3\. Do not expand scope. Implement exactly what's specified; flag suggestions separately instead of acting on them.

4\. Flag, don't guess, on:

&#x20;  - Any mismatch between this task's "Pre-confirmed State" section and what you actually find in the project.

&#x20;  - Any conflict with locked decisions in CLAUDE.md §2/§3.

&#x20;  - Anything requiring manual Editor judgment (visual tuning of crackle speed/density/intensity by eye) — implement with clearly exposed, easily tunable Inspector fields rather than guessing final values.

5\. Do not touch ScriptableObject template assets at runtime.

6\. At the end of your response, summarize what was implemented, anything flagged, and what (if anything) needs manual Editor action/tuning.



\---



\## 0. Context / Pre-confirmed State (do not re-verify, treat as given)



\- Bolt Striker's model uses the project's data-driven hero architecture (§3.1, `HeroDefinitionSO` Visual.Prefab field) — same setup pattern as Frost Warden.

\- Task 056 added a permanent pulsing blue emission shader to Frost Warden as a static "breathing" glow. This task is a \*\*separate, visually distinct effect\*\* for Bolt Striker — not a reuse or recolor of Frost Warden's shader. Where Frost Warden's effect is a uniform intensity pulse, Bolt Striker's effect must look like \*\*moving electric arcs/crackle lines\*\* traveling across the model's surface, not a uniform glow oscillation.

\- Bolt Striker's existing ability VFX (Task 046) uses a gold/yellow electricity color identity (flash, jump-bolts, crit-flash) — this permanent effect should use a matching gold/yellow color so the passive identity and active-ability VFX feel visually consistent.

\- This is purely a shader/material visual task — no gameplay logic, no ability hooks, no VFX spawn points. The effect must be always active while Bolt Striker is on screen, regardless of game state (not tied to ability use, cooldown, or combat state).



\---



\## 1. Goal



Add a permanent, animated gold/yellow electric crackle effect across the entire Bolt Striker model — moving arcs/lines rather than a static or uniformly-pulsing glow — to reinforce the character's lightning identity at all times.



\## 2. Scope



\### 2.1 Shader Approach



\- Hand-written ShaderLab/HLSL only (per CLAUDE.md convention — no Shader Graph).

\- Extend or wrap Bolt Striker's current material/shader to add an animated emission effect that reads as "traveling electric current," not a single oscillating intensity value. Reasonable techniques (pick one that fits cleanly with the project's existing hand-written shader conventions — flag if you take a different approach and explain why):

&#x20; - Scrolling/distorting noise (e.g. Voronoi or a simple scrolling noise texture) thresholded to produce thin, randomly-shifting bright line/arc shapes across the surface, animated via `\_Time`.

&#x20; - Multiple overlapping scrolling UV layers at different speeds/directions to fake unpredictable arc movement, rather than a single uniform scroll (a single direction scroll will look too "flowing"/uniform and less like erratic electricity).

\- Expose tunable shader properties for: emission color (`\_CrackleColor`, default gold/yellow matching Task 046's palette), crackle animation speed (`\_CrackleSpeed`), crackle density/threshold (`\_CrackleDensity` or equivalent controlling how much of the surface shows bright arcs at any moment), and base/idle emission intensity vs. peak arc intensity if the technique naturally separates these.

\- The animation must be driven by `\_Time` (or equivalent built-in shader time value) directly in the shader, not by a C# script animating properties every frame — keep this self-contained in the shader for performance, unless a concrete reason emerges to do it from script (flag if so).



\### 2.2 Coverage



\- The effect applies to the entire model uniformly (cloth, armor, skin, weapon — all of it), consistent with how Frost Warden's effect was applied in Task 056. If the model uses multiple materials/slots, apply the same crackle treatment consistently across all of them.



\### 2.3 Material Setup



\- If Bolt Striker's current material can have this behavior added directly without forking it, prefer that.

\- If the current material's shader doesn't support custom properties cleanly (e.g. unmodified Synty stock shader), create a new shader/material specifically for Bolt Striker that preserves any existing base color/tint work already done on the model and adds the crackle effect on top — do not lose existing visual work on the model if any tint/color customization has already been applied (check first, the way Frost Warden's blue tint was preserved in Task 056).



\### 2.4 Assignment



\- Apply the new/updated material to all relevant renderer slots on the Bolt Striker prefab/model so the effect is visible in-game without further manual steps beyond what's unavoidable (e.g. if Inspector drag-and-drop is the only way to finalize assignment, say so clearly in your summary).



\---



\## 3. Out of Scope



\- Any tie-in to abilities, cooldowns, combat state, or VFX spawn points — this is a permanent passive visual, always on.

\- Reuse or modification of Frost Warden's emission shader (Task 056) or the Task 044 enemy Frost status shader — kept fully separate.

\- Applying this same treatment to other heroes — Bolt Striker only, for this task.

\- Particle-based sparks/jump-bolts — that's already covered by Task 046's ability VFX; this task is the base-material permanent surface effect only.



\---



\## 4. Acceptance Criteria



\- \[ ] Bolt Striker displays a visible gold/yellow electric crackle effect across the entire model at all times in-game.

\- \[ ] The effect reads visually as moving/erratic arcs or current lines, not a uniform static glow or simple intensity pulse.

\- \[ ] Crackle color, speed, and density are exposed as tunable shader/material properties, not hardcoded.

\- \[ ] Any existing base tint/material work already done on Bolt Striker's model is preserved, not overwritten or reverted.

\- \[ ] Effect covers the full model (all material slots), not just one part.

\- \[ ] No gameplay-state coupling — the effect is unaffected by abilities, cooldowns, or combat state.

\- \[ ] No Shader Graph used — hand-written ShaderLab/HLSL only, consistent with existing VFX work in the project.

