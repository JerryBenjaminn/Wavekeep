\# Task 48 — Pyromancer: Hero Implementation + Upgrade Lines + Apex Talents



> Read `CLAUDE.md` in full, and review Task 05 (hero system), Task 29 (upgrade line system), and Task 34

> (DamageType tagging) before starting. Same structural pattern as Task 31 (Frost Warden) and Task 35 (Bolt

> Striker) — no new hero-system architecture required.



\## Goal



Add \*\*Pyromancer\*\*, a DoT/AoE hero whose identity is fire that spreads and stacks. Basic (Fireball) is a

single-target hit that applies a Burn DoT; Ultimate (Firewall) is a full-arena-width wall of fire dealing heavy

sustained DoT damage. Both abilities are tagged `Magical` (Task 34).



\## Scope



\### 1. Hero \& Ability Definitions

\- New `HeroDefinitionSO` for Pyromancer, following existing authoring pattern (Task 05).

\- \*\*Basic — Fireball\*\*: single-target ranged hit, applies a Burn DoT on hit, `DamageType: Magical`.

\- \*\*Ultimate — Firewall\*\*: full-arena-width damage wall (same width-based geometry as Frost Zone, Task 33),

&#x20; dealing high sustained DoT damage to everything inside, `DamageType: Magical`.



\### 2. Basic Skill Lines — Fireball



\*\*Line 1 — Smoldering Wound\*\*

Increases Burn's DoT damage and/or duration.

\- Tier 1: +20% Burn damage, +1s duration

\- Tier 2: +35% Burn damage, +2s duration

\- Tier 3: +50% Burn damage, +3s duration



\*\*Line 2 — Spreading Flame\*\*

When a Burned target dies, Burn spreads to the nearest other enemy in range, applying a fresh Burn instance.

\- Tier 1: spreads to 1 target, 15m range

\- Tier 2: spreads to 1 target, 20m range, 100% of original Burn potency

\- Tier 3: spreads to 2 targets, 20m range, 100% of original Burn potency



\*\*Line 3 — Stacking Embers\*\*

Fireball hits on the same target stack additional Burn instances (does not reset when hitting other targets in

between — stacks persist on that specific target for its Burn's remaining duration).

\- Tier 1: +10% DoT damage per stack, max 3 stacks

\- Tier 2: +15% DoT damage per stack, max 4 stacks

\- Tier 3: +20% DoT damage per stack, max 5 stacks



\*\*Line 4 — Combustion\*\*

When a Burn instance expires naturally on a target, a chance for it to detonate in a small AoE, dealing instant

damage to nearby enemies.

\- Tier 1: 20% chance, 2m radius, 30% of Basic's current damage

\- Tier 2: 35% chance, 2.5m radius, 45% of Basic's current damage

\- Tier 3: 50% chance, 3m radius, 60% of Basic's current damage



\### 3. Ultimate Skill Lines — Firewall



\*\*Line 5 — Raging Wall\*\*

Increases Firewall's DoT tick damage directly.

\- Tier 1: +20% tick damage

\- Tier 2: +35% tick damage

\- Tier 3: +50% tick damage



\*\*Line 6 — Lingering Embers\*\*

Increases Firewall's active duration.

\- Tier 1: +1.5s duration

\- Tier 2: +3s duration

\- Tier 3: +4.5s duration



\*\*Line 7 — Wildfire Spread\*\*

Enemies that die inside Firewall leave a small smoldering patch that continues dealing Burn-tier damage for a

short time after Firewall itself ends.

\- Tier 1: patch lasts 2s after Firewall ends, deals 20% of Firewall's tick damage

\- Tier 2: patch lasts 3s, deals 30% of Firewall's tick damage

\- Tier 3: patch lasts 4s, deals 40% of Firewall's tick damage



\*\*Line 8 — Inferno Surge\*\*

Firewall periodically deals an additional burst of instant AoE damage to everyone inside, on top of its regular

DoT ticks.

\- Tier 1: burst every 3s, 40% of Basic's current damage

\- Tier 2: burst every 2.5s, 55% of Basic's current damage

\- Tier 3: burst every 2s, 70% of Basic's current damage



\### 4. Apex Talents



\*\*Wildfire Apocalypse\*\* (requires Spreading Flame Tier 3 + Wildfire Spread Tier 3 — cross-skill)

Automatically triggers on its own cooldown: ignites every enemy within a radius around Pyromancer with a fresh

Burn instance, simulating a sudden spread event.

\- Cooldown: 9s

\- Burn applied: same potency as Smoldering Wound's current Tier 3 values

\- Radius: 6m



\*\*Cataclysm\*\* (requires Combustion Tier 3 + Inferno Surge Tier 3 — cross-skill)

Automatically triggers on its own cooldown: a large AoE burst dealing bonus damage specifically to any currently

Burning targets within range.

\- Cooldown: 11s

\- Base damage: 60% of Basic's current damage to all targets in radius, +40% additional to targets currently

&#x20; Burning

\- Radius: 5m



\## Out of Scope (do not implement)

\- Cross-hero combo apex talents (separate task — Task 50)

\- Real balance tuning beyond the numbers specified above



\## Acceptance Criteria

\- \[ ] Pyromancer exists as a playable hero with Basic (Fireball) and Ultimate (Firewall), both tagged `Magical`

\- \[ ] All 8 lines implemented with the exact tier values specified above

\- \[ ] Spreading Flame correctly triggers only on Burn-target death, applies fresh Burn to a new target

\- \[ ] Stacking Embers correctly persists stacks on a specific target independent of other targets being hit

\- \[ ] Combustion's detonation triggers only on natural Burn expiry, not on every hit

\- \[ ] Wildfire Spread's smoldering patch correctly persists and ticks after Firewall itself has ended

\- \[ ] Inferno Surge's burst ticks independently of and in addition to Firewall's regular DoT

\- \[ ] Wildfire Apocalypse and Cataclysm unlock only when both required lines are at Tier 3, trigger automatically

&#x20;     on their specified cooldowns

\- \[ ] No SO asset is mutated at runtime

\- \[ ] Full playtest: level Spreading Flame + Wildfire Spread to Tier 3, confirm Wildfire Apocalypse unlocks and

&#x20;     fires; separately level Combustion + Inferno Surge to Tier 3, confirm Cataclysm unlocks and fires



\## Reviewer Notes

Flag as blocking if:

\- Spreading Flame triggers on every hit instead of only on Burn-target death

\- Wildfire Spread's patch is implemented as part of Firewall's active duration instead of persisting after it ends

\- Apex talents require player input instead of triggering automatically on cooldown

