\# Task 33 — Frost Zone: Full-Arena Coverage + Absolute Zero Redesign



> Read `CLAUDE.md` in full, and review §2 (single spawn direction, defended wall) and Task 31 (Frost Warden's

> ultimate lines) before starting. This task changes Frost Zone's area-of-effect geometry and redesigns one

> upgrade line to fit the new geometry — no other line, the apex talents, or any other ability is affected.



\## Goal



Frost Zone currently spawns as a circle centered on Frost Warden's position, which under-covers the arena and

doesn't match the design intent: CC is Frost Warden's defining strength, so the ultimate should blanket the

entire approach lane the enemies walk through, not just a small area around the caster.



\## Scope



\### 1. Frost Zone Geometry Change



Replace the centered-circle shape with a fixed zone covering the full width of the arena's single approach lane

(per CLAUDE.md §2 — enemies spawn from one side and advance toward the wall across a single, open-width lane).



\- On activation, Frost Zone occupies the \*\*full width of the arena\*\* across a fixed depth band positioned in front

&#x20; of the wall (placeholder depth: 6m from the wall, extending back toward the spawn side) — not following Frost

&#x20; Warden's position, and not requiring Frost Warden to be standing in any particular spot to activate it.

\- This means the zone's shape/size is no longer meaningfully described by a "radius" — replace radius-based

&#x20; geometry with a fixed rectangular (or arena-width-matching) area for this ability specifically. Other systems

&#x20; using a generic radius/AoE concept (Basic ability's Wider Burst, the future apex talents, etc.) are unaffected;

&#x20; this change is scoped to Frost Zone's own shape only.

\- All enemies currently inside the arena's approach lane when Frost Zone is active are affected (slow from

&#x20; Deepening Frost, damage ticks from Zone Pulse) exactly as before — only the shape/positioning of "inside the

&#x20; zone" changes, not the per-tier values from Task 31.



\### 2. Absolute Zero Redesign



The original radius-expansion design no longer makes sense once the zone already covers the full arena width.

Replace it with a duration-extension effect: whenever an enemy dies while inside Frost Zone, the zone's remaining

active duration is extended.



\- Tier 1: +1s duration per death, no stacking limit beyond simply adding time (cannot exceed a hard cap of

&#x20; Lingering Chill's current max duration + 3s, to prevent runaway uptime — confirm/implement this cap)

\- Tier 2: +1.5s duration per death, same cap rule

\- Tier 3: +2s duration per death, same cap rule

\- Each enemy death inside the zone while active triggers this extension independently (multiple deaths in quick

&#x20; succession each add their respective amount, up to the cap).



\### 3. Interaction Check

\- Confirm Wider Burst (Basic, Task 31 Line 2) and the apex talents (Task 31/32) are unaffected — they use their

&#x20; own independent radius/AoE values and are not tied to Frost Zone's shape.

\- Confirm Zone Pulse (Line 7) and Deepening Frost (Line 5) correctly apply to the new rectangular/full-width area

&#x20; rather than a circular check.



\## Out of Scope (do not implement)

\- Any change to Frozen Ground, Wider Burst, Shattering Impact, Hard Freeze (Basic lines) — unaffected by this task

\- Any change to apex talent unlock conditions or their own AoE shapes/values

\- Real balance tuning of the depth-band value (6m) or the duration-extension cap beyond what's specified above —

&#x20; these are specified placeholders, not values to invent, but still expected to be retuned later



\## Acceptance Criteria

\- \[ ] Frost Zone, once activated, covers the full width of the arena's approach lane at a fixed depth band in

&#x20;     front of the wall, regardless of Frost Warden's position at cast time

\- \[ ] Deepening Frost's slow and Zone Pulse's damage correctly affect all enemies within the new full-width area,

&#x20;     not just those near Frost Warden

\- \[ ] Absolute Zero now extends Frost Zone's remaining duration per enemy death inside it, per the tier values

&#x20;     above, respecting the duration cap

\- \[ ] Wider Burst and both apex talents are confirmed unaffected by this change (still use their own independent

&#x20;     AoE values)

\- \[ ] No SO asset is mutated at runtime

\- \[ ] Full playtest: activate Frost Zone, confirm it spans the full arena width regardless of caster position;

&#x20;     let several enemies die inside it with Absolute Zero active, confirm duration extends correctly up to the cap



\## Reviewer Notes

Flag as blocking if:

\- Frost Zone's new shape is implemented as a very large circle approximating full coverage instead of an actual

&#x20; full-width area, which would under/over-cover depending on arena dimensions

\- Absolute Zero's duration extension has no cap, risking unbounded uptime

\- Wider Burst or apex talent AoE logic is accidentally coupled to Frost Zone's new shape code

