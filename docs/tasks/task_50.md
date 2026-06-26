\# Task 50 — Remaining Cross-Hero Combo Apex Talents (4 Pairs)



> Read `CLAUDE.md` in full, and review Task 38 (Frozen Lightning — the first combo apex and the established

> `ComboApexTalentDefinitionSO` pattern), Task 48 (Pyromancer apexes), and Task 49 (Marksman apexes) before

> starting. This task completes the full combo-apex matrix across all four heroes: combined with Frozen Lightning

> (already implemented), all six possible hero pairs will have a combo apex once this task is done.



\## Goal



Add four new `ComboApexTalentDefinitionSO` entries, each a passive synergy between a specific pair of single-hero

apex talents, following Frozen Lightning's established pattern (no independent cooldown — a rule that modifies

the behavior of one or both referenced apexes when both are active).



\## Scope



\### 1. Shatter (Frost Warden + Marksman)

Requires: Remorseless Winter (Frost Warden) + Bullet Storm (Marksman).

Whenever Remorseless Winter freezes a target, that target becomes primed (same priming concept as Frozen

Lightning's mechanism — reuse that pattern). If a Marksman shot (any Basic/Ultimate/apex shot tagged Physical)

hits a primed target, it detonates: the target takes a burst of Physical AoE damage affecting nearby enemies,

then the prime is consumed.

\- Primed window: 2s after freeze lands

\- Detonation damage: 200% of the triggering shot's damage, dealt as AoE to all enemies within 3m of the primed

&#x20; target (in addition to the shot's own normal damage to its original target/pierce targets)



\### 2. Frostburn (Frost Warden + Pyromancer)

Requires: Remorseless Winter (Frost Warden) + Cataclysm (Pyromancer).

Whenever any enemy is currently affected by a Frost Warden CC effect (Slow or Freeze, from any source — Frozen

Ground, Deepening Frost, Hard Freeze, or Remorseless Winter itself), any Burn DoT tick on that same enemy deals

significantly increased damage.

\- Burn tick damage multiplier while target has any active Slow/Freeze: x1.75

\- This is a continuous passive check (not a one-time consumed prime, unlike Shatter/Frozen Lightning) — re-evaluated

&#x20; every Burn tick based on the target's current CC state at that moment



\### 3. Chain Combustion (Bolt Striker + Pyromancer)

Requires: Thunderstorm (Bolt Striker) + Wildfire Apocalypse (Pyromancer).

Whenever Bolt Striker's Chain Lightning (the jump effect) hits a target that currently has an active Burn, the

jump also refreshes/extends that target's Burn duration and adds one Stacking Embers-style bonus stack

(consistent with Pyromancer's Line 3 stacking behavior), without requiring a Fireball hit.

\- Burn duration extension on chain-hit: +2s

\- Bonus stack added: equivalent to one Stacking Embers stack at Pyromancer's current tier value



\### 4. Incendiary Rounds (Marksman + Pyromancer)

Requires: Executioner's Volley (Marksman) + Cataclysm (Pyromancer).

Whenever Marksman's Piercing Rounds (or Full Pierce) hits multiple enemies in one shot, every enemy hit beyond

the first also receives a Burn instance (at Pyromancer's current Smoldering Wound tier potency), not just direct

damage.

\- Applies once per pierced enemy per shot, regardless of how many enemies are pierced



\## Data Model Notes

\- All four reuse the existing `ComboApexTalentDefinitionSO` type and `Passive` `TriggerType` from Task 38 — no

&#x20; schema changes should be needed. If any of these four genuinely requires a schema addition, flag it and explain

&#x20; why before proceeding, since the goal is to prove the existing model generalizes.

\- Each requires its own two specific apex prerequisites (per hero pairing above) — confirm none of the four

&#x20; accidentally shares an apex requirement with another combo apex in a way that causes ambiguous unlock logic.



\## Out of Scope (do not implement)

\- Any change to the five single-hero apex talents' own behavior, damage, or cooldowns

\- Any change to Frozen Lightning (already implemented, Task 38)

\- VFX for these four combo apexes (separate future task, following Task 47's pattern)

\- Real balance tuning beyond the numbers specified above



\## Acceptance Criteria

\- \[ ] All four combo apexes implemented using the existing `ComboApexTalentDefinitionSO`/`Passive` pattern, no

&#x20;     schema changes unless explicitly flagged and justified

\- \[ ] Shatter: Remorseless Winter correctly primes targets; a Marksman Physical hit on a primed target correctly

&#x20;     detonates AoE damage and consumes the prime

\- \[ ] Frostburn: Burn ticks correctly deal x1.75 damage while the target has any active Frost Warden CC effect,

&#x20;     re-evaluated per tick (not a one-time consumed effect)

\- \[ ] Chain Combustion: Chain Lightning's jump correctly extends Burn duration and adds a stack on an already-Burning

&#x20;     target, without requiring a Fireball hit

\- \[ ] Incendiary Rounds: every pierced enemy beyond the first in a Piercing Rounds/Full Pierce hit receives a Burn

&#x20;     instance at Pyromancer's current tier potency

\- \[ ] Each combo apex unlocks only when both of its specific required apexes are active in the same run

\- \[ ] No SO asset is mutated at runtime

\- \[ ] Full playtest: with all four heroes active across test runs, verify each of the four combo apexes triggers

&#x20;     correctly under its specific conditions



\## Reviewer Notes

Flag as blocking if:

\- Any combo apex requires a new SO schema without being flagged/justified first

\- Frostburn is implemented as a one-time consumed prime instead of a continuous per-tick check

\- A combo apex's unlock condition is ambiguous or accidentally shared with another combo apex's requirement

