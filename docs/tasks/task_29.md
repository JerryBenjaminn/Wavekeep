\# Task 29 — Upgrade Line System (Architecture Migration)



> Read `CLAUDE.md` in full, and review Task 04 (ability system, IAbility/AbilityRuntime), Task 07 (level-up card

> flow), and Task 19 (Frost Warden's existing hero-exclusive upgrades, tags, TagInteractionRule) before starting.

> This task replaces the §3.8 tag-based upgrade-pool model with a structured per-skill upgrade-line model. This is

> a structural migration — it intentionally reuses Frost Warden's existing upgrade content reshaped into the new

> format, rather than designing new content. New Frost Warden content is a separate, later task.



\## Goal



Replace the current level-up card model (shared generic pool + hero-exclusive pool, both tag-driven) with a model

where each hero's skill (currently: Basic and Ultimate) has multiple independent \*\*upgrade lines\*\*. Each line has

three tiers of increasing effect. Lines progress independently and in parallel — a player can be investing in

several lines on the same skill at once. When a hero has at least two of its lines at Tier 3, an \*\*Apex Talent\*\*

becomes available: a new, independent, automatically-triggering ability with its own cooldown, separate from the

hero's player-controlled Basic and Ultimate.



This task also removes the old shared generic upgrade pool from level-up cards entirely (those generic stat

upgrades move to the shop as consumables in a separate task) and removes/retires the `TagInteractionRule` model in

favor of upgrade lines as the sole hero-exclusive progression mechanism going forward.



\## Scope



\### 1. Data Layer



\- New `UpgradeLineDefinitionSO`:

&#x20; - Identifies which hero and which skill (Basic or Ultimate, via reference to that hero's

&#x20;   `BasicAbilityDefinitionSO`/`UltimateAbilityDefinitionSO`) it belongs to.

&#x20; - Display name and per-tier description text (3 tiers).

&#x20; - Per-tier effect definition — reuse the existing numeric-modifier and `StatusEffectType`+magnitude pattern

&#x20;   already used by today's upgrade system, just scoped per tier instead of per flat upgrade.

\- New `ApexTalentDefinitionSO`:

&#x20; - References the hero it belongs to.

&#x20; - References two (or more) `UpgradeLineDefinitionSO` assets — all of which must reach Tier 3 for this apex to

&#x20;   unlock.

&#x20; - Defines the apex ability's own effect/behavior and cooldown, following the existing `IAbility` pattern as

&#x20;   closely as possible (it is a new ability instance, not a modifier on an existing one).

\- `HeroDefinitionSO`: replace the existing `TagInteractionRule` list with a list of `UpgradeLineDefinitionSO`

&#x20; references (the lines this hero owns) and a list of `ApexTalentDefinitionSO` references (the apexes this hero can

&#x20; unlock). Existing `UpgradeTag`/`TagInteractionRule` types can be removed once nothing references them — confirm

&#x20; nothing else in the codebase depends on them before deleting.

\- Migrate Frost Warden's existing hero-exclusive upgrades (Task 19/20) into this new line format as a 1:1 reshape

&#x20; where reasonable (e.g. an existing 3-step upgrade becomes a 3-tier line) — exact regrouping is an implementation

&#x20; judgment call, document how you mapped old upgrades to new lines. Content correctness/balance is not the

&#x20; concern here; structural correctness is.



\### 2. Runtime Layer



\- `HeroRuntime` tracks, per owned `UpgradeLineDefinitionSO`, a current tier (0 = not yet picked, 1–3 = current

&#x20; tier). This replaces whatever flat "list of picked upgrade IDs" tracking currently exists for hero-exclusive

&#x20; upgrades.

\- After any line reaches Tier 3, check all `ApexTalentDefinitionSO`s owned by the hero: if all of an apex's

&#x20; required lines are now at Tier 3, instantiate that apex as a new runtime ability instance, ticking its own

&#x20; cooldown and triggering automatically (no player input) — consistent with the project's no-static-singleton,

&#x20; constructor/init-injection pattern (§3.5) for wiring it into `HeroRuntime`'s update/tick path.

\- Level-up card generation (`LevelUpCardPicker`, Task 07): replace the shared+hero-pool draw with a draw from the

&#x20; current hero's (or current heroes', if multiple are active — single hero is fine for this task) lines that are

&#x20; not yet at Tier 3. Selection should remain random among eligible lines, consistent with current card-picker

&#x20; behavior, just drawing from a different source set.

\- Remove the old shared generic upgrade pool from the level-up draw entirely — it is out of scope for this task to

&#x20; also build the shop-side replacement (that's a separate task), so for now those generic upgrades simply stop

&#x20; appearing in level-up cards. Note in your report that the generic pool's shop migration is pending as a known

&#x20; follow-up, not a bug.



\## Out of Scope (do not implement)

\- New/rebalanced Frost Warden line content beyond a structural 1:1 reshape of what already exists

\- Moving the old generic upgrade pool into the shop as consumables (separate task)

\- Multi-hero team selection in the hub (separate task)

\- New apex talent designs beyond wiring the mechanism itself (a placeholder apex definition for Frost Warden

&#x20; using two of its migrated lines is sufficient to prove the mechanism works end-to-end)



\## Acceptance Criteria

\- \[ ] `UpgradeLineDefinitionSO` and `ApexTalentDefinitionSO` exist and follow existing SO authoring conventions

\- \[ ] `HeroDefinitionSO` references lines and apexes instead of `TagInteractionRule`; old tag-interaction types

&#x20;     removed once confirmed unused

\- \[ ] Frost Warden's existing hero-exclusive upgrades are reshaped into at least two `UpgradeLineDefinitionSO`

&#x20;     lines with 3 tiers each, documented mapping from old upgrades to new lines

\- \[ ] At least one `ApexTalentDefinitionSO` exists for Frost Warden, requiring two of its lines at Tier 3

\- \[ ] `HeroRuntime` correctly tracks per-line tier progress and triggers apex unlock when conditions are met

\- \[ ] Apex ability runs automatically on its own cooldown once unlocked, with no player input required

\- \[ ] Level-up cards draw only from the active hero's not-yet-maxed lines; the old shared generic pool no longer

&#x20;     appears in level-up cards

\- \[ ] No SO asset is mutated at runtime

\- \[ ] No static `Instance` patterns introduced

\- \[ ] Full playtest: play a run, level up Frost Warden through two lines to Tier 3 each, confirm apex

&#x20;     ability triggers automatically with its own cooldown



\## Reviewer Notes

Flag as blocking if:

\- Apex talent is implemented as a passive stat modifier instead of an independent, automatically-triggering

&#x20; ability with its own cooldown

\- Line progression is tracked as a flat list of picked upgrades instead of per-line tier state

\- `TagInteractionRule` is removed while something else in the codebase still depends on it (verify before deleting)

\- Card picker still references or draws from the old shared generic upgrade pool

