\# Task 38 — Cross-Hero Combo Apex: Frozen Lightning



> Read `CLAUDE.md` in full, and review Task 29/31 (Frost Warden apex talents, ApexTalentDefinitionSO), Task 35

> (Bolt Striker apex talents), and Task 36/37 (multi-hero runtime) before starting. This task introduces the

> project's first \*\*cross-hero\*\* apex talent — a talent that requires specific apex talents from two different

> heroes, rather than two lines from a single hero. The data model must generalize to support future combo apexes

> with different trigger styles (passive synergy vs. independent active ability), not just this one pair.



\## Goal



Add \*\*Frozen Lightning\*\*, the most powerful upgrade currently available in the game, unlocking only when the

player has both \*\*Remorseless Winter\*\* (Frost Warden) and \*\*Lethal Surge\*\* (Bolt Striker) active in the same run.

Frozen Lightning is implemented as a passive synergy rule (not an independent ability with its own cooldown):

whenever Remorseless Winter freezes a target, that target becomes primed — if Lethal Surge triggers on a primed

target, it deals significantly amplified damage. This rewards the player for unlocking both heroes' strongest

apex and building toward this specific pair.



\## Scope



\### 1. Data Model — `ComboApexTalentDefinitionSO`

\- New SO type representing a cross-hero apex, distinct from the existing single-hero `ApexTalentDefinitionSO`

&#x20; (Task 29).

\- References exactly two `ApexTalentDefinitionSO` assets, each belonging to a different hero (e.g. Remorseless

&#x20; Winter from Frost Warden, Lethal Surge from Bolt Striker) — both must already be unlocked (per their own

&#x20; existing unlock conditions) for the combo to unlock.

\- Includes a `TriggerType` field (enum: `Passive`, `Active`) so future combo apexes can choose either style without

&#x20; a new SO type:

&#x20; - `Passive`: no independent cooldown/ability — defines a synergy rule between the two referenced apexes'

&#x20;   existing behavior (this task's case).

&#x20; - `Active`: would behave like a third independent automatic ability with its own cooldown (not used by this

&#x20;   task, but the field must exist and be documented so a future combo apex can use it without a schema change).

\- For `Passive`-type combo apexes, define the synergy effect as a modifier description (which apex "primes" a

&#x20; target, which apex "consumes" the primed state and how the bonus is calculated) — author Frozen Lightning's

&#x20; specific values per the effect described below.



\### 2. Frozen Lightning Behavior

\- When Remorseless Winter (Frost Warden's apex) freezes a target, that target is marked "primed" for a short

&#x20; window.

\- If Lethal Surge (Bolt Striker's apex) triggers on a primed target within that window, Lethal Surge's damage for

&#x20; that trigger is multiplied significantly, then the primed mark is consumed (cannot be reused until Remorseless

&#x20; Winter freezes that or another target again).

\- Placeholder values (final balance later):

&#x20; - Primed window: 2s after Remorseless Winter's freeze lands

&#x20; - Lethal Surge damage multiplier while consuming a primed target: x2.5 (applied on top of Lethal Surge's normal

&#x20;   damage calculation from Task 35, including its existing Static Charge/Execute bonuses)

\- This is the strongest upgrade in the game by design — the multiplier is intentionally large; do not soften it

&#x20; without explicit instruction.

\- Unlock requires both Remorseless Winter and Lethal Surge to be currently active/unlocked in the same run (i.e.

&#x20; both heroes selected per Task 37, and both apex conditions independently met per Task 29/35's existing logic) —

&#x20; no separate unlock animation/moment is required beyond however apex unlocks are currently surfaced to the

&#x20; player (Task 32's indicator pattern), extended to show this combo apex once both prerequisites are met.



\### 3. UI

\- Once unlocked, Frozen Lightning should be indicated to the player using the existing apex-indicator UI pattern

&#x20; from Task 32 — since this is passive (no cooldown of its own), the indicator can be a simple "active" badge/icon

&#x20; rather than a cooldown fill, distinct from the cooldown-bar style used for Remorseless Winter/Permafrost

&#x20; Eruption/Thunderstorm/Lethal Surge. Document this distinction.



\## Out of Scope (do not implement)

\- Any `Active`-type combo apex content (the field must exist for future use, but no active combo apex is

&#x20; designed or implemented in this task)

\- Additional combo apex pairs beyond Frozen Lightning

\- Real balance tuning of the x2.5 multiplier or 2s window beyond the placeholders specified above



\## Acceptance Criteria

\- \[ ] `ComboApexTalentDefinitionSO` exists, referencing two `ApexTalentDefinitionSO` assets from different heroes,

&#x20;     with a `TriggerType` field supporting `Passive` and `Active`

\- \[ ] Frozen Lightning unlocks only when both Remorseless Winter and Lethal Surge are active in the same run

\- \[ ] Remorseless Winter's freeze correctly primes its target for the specified window

\- \[ ] Lethal Surge correctly deals x2.5 damage (on top of its own existing bonuses) when triggering on a primed

&#x20;     target, and consumes the prime on use

\- \[ ] Primed state expires correctly if not consumed within the window

\- \[ ] UI shows a distinct passive "active" indicator for Frozen Lightning once unlocked, separate from

&#x20;     cooldown-bar-style indicators

\- \[ ] No SO asset is mutated at runtime

\- \[ ] No static `Instance` patterns introduced

\- \[ ] Full playtest: run with both heroes, unlock both Remorseless Winter and Lethal Surge, confirm Frozen

&#x20;     Lightning unlocks, confirm a Remorseless Winter freeze followed by a Lethal Surge trigger within 2s deals

&#x20;     the amplified damage, confirm the prime expires if Lethal Surge doesn't trigger in time



\## Reviewer Notes

Flag as blocking if:

\- Frozen Lightning is implemented as a third independent ability with its own cooldown instead of a passive

&#x20; synergy rule between the two existing apexes

\- The data model hardcodes this specific pair instead of generalizing via `ComboApexTalentDefinitionSO` with

&#x20; swappable apex references

\- `TriggerType` field is omitted, locking future combo apexes into the passive-only pattern

