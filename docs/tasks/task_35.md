\# Task 35 — Bolt Striker: Hero Implementation + Upgrade Lines + Apex Talents



> Read `CLAUDE.md` in full, and review Task 05 (hero system, HeroDefinitionSO/HeroRuntime), Task 29 (upgrade line

> system), Task 23 (crit pipeline), and Task 34 (DamageType tagging, Armor/MagicResist, temporary Armor reduction

> support) before starting. This task adds a new hero from scratch — Basic and Ultimate ability definitions, 8

> upgrade lines, and 2 apex talents — following the same structural pattern established for Frost Warden

> (Task 19/29/31), with no new hero-system architecture required.



\## Goal



Add \*\*Bolt Striker\*\*, a single-target DPS hero with a crit-leaning playstyle. No AoE, no damage-over-time — every

effect either hits one primary target harder, extends benefit to a single secondary target (Chain Lightning), or

debuffs a single target's defenses. Both Basic and Ultimate abilities are tagged `Magical` (Task 34).



\## Scope



\### 1. Hero \& Ability Definitions

\- New `HeroDefinitionSO` for Bolt Striker, following the existing authoring pattern (Task 05).

\- \*\*Basic — Lightning Bolt\*\*: single-target ranged hit, `DamageType: Magical`.

\- \*\*Ultimate — Single-target nuke\*\*: high-impact hit(s) concentrated on one primary target, `DamageType: Magical`.



\### 2. Basic Skill Lines — Lightning Bolt



\*\*Line 1 — Chain Lightning\*\* (the only line with any secondary-target reach; still single-hit-per-jump, not AoE

or DoT)

On hit, the bolt jumps to one additional nearby enemy for reduced damage.

\- Tier 1: 1 jump, 40% of main hit's damage to the jumped target

\- Tier 2: 1 jump, 55% of main hit's damage

\- Tier 3: 2 jumps, 55% of main hit's damage to each



\*\*Line 2 — Static Charge\*\* (rewards sustained focus on one target)

Consecutive hits on the \*same\* target stack a damage bonus against that target; switching targets resets the

stack.

\- Tier 1: +5% damage per stack, max 3 stacks (+15% max)

\- Tier 2: +7% damage per stack, max 4 stacks (+28% max)

\- Tier 3: +10% damage per stack, max 5 stacks (+50% max)



\*\*Line 3 — Overcharge\*\* (crit-leaning, two-part effect: passive crit chance + a separate burst chance)

Grants a flat crit chance bonus (feeding the existing Task 23 crit pipeline) and, independently, a chance for

Lightning Bolt to deal a one-time large damage spike on top of any normal/crit result.

\- Tier 1: +5% crit chance; +5% chance for a bonus spike hit dealing +50% damage

\- Tier 2: +10% crit chance; +10% chance for a bonus spike hit dealing +75% damage

\- Tier 3: +15% crit chance; +15% chance for a bonus spike hit dealing +100% damage



\*\*Line 4 — Piercing Bolt\*\* (consumes Task 34's temporary Armor reduction support)

On hit, applies a temporary reduction to the target's effective Armor, increasing Physical damage taken from all

sources for the duration (per Task 34's generic, source-agnostic implementation).

\- Tier 1: -10 effective Armor for 2s

\- Tier 2: -15 effective Armor for 3s

\- Tier 3: -20 effective Armor for 4s



\### 3. Ultimate Skill Lines — Single-Target Nuke



\*\*Line 5 — Multi-Strike\*\*

The ultimate hits the same target multiple times within one cast instead of once.

\- Tier 1: 2 hits, each at 60% of base ultimate damage

\- Tier 2: 3 hits, each at 60% of base ultimate damage

\- Tier 3: 4 hits, each at 60% of base ultimate damage



\*\*Line 6 — Execute\*\*

Bonus damage against targets below an HP% threshold.

\- Tier 1: +20% damage vs targets under 25% HP

\- Tier 2: +35% damage vs targets under 30% HP

\- Tier 3: +50% damage vs targets under 35% HP



\*\*Line 7 — Charged Finisher\*\*

Flat increase to the ultimate's base damage.

\- Tier 1: +15% base damage

\- Tier 2: +30% base damage

\- Tier 3: +50% base damage



\*\*Line 8 — Overload\*\* (one-time debuff multiplier, not DoT — mirrors Piercing Bolt's pattern but as a generic

vulnerability rather than Armor-specific, since this is the Ultimate's signature debuff)

The ultimate leaves the target with a temporary vulnerability: the target takes increased damage from all sources

for the debuff's duration (implement as a generic incoming-damage multiplier on `EnemyRuntime`, separate from the

Armor-specific mechanism Task 34 built — document the distinction clearly since both are "temporary defense-side

debuffs" but operate on different stages of the pipeline).

\- Tier 1: +10% damage taken for 2s

\- Tier 2: +15% damage taken for 3s

\- Tier 3: +20% damage taken for 4s



\### 4. Apex Talents



\*\*Thunderstorm\*\* (requires Chain Lightning Tier 3 + Multi-Strike Tier 3 — cross-skill)

Automatically triggers on its own cooldown: strikes the current primary target with a bolt that also jumps to a

nearby secondary target, using Chain Lightning's jump behavior and Multi-Strike's repeated-hit behavior combined

into one automatic burst.

\- Cooldown: 9s

\- Damage: 2 hits at 50% of Basic ability's current damage to primary target, 1 jump at 40% of that to a secondary

&#x20; target



\*\*Lethal Surge\*\* (requires Static Charge Tier 3 + Execute Tier 3 — cross-skill)

Automatically triggers on its own cooldown: deals a finishing strike against Bolt Striker's current primary

target, with bonus damage if that target is below the Execute threshold, consuming/benefiting from current

Static Charge stacks at time of trigger.

\- Cooldown: 11s

\- Base damage: 60% of Basic ability's current damage, +10% per current Static Charge stack, +30% additional if

&#x20; target is below Execute's current HP% threshold



\## Out of Scope (do not implement)

\- Any AoE or damage-over-time effect on Bolt Striker (explicitly excluded by design)

\- Any MagicResist-side temporary debuff (Task 34 only built the Armor-side mechanism; Overload's vulnerability

&#x20; debuff is a separate generic damage-taken multiplier, not a MagicResist reduction — do not conflate the two)

\- Cross-hero combo lines with Frost Warden (e.g. "Frozen Lightning") — single-hero apexes only, cross-hero combos

&#x20; wait for multi-hero runtime support

\- Hero-slot unlock progression (separate task)

\- Real balance tuning beyond the numbers specified above — these are fully-specified placeholders, expected to be

&#x20; retuned later



\## Acceptance Criteria

\- \[ ] Bolt Striker exists as a playable hero with Basic (Lightning Bolt) and Ultimate, both tagged `Magical`

\- \[ ] All 8 lines implemented with the exact tier values specified above

\- \[ ] Chain Lightning's jump deals reduced damage to exactly one (or two at Tier 3) additional target, not an AoE

\- \[ ] Static Charge stacks correctly per consecutive hit on the same target and resets on target switch

\- \[ ] Overcharge's crit chance bonus correctly feeds the existing Task 23 crit pipeline; its bonus spike is a

&#x20;     separate, independently-rolled effect

\- \[ ] Piercing Bolt correctly uses Task 34's generic temporary Armor reduction mechanism (verify Physical damage

&#x20;     from another hero, if testable, is also increased against a Piercing-Bolt-debuffed target)

\- \[ ] Overload's vulnerability is implemented as a separate generic damage-taken multiplier, distinct from

&#x20;     Piercing Bolt's Armor-specific mechanism

\- \[ ] Multi-Strike correctly applies multiple hits per ultimate cast to the same target

\- \[ ] Execute and Overload thresholds/bonuses apply correctly

\- \[ ] Thunderstorm and Lethal Surge unlock only when both required lines are at Tier 3, trigger automatically on

&#x20;     their specified cooldowns with no player input

\- \[ ] No SO asset is mutated at runtime

\- \[ ] Full playtest: level Chain Lightning + Multi-Strike to Tier 3, confirm Thunderstorm unlocks and fires

&#x20;     automatically; separately level Static Charge + Execute to Tier 3, confirm Lethal Surge unlocks and fires

&#x20;     automatically



\## Reviewer Notes

Flag as blocking if:

\- Any Bolt Striker line introduces AoE beyond Chain Lightning's specified single/double jump, or any

&#x20; damage-over-time effect

\- Piercing Bolt is implemented as a one-off special case instead of using Task 34's generic Armor-reduction

&#x20; mechanism

\- Overload's vulnerability debuff is implemented using Task 34's Armor-reduction mechanism instead of as its own

&#x20; generic damage-taken multiplier

\- Overcharge's crit chance bonus bypasses the existing Task 23 crit pipeline instead of feeding into it

