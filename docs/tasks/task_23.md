# Task 23 — New Shop Consumable Types (Crit, Elemental, Ultimate Duration, Basic Damage)

> Read `CLAUDE.md` in full, and review Task 06/09 (shop core, tiers) and Task 12 (gear stat-modifier pipeline) before starting. This task adds new consumable effect types to the shop's item pool, expanding beyond Task 09's reroll-potion-only new content.

## Goal

The shop can offer several new consumable types — a Crit Chance/Damage potion, Elemental (Frost/Lightning) potions, an Ultimate Duration potion, and a Basic Attack Damage potion — each correctly plugging into the existing `AbilityRuntime` modifier pipeline, each authored across the existing T1/T2/T3 tiers (Task 09) with scaling magnitude per tier.

## Scope

### 1. Crit System (New Mechanic)
- The project has no crit-chance/crit-damage concept yet — this task introduces it minimally: `AbilityRuntime`'s damage computation gains an optional crit roll (chance to deal bonus damage, magnitude separate from chance) as a further step in the existing modifier pipeline (after level/tag/gear/consumable modifiers, crit is a final multiplicative roll) — base crit chance/damage are 0%/0 bonus by default (no crit without a consumable/upgrade granting it), consistent with the project's pattern of additive systems that do nothing until granted.
- Crit Potion (T1/T2/T3): grants flat crit chance % and/or crit damage % bonus (e.g. T1 +5% chance, T2 +10%, T3 +15%, or split chance/damage across two potion variants — your call, document it).

### 2. Elemental Potions (Frost/Lightning)
- Frost Potion: amplifies Frost Warden's frost-stack effects specifically (e.g. +stack-gain-per-hit or +slow-per-stack) — since Frost Warden is currently the only hero with a frost-themed kit (Task 19), document how this potion behaves for a hero without frost mechanics (e.g. no-op, or a generic minor slow-on-hit fallback — your call, document it).
- Lightning Potion: since no Lightning-themed hero/ability exists yet, implement this as a generic effect for now (e.g. a flat damage or attack-speed bonus tagged `Elemental_Lightning`) that can be meaningfully upgraded into something Lightning-specific once a Lightning hero exists (per the earlier-discussed multi-hero idea) — document this as an intentional placeholder.
- Both authored across T1/T2/T3 with scaling magnitude.

### 3. Ultimate Duration Potion
- Increases the active duration of whatever ability the hero's `UltimateAbilityDefinitionSO` defines (e.g. Frost Zone's 6s base duration) — plugs into the same modifier pipeline as other duration-affecting upgrades (Task 19's "Extended Zone"), not a separate calculation. T1/T2/T3 scaling.

### 4. Basic Attack Damage Potion
- Flat or percentage damage bonus to the basic ability, same modifier pipeline as existing damage modifiers. T1/T2/T3 scaling.

### 5. Shop Integration
- All new items are `ConsumableDefinitionSO` assets (Task 06/09 pattern), tiered, purchasable through the existing `ShopController.TryPurchase` flow with no new purchase code path — consistent with Task 09's reroll potion precedent.

## Out of Scope (do not implement)
- A real Lightning hero or Lightning-specific ability kit (that's the earlier-discussed multi-hero idea, a separate future task)
- Crit visual feedback (e.g. a "Crit!" popup) — can follow in a future visual pass
- New rarity tiers or shop mechanics beyond what Task 09 already supports

## Acceptance Criteria
- [ ] Crit chance/damage mechanic implemented as a final step in the existing `AbilityRuntime` modifier pipeline, defaulting to 0% (no behavior change) until granted
- [ ] Crit, Frost, Lightning, Ultimate Duration, and Basic Attack Damage potions exist as `ConsumableDefinitionSO` assets across T1/T2/T3 with sensible scaling magnitude per tier
- [ ] All new consumables apply their effects through the existing modifier pipeline (no parallel calculation), purchasable via the existing `ShopController.TryPurchase` flow
- [ ] Frost Potion's behavior for non-Frost-Warden heroes is implemented and documented (no-op or generic fallback, your choice)
- [ ] No SO asset is mutated at runtime
- [ ] No static `Instance` patterns introduced
- [ ] Full playtest: Play → buy one of each new potion type across a run → confirm each visibly changes the relevant stat (crit damage occasionally spikes, Frost Warden's stacks/slow are stronger, ultimate lasts longer, basic damage is higher)

## Reviewer Notes
Flag as blocking if:
- Crit, elemental, duration, or damage effects are applied via a new parallel calculation path instead of extending the existing `AbilityRuntime` modifier pipeline
- A new consumable purchase uses a different code path than `ShopController.TryPurchase`
