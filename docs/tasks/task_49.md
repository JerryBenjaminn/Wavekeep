\# Task 49 — Marksman: Hero Implementation + Upgrade Lines + Apex Talents



> Read `CLAUDE.md` in full, and review Task 05 (hero system), Task 29 (upgrade line system), and Task 34

> (DamageType tagging, Armor mitigation) before starting. Same structural pattern as prior heroes.



\## Goal



Add \*\*Marksman\*\*, a Physical-damage DPS hero whose identity is rapid, piercing fire — basic shots punch through

multiple enemies in a line, and the ultimate is a sustained full-arena-width spray of fire. Both abilities are

tagged `Physical` (Task 34) — the first hero to use this damage type, making Armor mitigation actually relevant

for the first time.



\## Scope



\### 1. Hero \& Ability Definitions

\- New `HeroDefinitionSO` for Marksman, following existing authoring pattern.

\- \*\*Basic\*\*: a simple, fast single-shot ranged attack with no special base behavior (a plain bullet/arrow),

&#x20; `DamageType: Physical`.

\- \*\*Ultimate — Minigun\*\*: for a fixed duration, fires rapidly across the full arena width in a sweeping pattern,

&#x20; `DamageType: Physical`.



\### 2. Basic Skill Lines



\*\*Line 1 — Piercing Rounds\*\*

Basic shots pierce through and damage every enemy in their path, instead of stopping at the first hit.

\- Tier 1: pierces up to 2 enemies, 100% damage to each

\- Tier 2: pierces up to 4 enemies, 100% damage to each

\- Tier 3: pierces unlimited enemies in line, 100% damage to each



\*\*Line 2 — Rapid Fire\*\*

Increases Basic's own fire rate directly.

\- Tier 1: +20% fire rate

\- Tier 2: +35% fire rate

\- Tier 3: +50% fire rate



\*\*Line 3 — Multishot\*\*

Basic fires multiple separate shots per trigger in a narrow spread (separate hit lines, not one wider AoE shot).

\- Tier 1: 2 shots, 15% spread angle

\- Tier 2: 3 shots, 15% spread angle

\- Tier 3: 4 shots, 20% spread angle



\*\*Line 4 — Armor Shredder\*\*

Each Basic hit applies a stacking Armor-reduction debuff to the target (separate mechanism from Bolt Striker's

Piercing Bolt — this one stacks per hit rather than being a flat single-application debuff).

\- Tier 1: -3 effective Armor per stack, max 5 stacks, 3s refresh duration per stack

\- Tier 2: -5 effective Armor per stack, max 6 stacks, 3s refresh duration per stack

\- Tier 3: -7 effective Armor per stack, max 8 stacks, 3s refresh duration per stack



\### 3. Ultimate Skill Lines — Minigun



\*\*Line 5 — Sustained Barrage\*\*

Increases Minigun's active duration (base 5s).

\- Tier 1: +1.5s duration

\- Tier 2: +3s duration

\- Tier 3: +4.5s duration



\*\*Line 6 — Faster Spin-Up\*\*

Increases Minigun's internal fire rate during its active duration.

\- Tier 1: +25% fire rate during Minigun

\- Tier 2: +45% fire rate during Minigun

\- Tier 3: +65% fire rate during Minigun



\*\*Line 7 — Heavy Rounds\*\*

Increases Minigun's per-shot damage directly.

\- Tier 1: +20% damage per shot

\- Tier 2: +35% damage per shot

\- Tier 3: +50% damage per shot



\*\*Line 8 — Full Pierce\*\*

Minigun's shots gain their own independent piercing bonus damage against pierced targets beyond the first,

separate from and stacking with Basic's Piercing Rounds.

\- Tier 1: +15% damage to pierced targets beyond the first hit

\- Tier 2: +30% damage to pierced targets beyond the first hit

\- Tier 3: +50% damage to pierced targets beyond the first hit



\### 4. Apex Talents



\*\*Bullet Storm\*\* (requires Multishot Tier 3 + Faster Spin-Up Tier 3 — cross-skill)

Automatically triggers on its own cooldown: a short, extremely dense burst of shots covering a wide arc in front

of Marksman.

\- Cooldown: 9s

\- Duration: 1.5s burst

\- Shots: 12 over the burst, each at 50% of Basic's current damage, full pierce



\*\*Executioner's Volley\*\* (requires Armor Shredder Tier 3 + Heavy Rounds Tier 3 — cross-skill)

Automatically triggers on its own cooldown: a single heavy shot at the target with the highest current Armor

Shredder stack count, dealing bonus damage scaled to that stack count.

\- Cooldown: 10s

\- Base damage: 80% of Basic's current damage, +15% per current Armor Shredder stack on the target



\## Out of Scope (do not implement)

\- Cross-hero combo apex talents (separate task — Task 50)

\- Real balance tuning beyond the numbers specified above



\## Acceptance Criteria

\- \[ ] Marksman exists as a playable hero with Basic and Ultimate (Minigun), both tagged `Physical`

\- \[ ] All 8 lines implemented with the exact tier values specified above

\- \[ ] Piercing Rounds correctly damages all enemies in the shot's path up to its current tier's limit

\- \[ ] Armor Shredder correctly stacks per hit (distinct mechanism from Bolt Striker's Piercing Bolt) with correct

&#x20;     stack cap and refresh behavior per tier

\- \[ ] Minigun's duration, fire rate, damage, and pierce bonus all scale correctly per their respective lines

\- \[ ] Bullet Storm and Executioner's Volley unlock only when both required lines are at Tier 3, trigger

&#x20;     automatically on their specified cooldowns

\- \[ ] Physical `DamageType` correctly triggers Armor-based mitigation (Task 34) against enemies with nonzero Armor

\- \[ ] No SO asset is mutated at runtime

\- \[ ] Full playtest: level Multishot + Faster Spin-Up to Tier 3, confirm Bullet Storm unlocks and fires;

&#x20;     separately level Armor Shredder + Heavy Rounds to Tier 3, confirm Executioner's Volley unlocks and scales

&#x20;     with target's current Shredder stacks



\## Reviewer Notes

Flag as blocking if:

\- Armor Shredder is implemented using the same mechanism as Bolt Striker's Piercing Bolt instead of as its own

&#x20; stacking debuff

\- Piercing Rounds and Full Pierce's bonuses don't stack correctly (one overriding the other instead of combining)

\- Apex talents require player input instead of triggering automatically on cooldown

