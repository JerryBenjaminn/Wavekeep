\# Task 34 — Armor \& Magic Resistance System



> Read `CLAUDE.md` in full, and review Task 02 (WaveConfig/DifficultyTier scaling), Task 04 (IAbility/AbilityRuntime

> modifier pipeline), and Task 23 (existing crit pipeline, as the precedent for "final step in the damage

> pipeline") before starting. This is a foundational damage-mitigation system required before Bolt Striker's

> content (a later task) can be implemented meaningfully — Piercing Bolt and Overload both depend on this

> existing first.



\## Goal



Introduce two enemy-side defensive stats, \*\*Armor\*\* (reduces Physical damage taken) and \*\*Magic Resistance\*\*

(reduces Magical damage taken), using a diminishing-returns formula so neither stat can make an enemy fully

immune. Every ability gets a damage type tag (Physical or Magical) so the right resistance stat applies. Frost

Warden's and Bolt Striker's abilities are both tagged Magical.



\## Scope



\### 1. Damage Type Tagging

\- Add a `DamageType` field (enum: `Physical`, `Magical`) to `AbilityDefinitionSO` (and to the apex talent ability

&#x20; definitions from Task 29/31, since those are also independent abilities).

\- Set existing abilities' damage type: Frost Warden's Basic (Frost Bolt Burst), Ultimate (Frost Zone), and both

&#x20; apex talents (Remorseless Winter, Permafrost Eruption) → `Magical`.

\- Any future ability (including Bolt Striker, added in a later task) must set this field explicitly — flag if any

&#x20; existing ability lacks a sensible default, rather than silently defaulting all to one type.



\### 2. Enemy Defensive Stats

\- Add `Armor` and `MagicResist` fields to `EnemyDefinitionSO`, authored per enemy type alongside existing stats

&#x20; (HP, movement, loot yield).

\- Scale both stats by wave/difficulty the same way HP and damage currently scale (via `WaveConfigSO`/

&#x20; `DifficultyTierSO` multipliers, per CLAUDE.md §3.1) — reuse the existing scaling mechanism rather than adding a

&#x20; separate scaling path.



\### 3. Damage Mitigation Formula

\- Apply a diminishing-returns reduction as the step in `AbilityRuntime`'s damage pipeline that checks the

&#x20; incoming ability's `DamageType` against the target's `Armor` or `MagicResist`: damageTaken = rawDamage \* (100/100 + relevantDefenseStat)) where `relevantDefenseStat` is `Armor` if the ability is `Physical`, `MagicResist` if `Magical`.

\- This mitigation step applies \*\*after\*\* all existing modifiers (level/tag/gear/consumable/crit from Task 23) have

&#x20; already been resolved into a final raw damage number — it is the last step before damage is applied to the

&#x20; enemy, consistent with how Task 23 added crit as a final multiplicative step.

\- A defense stat of 0 results in no mitigation (`damageTaken == rawDamage`), preserving current behavior for any

&#x20; enemy without explicit Armor/MagicResist values set.



\### 4. Temporary Defense Reduction Support (for future use, e.g. Piercing Bolt)

\- `EnemyRuntime` needs to support a temporary, time-limited reduction to its effective `Armor` (not `MagicResist`

&#x20; for now, since the only currently-planned consumer — Piercing Bolt, a future Bolt Striker line — targets

&#x20; Physical defense specifically) — a debuff that lowers the enemy's effective Armor for a duration, then reverts.

&#x20; This should affect damage from \*\*all sources\*\* hitting that enemy while active (any hero's Physical-tagged

&#x20; abilities), not just the source that applied the debuff — implement this as a generic, stackable-by-design (but

&#x20; not necessarily stacking yet) status-style modifier on `EnemyRuntime`'s effective Armor calculation, following

&#x20; the existing status-effect state-machine pattern from Task 19/§3.8 rather than a one-off special case.

\- No ability currently grants this debuff — this task only builds the support mechanism so a future task (Bolt

&#x20; Striker) can apply it without further `EnemyRuntime` changes.



\## Out of Scope (do not implement)

\- Any Bolt Striker ability content (separate task)

\- Any temporary MagicResist reduction mechanism (not currently needed by any planned ability)

\- Real balance values for Armor/MagicResist per enemy type — placeholder/sensible-default values are fine, real

&#x20; tuning happens later

\- UI display of enemy defensive stats (not requested)



\## Acceptance Criteria

\- \[ ] `DamageType` field exists on `AbilityDefinitionSO`; Frost Warden's Basic, Ultimate, and both apex talents

&#x20;     are tagged `Magical`

\- \[ ] `Armor` and `MagicResist` fields exist on `EnemyDefinitionSO`, scale via the existing wave/difficulty

&#x20;     multiplier mechanism

\- \[ ] Damage mitigation formula correctly applies diminishing-returns reduction based on the incoming ability's

&#x20;     `DamageType` vs. the matching enemy defense stat, as the final step after all existing modifiers (including

&#x20;     crit)

\- \[ ] Enemies with 0 Armor/MagicResist take full damage (no behavior change from before this task)

\- \[ ] `EnemyRuntime` supports a temporary Armor reduction debuff (duration-based, affects damage from all

&#x20;     sources), with no current ability granting it yet

\- \[ ] No SO asset is mutated at runtime

\- \[ ] Full playtest: confirm damage numbers visibly decrease against an enemy with nonzero Armor (Physical

&#x20;     source) or MagicResist (Magical source — test with Frost Warden), and confirm an enemy with 0 in the

&#x20;     relevant stat takes unchanged damage



\## Reviewer Notes

Flag as blocking if:

\- Mitigation is applied before crit/other modifiers instead of as the final pipeline step

\- Mitigation uses a flat-subtraction formula instead of the specified diminishing-returns formula

\- Temporary Armor reduction is implemented as a one-off special case instead of using the existing status-effect

&#x20; pattern

\- Magical-vs-Physical type is missing or defaults silently on any existing ability without being flagged

