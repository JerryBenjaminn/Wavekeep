\# Task 45 — Frost Warden VFX: Basic + Ultimate Cast/Impact Effects



> Read `CLAUDE.md` in full, and review Task 44 (frost status shader prototype — reuse its visual language/color

> palette) and Task 31/33 (Frost Bolt Burst, Frost Zone mechanics and geometry) before starting. Use hand-written

> ShaderLab/HLSL and/or simple Particle Systems, consistent with Task 44's approach — not Shader Graph.



\## Goal



Add visual feedback for Frost Warden's two player-facing abilities: a projectile + ice-explosion effect for

Frost Bolt Burst (Basic), and a full-arena activation + ambient + pulse effect for Frost Zone (Ultimate). Reuse

the blue/white crystallization visual language established in Task 44 so Frost Warden's whole kit feels visually

cohesive.



\## Scope



\### 1. Frost Bolt Burst (Basic)

\- \*\*Projectile\*\*: a visible traveling effect (ice/snow-ball-style, simple shape + particle trail) from Frost

&#x20; Warden to the target point, replacing or supplementing the current invisible/instant hit resolution.

\- \*\*Impact\*\*: on hit, a crystallization-style burst — reuse Task 44's Voronoi/crack-pattern visual approach as a

&#x20; one-shot expanding burst rather than a persistent surface effect, scaled to the ability's current AoE radius

&#x20; (Task 31 Line 2, Wider Burst) so the burst visually grows when that line is upgraded.

\- If Frozen Ground (Line 1) is active on this hit, layer in a small persistent ice-patch decal on the ground at

&#x20; the impact point for that line's duration, visually distinct from (but thematically consistent with) the burst.



\### 2. Frost Zone (Ultimate)

\- \*\*Activation\*\*: a clear cast/activation moment when Frost Zone triggers — e.g. a sweeping frost wave that

&#x20; visually establishes the full-arena-width band (Task 33's geometry) so the player immediately sees the area's

&#x20; extent.

\- \*\*Ambient\*\*: a subtle persistent visual across the active zone for its duration (e.g. light mist/fog similar in

&#x20; spirit to Task 44's Slow-tier effect, low-intensity so it doesn't obscure enemies/gameplay).

\- \*\*Zone Pulse\*\* (Line 7, if unlocked): a periodic visual pulse synced to the actual pulse timing/damage tick,

&#x20; e.g. a expanding ring or brightness flash across the zone on each tick, so the player can see the pulse rhythm

&#x20; rather than only inferring it from damage numbers.



\### 3. Integration

\- Hook all effects to the existing ability execution points (`IAbility.Execute`, the existing status-effect/line

&#x20; modifier resolution) — no new parallel ability-trigger path.

\- Confirm effects correctly reflect current upgrade-line tiers where specified above (burst size scaling with

&#x20; Wider Burst, pulse timing matching Zone Pulse's current tier interval) rather than using fixed/placeholder

&#x20; values once those lines are upgraded.

\- Confirm effects work correctly with the `EnemyPoolManager` and don't leak/persist incorrectly across multiple

&#x20; casts in the same run.



\## Out of Scope (do not implement)

\- Apex talent VFX (Remorseless Winter, Permafrost Eruption) or combo apex VFX (Frozen Lightning) — separate task

\- Bolt Striker or any other hero's VFX

\- Audio

\- Main Menu or other UI work



\## Acceptance Criteria

\- \[ ] Frost Bolt Burst shows a visible projectile and a crystallization-style impact burst, scaling with Wider

&#x20;     Burst's current tier radius

\- \[ ] Frozen Ground's ice patch (when active) renders distinctly from the one-shot burst

\- \[ ] Frost Zone shows a clear activation effect establishing the full-arena-width area, a subtle ambient effect

&#x20;     for its duration, and (if Zone Pulse is unlocked) a visual pulse synced to its actual tick timing

\- \[ ] All effects hook into existing ability execution points, no parallel trigger logic

\- \[ ] No SO asset is mutated at runtime

\- \[ ] Full playtest: cast Basic and Ultimate at various upgrade tiers, confirm visuals scale/sync correctly with

&#x20;     line values; confirm no visual leftovers persist incorrectly between casts



\## Reviewer Notes

Flag as blocking if:

\- VFX scale/timing uses hardcoded values instead of reading the ability's actual current tier values

\- A new parallel ability-trigger path is introduced instead of hooking existing execution points

