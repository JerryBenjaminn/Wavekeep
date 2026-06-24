\# Task 31 — Frost Warden: Final Upgrade Line Content + Apex Talents



> Read `CLAUDE.md` in full, and review Task 29 (upgrade line system, structural migration) before starting. This

> task replaces Frost Warden's placeholder/migrated lines from Task 29 with the final designed line content below.

> Numeric values are placeholders for balance purposes but are specified explicitly so no value needs to be

> guessed — implement them as given, they will be tuned later in the editor without code changes.



\## Goal



Implement Frost Warden's 8 final upgrade lines (4 on Basic — Frost Bolt Burst, 4 on Ultimate — Frost Zone) and 2

apex talents, replacing whatever placeholder content Task 29 created. Frost Warden's design intent is a pure

\*\*CC + AoE DPS\*\* hero — no DoT/damage-over-time effects belong on this hero; that space is reserved for a future

hero archetype.



\## Scope



\### 1. Basic Skill Lines — Frost Bolt Burst



\*\*Line 1 — Frozen Ground\*\* (pure CC)

On hit, leaves a patch of ice at the impact point that slows enemies standing in it.

\- Tier 1: patch radius 1.5m, duration 2s, slow 20%

\- Tier 2: patch radius 2m, duration 3s, slow 30%

\- Tier 3: patch radius 2.5m, duration 4s, slow 40%



\*\*Line 2 — Wider Burst\*\* (pure AoE reach)

Increases the AoE radius of Frost Bolt Burst's impact and the max number of enemies it can hit.

\- Tier 1: +15% radius, +1 max targets

\- Tier 2: +30% radius, +2 max targets

\- Tier 3: +50% radius, +3 max targets



\*\*Line 3 — Shattering Impact\*\* (CC→damage payoff, not DoT)

Frost Bolt Burst deals bonus instant damage (on the initial hit itself, not a separate tick) against targets

currently affected by any Slow or Freeze status.

\- Tier 1: +15% damage vs slowed/frozen targets

\- Tier 2: +30% damage vs slowed/frozen targets

\- Tier 3: +50% damage vs slowed/frozen targets



\*\*Line 4 — Hard Freeze\*\* (pure CC, upgrades slow into hard CC)

On hit, adds a chance to fully stun (freeze in place, no movement/attack) the target briefly instead of only

applying the normal slow.

\- Tier 1: 10% chance, 0.5s stun

\- Tier 2: 20% chance, 0.75s stun

\- Tier 3: 30% chance, 1s stun



\### 2. Ultimate Skill Lines — Frost Zone



\*\*Line 5 — Deepening Frost\*\* (pure CC)

Increases the slow percentage applied to enemies standing inside Frost Zone.

\- Tier 1: 30% slow

\- Tier 2: 40% slow

\- Tier 3: 50% slow

(these replace/override Frost Zone's current base slow value rather than stacking additively with it — confirm

against current base value and document if base differs from these numbers)



\*\*Line 6 — Lingering Chill\*\* (pure CC)

Increases Frost Zone's active duration.

\- Tier 1: +1.5s duration

\- Tier 2: +3s duration

\- Tier 3: +4.5s duration



\*\*Line 7 — Zone Pulse\*\* (pure AoE DPS, area-tick not target-tick)

Frost Zone periodically deals AoE damage to all enemies currently standing inside it. This is an area-tied pulse,

not a debuff that follows the enemy if they leave the zone — an enemy that exits the zone before a pulse takes no

damage from that pulse.

\- Tier 1: pulse every 1.5s, damage equal to 10% of Basic ability's current damage per pulse

\- Tier 2: pulse every 1.2s, damage equal to 18% of Basic ability's current damage per pulse

\- Tier 3: pulse every 1s, damage equal to 28% of Basic ability's current damage per pulse



\*\*Line 8 — Absolute Zero\*\* (CC area growth, indirectly supports AoE DPS via Zone Pulse coverage)

Whenever an enemy dies while inside Frost Zone, the zone's radius temporarily expands.

\- Tier 1: +10% radius for 2s per death, does not stack beyond +10% (refreshes duration on new death)

\- Tier 2: +20% radius for 2.5s per death, stacks up to 2 instances (+40% max)

\- Tier 3: +30% radius for 3s per death, stacks up to 3 instances (+90% max)



\### 3. Apex Talents



\*\*Remorseless Winter\*\* (requires Frozen Ground Tier 3 + Deepening Frost Tier 3 — cross-skill CC apex)

Automatically triggers on its own cooldown: freezes (hard stun, same effect category as Hard Freeze) the nearest

enemy to Frost Warden for a fixed duration.

\- Cooldown: 8s

\- Stun duration: 1.5s

\- No player input required; ticks automatically once unlocked, following the existing IAbility cooldown pattern.



\*\*Permafrost Eruption\*\* (requires Wider Burst Tier 3 + Zone Pulse Tier 3 — cross-skill AoE DPS apex)

Automatically triggers on its own cooldown: deals a burst of AoE damage in a large radius centered on Frost

Warden's current position.

\- Cooldown: 10s

\- Damage: equal to 50% of Basic ability's current damage, applied once to all enemies in radius

\- Radius: 4m (placeholder, independent of Frost Zone's own radius)

\- No player input required; ticks automatically once unlocked.



\## Out of Scope (do not implement)

\- Any DoT/damage-over-time effect on Frost Warden (explicitly excluded by design — reserved for a future hero)

\- Cross-hero apex talents (e.g. combining Frost Warden lines with another hero's lines) — single-hero apexes only

&#x20; for now

\- Changes to how many lines are drawn per level-up or card weighting logic (Task 29's existing draw behavior

&#x20; stands)

\- Real balance tuning beyond the numbers specified above — these are deliberately fully-specified placeholders,

&#x20; not values to be invented, but they are still expected to be retuned later



\## Acceptance Criteria

\- \[ ] All 8 lines implemented with the exact tier values specified above, replacing Task 29's placeholder Frost

&#x20;     Warden lines

\- \[ ] Frozen Ground, Hard Freeze, Deepening Frost, Lingering Chill, and Absolute Zero apply no damage-over-time —

&#x20;     pure CC/utility effects only

\- \[ ] Shattering Impact's bonus applies to the initial hit only, not as a separate damage tick

\- \[ ] Zone Pulse damages only enemies inside the zone at the moment of each pulse (verified: an enemy that exits

&#x20;     mid-interval before the next pulse takes no damage from that pulse)

\- \[ ] Absolute Zero's radius expansion stacking behaves per tier as specified (no stack at T1, up to 2 at T2, up

&#x20;     to 3 at T3) and decays/refreshes duration correctly

\- \[ ] Remorseless Winter and Permafrost Eruption both unlock only when both required lines are at Tier 3, trigger

&#x20;     automatically on their specified cooldowns with no player input

\- \[ ] No SO asset is mutated at runtime

\- \[ ] Full playtest: level Frozen Ground + Deepening Frost to Tier 3, confirm Remorseless Winter unlocks and

&#x20;     fires automatically; separately level Wider Burst + Zone Pulse to Tier 3, confirm Permafrost Eruption

&#x20;     unlocks and fires automatically



\## Reviewer Notes

Flag as blocking if:

\- Any line introduces a damage-over-time effect on Frost Warden

\- Zone Pulse is implemented as a per-target DoT that persists after leaving the zone, instead of an area-tied tick

\- Shattering Impact is implemented as a separate damage tick instead of a modifier on the initial hit

\- An apex talent requires player input to trigger instead of activating automatically on cooldown

