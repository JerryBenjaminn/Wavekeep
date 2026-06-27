\# Task 53 — Firewall Fix: Mid-Arena Positioning + Particle System Visual Overhaul



> Read `CLAUDE.md` in full, and review Task 33 (Frost Zone's full-arena-width geometry — Firewall should use the

> same positioning logic) and Task 51 (Firewall's current VFX implementation, the yellow-rectangle visual being

> replaced here) before starting.



\## Bug: Incorrect Positioning



Firewall currently spawns directly in front of the wall (matching Frost Zone's depth-band placement from Task

33). The intended design is different: Firewall should spawn roughly in the middle of the arena, between the

wall and the enemy spawn edge, so advancing enemies walk through it and take a large Burn DoT application as they

cross, before reaching the wall. Reposition Firewall's activation point to the arena's mid-depth rather than

reusing Frost Zone's near-wall depth band — keep the same full-arena-width geometry (Task 33), just move where

along the depth axis it's centered.



\- On entering Firewall's band, an enemy should receive a Burn application (consistent with Pyromancer's existing

&#x20; Burn mechanics, e.g. equivalent to a strong Fireball-tier Burn) in addition to Firewall's existing per-tick DoT

&#x20; damage while standing inside it — confirm this on-entry Burn application exists; if Firewall currently only

&#x20; deals its own DoT tick without separately applying a lingering Burn status, add that on-entry Burn application

&#x20; now, since "walking through and getting burned for it" is the explicit design intent.



\## Visual Overhaul: Particle System-Based Wall



Replace the current flat yellow rectangle with a proper layered fire-wall effect using Unity's built-in Particle

System (not a flat mesh/quad), so it reads as a literal wall of flame.



\### Scope

\- Use a long, thin `ShapeModule` emission area (line or box shape matching the full-arena-width band) to emit

&#x20; flame particles continuously along Firewall's length for its active duration.

\- Layer at least two particle effects for visual depth: a base layer of larger, slower-rising flame particles

&#x20; (orange/red, additive blending) and a secondary layer of smaller, faster embers/sparks rising above the base

&#x20; flames.

\- Particle size, color-over-lifetime, and emission rate should give a convincing "wall of fire" silhouette rather

&#x20; than scattered individual flames — tune density so the wall reads as continuous along its full width.

\- Pair with a subtle heat-shimmer/distortion shader pass behind or around the wall if feasible (reuse the

&#x20; heat-shimmer approach from Task 52's Minigun if a similar technique applies here), for added visual depth.

\- Activation, ambient, and Inferno Surge/Wildfire Spread visual behaviors from Task 51 should carry over onto

&#x20; this new particle-based wall rather than being lost — confirm Inferno Surge's synced burst and Wildfire

&#x20; Spread's after-effect still work correctly with the new visual base.



\## Out of Scope (do not implement)

\- Any change to Firewall's damage values, DoT tick rate, or duration (Task 48's values stay as-is — this task

&#x20; only changes position and visual)

\- Any change to Frost Zone or other heroes' VFX



\## Acceptance Criteria

\- \[ ] Firewall now activates centered at the arena's mid-depth rather than directly in front of the wall, still

&#x20;     spanning the full arena width

\- \[ ] Enemies crossing into Firewall's band receive a Burn application on entry, in addition to existing per-tick

&#x20;     DoT while standing inside it

\- \[ ] Firewall's visual is rendered via layered Particle System emission (base flames + embers), not a flat

&#x20;     colored rectangle/mesh

\- \[ ] Inferno Surge's synced burst and Wildfire Spread's after-effect (Task 51) still function correctly with the

&#x20;     new particle-based visual

\- \[ ] No SO asset is mutated at runtime

\- \[ ] Full playtest: confirm Firewall spawns mid-arena, confirm enemies walking through receive Burn on entry

&#x20;     plus ongoing DoT, confirm the new particle-based wall looks like a continuous wall of fire rather than a

&#x20;     flat shape



\## Reviewer Notes

Flag as blocking if:

\- Firewall still spawns at the wall-adjacent depth band instead of mid-arena

\- The visual is still a flat mesh/quad rather than Particle-System-driven

\- On-entry Burn application is missing (only tick damage exists, no actual Burn status applied)

