\# Task 46 — Bolt Striker VFX: Lightning Bolt + Single-Target Nuke Effects



> Read `CLAUDE.md` in full, and review Task 44/45 (frost VFX precedent — same workflow, different palette) and

> Task 35 (Bolt Striker's lines: Chain Lightning, Static Charge, Overcharge, Piercing Bolt, Multi-Strike, Execute,

> Charged Finisher, Overload) before starting. Use hand-written ShaderLab/HLSL and/or simple Particle Systems, not

> Shader Graph. Bolt Striker's effects are gold/yellow electrical flashes rather than persistent surface effects,

> consistent with the hero having no status-effect-based abilities (unlike Frost Warden).



\## Goal



Add visual feedback for Bolt Striker's two player-facing abilities: a fast electrical bolt/flash for Lightning

Bolt (Basic), with a visible jump effect when Chain Lightning triggers, and a heavier multi-hit electrical strike

for the single-target nuke (Ultimate). Effects are quick, bright, gold/yellow electrical flashes — momentary by

nature, not lingering area effects, matching the hero's single-target burst identity.



\## Scope



\### 1. Lightning Bolt (Basic)

\- \*\*Main hit\*\*: a fast, bright gold/yellow bolt effect from Bolt Striker to the target — e.g. a jagged

&#x20; lightning-line shape (simple zigzag mesh/line renderer or particle trail) plus a brief flash at the impact

&#x20; point. Should read as instant/snappy, not a slow travel like Frost Bolt Burst's projectile.

\- \*\*Chain Lightning\*\* (Line 1, if upgraded): when the bolt jumps to a secondary target, render a visibly distinct

&#x20; secondary bolt connecting the primary and secondary target, slightly thinner/dimmer than the main hit to

&#x20; communicate it's the reduced-damage jump. At Tier 3 (two jumps), both jump-bolts should render.

\- \*\*Overcharge\*\* (Line 3, if upgraded): the bonus spike hit (when it triggers) should flash noticeably

&#x20; brighter/larger than a normal hit so the player can tell at a glance when the spike procs, distinct from a

&#x20; normal crit flash if the existing crit pipeline (Task 23) already has its own visual — confirm whether crit

&#x20; hits currently have any visual feedback; if not, this task should add a simple shared crit-flash treatment

&#x20; reused for both normal crits and Overcharge's spike (with Overcharge's spike being the more intense version).

\- \*\*Piercing Bolt\*\* (Line 4, if upgraded): the target's temporary Armor-reduction debuff (Task 34) should show a

&#x20; brief debuff indicator on the target (e.g. a small cracking/weakening visual distinct from Frost Warden's

&#x20; ice-crack pattern — suggest a different shape language, e.g. jagged gold fracture lines rather than blue

&#x20; Voronoi ice, to avoid visually implying a frost effect) for the debuff's duration.



\### 2. Single-Target Nuke (Ultimate)

\- \*\*Cast/impact\*\*: a heavier, more dramatic version of the basic bolt — e.g. a thicker bolt or a brief

&#x20; convergence of multiple smaller bolts onto the target, communicating higher impact than the basic attack.

\- \*\*Multi-Strike\*\* (Line 5, if upgraded): each individual hit within the cast should flash distinctly (not one

&#x20; blended effect), so the player can see the hit count — sync exact hit timing to the line's current tier (2/3/4

&#x20; hits).

\- \*\*Execute\*\* (Line 6, if upgraded): when the bonus-damage execute condition triggers (target below HP%

&#x20; threshold), add a distinct stronger flash/color shift on that specific hit so the player notices the execute

&#x20; proc'd.

\- \*\*Overload\*\* (Line 8, if upgraded): similar to Piercing Bolt, show a brief debuff indicator on the target for

&#x20; its duration, visually distinguishable from Piercing Bolt's indicator since they're different mechanics

&#x20; (Armor-specific vs. generic damage-taken vulnerability) — your call on the distinct visual treatment, document

&#x20; it.



\### 3. Integration

\- Hook all effects to existing ability execution points (`IAbility.Execute`, line modifier resolution) — no

&#x20; parallel trigger path.

\- Confirm effect counts/timing reflect current upgrade-line tiers (Chain Lightning's jump count, Multi-Strike's

&#x20; hit count) rather than fixed values.

\- Confirm effects work correctly with `EnemyPoolManager` pooling and don't leak/persist incorrectly across casts.



\## Out of Scope (do not implement)

\- Apex/combo apex VFX (Thunderstorm, Lethal Surge, Frozen Lightning) — separate task

\- Frost Warden or any other hero's VFX

\- Audio

\- Static Charge's stack count having its own dedicated visual indicator beyond what's needed to read the

&#x20; Overcharge/crit flashes correctly (a numeric/UI stack counter, if wanted, is a UI task, not VFX)



\## Acceptance Criteria

\- \[ ] Lightning Bolt shows a fast gold/yellow bolt + impact flash; Chain Lightning's jump(s) render as distinct

&#x20;     secondary bolts when upgraded, matching current tier's jump count

\- \[ ] A shared crit-flash treatment exists and Overcharge's bonus spike renders as a clearly more intense version

&#x20;     of it

\- \[ ] Piercing Bolt's Armor-reduction debuff shows a brief, distinct visual indicator on the target for its

&#x20;     duration

\- \[ ] Single-target nuke shows a heavier impact effect than the basic bolt; Multi-Strike's individual hits are

&#x20;     each visually distinct and match the current tier's hit count

\- \[ ] Execute procs show a distinct stronger flash on the triggering hit

\- \[ ] Overload's debuff indicator is visually distinguishable from Piercing Bolt's

\- \[ ] All effects hook into existing ability execution points, no parallel trigger logic

\- \[ ] No SO asset is mutated at runtime

\- \[ ] Full playtest: cast Basic and Ultimate at various upgrade tiers, confirm jump/hit counts and debuff visuals

&#x20;     match current line tiers; confirm no visual leftovers persist incorrectly between casts



\## Reviewer Notes

Flag as blocking if:

\- Effect counts/timing use hardcoded values instead of reading the ability's actual current tier values

\- Piercing Bolt and Overload debuff indicators are visually identical, making it impossible to tell which debuff

&#x20; is active

\- A new parallel ability-trigger path is introduced instead of hooking existing execution points

