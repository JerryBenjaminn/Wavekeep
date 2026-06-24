\# Task 30 — Migrate Generic Stat Upgrades to Shop Consumables



> Read `CLAUDE.md` in full, and review Task 06/09 (shop core, tiers), Task 23/24 (consumable authoring pattern,

> effect pipeline), and Task 29 (upgrade line system, which removed the old shared generic pool from level-up

> cards) before starting. Task 29 removed the shared generic upgrade pool from level-up cards but did not migrate

> its content anywhere — this task moves that content into the shop as consumables, following the existing

> potion/elixir authoring pattern.



\## Goal



Every generic stat upgrade that used to be part of the shared level-up card pool (e.g. flat damage bonuses, AoE

burst, slow field, and other non-hero-exclusive effects from the old §3.8 shared pool) is recreated as a

`ConsumableDefinitionSO` potion/elixir, purchasable through the existing shop flow, authored across the existing

T1/T2/T3 tiers (Task 09 pattern) consistent with how Task 23/24's potions were added.



\## Scope



\### 1. Content Migration

\- Identify every effect that existed in the old shared generic upgrade pool (before Task 29 removed it from level-up

&#x20; cards) — check version history / the pre-Task-29 pool contents if still accessible in the project, or the design

&#x20; notes in earlier task files if the pool's contents are documented there.

\- For each distinct effect, create a new `ConsumableDefinitionSO` (or reuse an existing one if a near-equivalent

&#x20; potion already exists from Task 23/24 — avoid duplicating, e.g. if a "Basic Attack Damage Potion" already

&#x20; covers what used to be a generic flat-damage upgrade, just confirm it covers the same ground rather than

&#x20; creating a second item).

\- Author each across T1/T2/T3 tiers with scaling magnitude, naming and tier conventions consistent with existing

&#x20; shop items.

\- Apply effects through the existing `AbilityRuntime` modifier pipeline (Task 23's established pattern) — no new

&#x20; parallel calculation path.



\### 2. Shop Integration

\- All migrated items are purchasable via the existing `ShopController.TryPurchase` flow and appear in the shop's

&#x20; normal tiered offer rotation alongside existing consumables — no new purchase code path.



\## Out of Scope (do not implement)

\- Any change to the upgrade line / apex talent system from Task 29

\- New effect types beyond what the old generic pool already had (this is a migration, not new design)

\- Balance tuning of magnitudes — placeholder/sensible-default scaling per tier is fine, real tuning happens later



\## Acceptance Criteria

\- \[ ] Every distinct effect from the old shared generic upgrade pool exists as a `ConsumableDefinitionSO`,

&#x20;     either newly created or confirmed already covered by an existing Task 23/24 potion

\- \[ ] No effect from the old pool is silently dropped — report any that were intentionally excluded and why

\- \[ ] All migrated items are authored across T1/T2/T3 tiers and purchasable through the existing

&#x20;     `ShopController.TryPurchase` flow

\- \[ ] All effects apply through the existing `AbilityRuntime` modifier pipeline, no parallel calculation path

\- \[ ] No SO asset is mutated at runtime



\## Reviewer Notes

Flag as blocking if:

\- An old generic pool effect is dropped without being reported/justified

\- A new consumable uses a different purchase code path than `ShopController.TryPurchase`

\- A duplicate consumable is created when an equivalent one already exists from Task 23/24

