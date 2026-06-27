\# Task 55 — Frost Warden: Permanent Pulsing Frost Emission Shader



> Status: Ready for implementation

> Depends on: Task 044 (Frost Slow/Freeze status shader — prototype reference for technique), §3.5 (Enemy pooling — N/A here but shares shader-writing conventions), CLAUDE.md note that all VFX/shader work is hand-written ShaderLab/HLSL + Particle System, not Shader Graph



\---



\## Instructions to Claude Code (read first)



1\. Read CLAUDE.md in full before starting, even if read in a prior session.

2\. Read this entire task file before writing any code.

3\. Do not expand scope. Implement exactly what's specified; flag suggestions separately instead of acting on them.

4\. Flag, don't guess, on:

&#x20;  - Any mismatch between this task's "Pre-confirmed State" section and what you actually find in the project.

&#x20;  - Any conflict with locked decisions in CLAUDE.md §2/§3.

&#x20;  - Anything requiring manual Unity Editor judgment (visual tuning of pulse speed/intensity by eye) — implement with clearly exposed, easily tunable Inspector fields rather than guessing final values.

5\. Do not touch ScriptableObject template assets at runtime.

6\. At the end of your response, summarize what was implemented, anything flagged, and what (if anything) needs manual Editor action/tuning.



\---



\## 0. Context / Pre-confirmed State (do not re-verify, treat as given)



\- Frost Warden's model is a Synty rogue-type model (SM\_Chr\_Male\_Rouge\_01), already re-materialed by the developer to a blue/icy color tint (manual Inspector change, already done — this task does not need to redo or touch that color tint).

\- Task 044 already implemented a Frost Slow/Freeze status shader (mist vs. crystallization look) used as a temporary per-enemy debuff effect. This task is a \*\*separate, permanent, always-on\*\* effect for the Frost Warden hero specifically — not a reuse or modification of the Task 044 status-effect shader, and must not affect or be affected by enemies' temporary Frost status visuals. Do not merge these two systems; they serve different purposes (temporary debuff vs. permanent character identity).

\- This is purely a shader/material visual task — no gameplay logic, no ability hooks, no VFX spawn points. The effect must be always active while Frost Warden is on screen, regardless of game state (not tied to any ability use, cooldown, or combat state).



\---



\## 1. Goal



Add a permanent, pulsing blue emission glow across the entire Frost Warden model, layered on top of the existing blue-tinted base material, to reinforce the character's frost identity at all times.



\## 2. Scope



\### 2.1 Shader Approach



\- Hand-written ShaderLab/HLSL only (per CLAUDE.md convention — no Shader Graph).

\- Extend or wrap the Frost Warden's current material/shader to add an \*\*Emission\*\* contribution:

&#x20; - Emission color: blue/cyan, tunable via an exposed shader property (e.g. `\_FrostEmissionColor`).

&#x20; - Emission intensity: oscillates smoothly over time (sine wave or similar) between a tunable min and max value — exposed as `\_FrostPulseMinIntensity`, `\_FrostPulseMaxIntensity`, and `\_FrostPulseSpeed` (or equivalent naming) so the developer can tune the "breathing" feel without touching shader code.

\- The pulse must be driven by `\_Time` (or equivalent built-in shader time value) directly in the shader, not by a C# script animating a material property every frame — keep this self-contained in the shader for performance and simplicity, unless a concrete reason emerges to do it from script (flag if you find one).



\### 2.2 Coverage



\- The effect applies to the entire model uniformly (cloth, armor, skin, weapon — all of it), not masked to specific material slots or body parts. If the model uses multiple materials/slots, apply the same emission treatment consistently across all of them rather than only the first/main one.



\### 2.3 Material Setup



\- If Frost Warden's current material (the one the developer applied for the blue tint) can have this emission behavior added directly without forking it, prefer that — keep the existing tint change intact, only add the emission layer on top.

\- If the current material's shader doesn't support custom properties cleanly (e.g. it's an unmodified Synty stock shader), create a new shader/material specifically for Frost Warden that preserves the existing blue base color/tint and adds the pulsing emission on top — do not lose the developer's existing tint work.



\### 2.4 Assignment



\- Apply the new/updated material to all relevant renderer slots on the Frost Warden prefab so the effect is visible in-game without further manual steps beyond what's unavoidable (e.g. if Inspector drag-and-drop of the material is the only way to finalize assignment, say so clearly in your summary).



\---



\## 3. Out of Scope



\- Any frost crystal/rime surface texture or normal-map detail — explicitly not wanted per design decision, emission glow only.

\- Any tie-in to abilities, cooldowns, combat state, or VFX spawn points — this is a permanent passive visual, always on.

\- Reuse or modification of the Task 044 enemy Frost status shader — kept fully separate.

\- Applying this same treatment to other heroes — Frost Warden only, for this task.



\---



\## 4. Acceptance Criteria



\- \[ ] Frost Warden displays a visible blue/cyan emission glow across the entire model at all times in-game.

\- \[ ] The glow's intensity pulses smoothly (breathing effect) rather than staying static or flickering harshly.

\- \[ ] Pulse min/max intensity, color, and speed are exposed as tunable shader/material properties, not hardcoded.

\- \[ ] The existing blue base tint/material work the developer already did is preserved, not overwritten or reverted.

\- \[ ] Effect covers the full model (all material slots), not just one part.

\- \[ ] No gameplay-state coupling — the glow is unaffected by abilities, cooldowns, or combat state.

\- \[ ] No Shader Graph used — hand-written ShaderLab/HLSL only, consistent with existing VFX work in the project.

