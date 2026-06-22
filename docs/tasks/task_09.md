# Task 09 — Shop Tiers & Reroll System

> Read `CLAUDE.md` in full, and review the Task 01–08 implementations before starting. This task extends the existing Task 06 shop system with item tiers and a persistent reroll currency. No new item effects are added in this task — that's a later, separate task once this system is validated (per the project's placeholder-first approach).

## Goal

By the end of this task, the shop's `ConsumableDefinitionSO` items are organized into three tiers (T1 weakest, T2 mid, T3 strong), the shop offers a "Reroll" action that swaps out the current offered items at the cost of a reroll point, the player starts each run with 3 reroll points, and reroll points are a single persistent-for-the-run pool that never resets between shop visits — only consumed on use and replenished by purchasing a reroll potion (T1 potion = +1 reroll, T2 = +2, T3 = +3).

## Scope

### 1. `ConsumableDefinitionSO` — Add Tier Field
- Add a `tier` field (enum: `Tier1`, `Tier2`, `Tier3` or `int 1-3` — your call, document it).
- Retag/organize Task 06's existing consumable assets into tiers (decide sensible tier placement for what already exists — e.g. the weakest existing effect becomes T1, etc.). Document your tier assignments.
- Add one new `ConsumableDefinitionSO`: a **Reroll Potion**, authored in three tier variants (T1/T2/T3) granting +1/+2/+3 reroll points respectively on purchase. This is the only new item content this task — everything else is the tier/reroll system around existing items.

### 2. Reroll Point Tracking
- Add reroll point tracking to wherever per-run economy state already lives (`CurrencyManager`, or a new small `RerollManager`/field — your call given existing patterns from Task 03/06, document it).
- Starts at 3 at run start. Never resets on its own — only changes via consumption (reroll action, -1) or reroll potion purchase (+1/+2/+3 per tier).
- Must be correctly reset to 3 as part of Task 08's "Play Again" full-state-reset flow (this is a deliberate exception to "never resets" — it resets only at a brand new run start, not between shop visits within a run). Audit Task 08's reset logic and add reroll points to it.
- Expose current reroll count to the shop UI (read-only getter is enough).

### 3. Shop Offer Generation with Tiers
- Decide how tier affects what's offered: e.g. early shop visits skew toward T1, later visits (post wave 5/10, tying loosely into the wave-scaling direction discussed) skew toward higher tiers — OR keep it simple for this task (uniform random across all tiers/items) and leave tier-weighted offer probability as a documented follow-up once wave-scaling (a separate planned task) exists to hook into. Prefer the simpler option for this task unless it's trivial to do properly now — document your choice either way.
- Shop continues to offer a fixed-size set of items per visit (same count as Task 06 — don't change the offer size in this task).

### 4. Reroll Action (Shop UI)
- Add a "Reroll" button to the existing Task 06 shop screen, showing current reroll point count, disabled when count is 0.
- On click: if reroll points > 0, decrement by 1, re-roll the currently offered item set (same generation logic as §3, just re-invoked), and refresh the UI. Does not affect currency — reroll points and currency are separate resources.
- Reroll Potion purchases happen via the existing `ShopController.TryPurchase` flow (Task 06) — purchasing one applies its effect by incrementing reroll points (per §1/§2), exactly like any other consumable effect application, not a special-cased purchase path.

## Out of Scope (do not implement)
- New non-reroll consumable effect types (that's a later task)
- Tier-weighted offer probability tied to wave number (only do this now if trivial; otherwise it's a documented follow-up)
- Visual tier styling/card art (text label like "[T2]" next to item name is enough)
- Hero-specific or upgrade-pool tiering (this task is shop/consumables only, not the XP level-up upgrade pool)

## Acceptance Criteria
- [ ] `ConsumableDefinitionSO` has a `tier` field; existing Task 06 items are assigned sensible tiers
- [ ] Reroll Potion exists in three tier variants, each granting the correct reroll point amount on purchase via the normal `TryPurchase` flow
- [ ] Reroll points start at 3 per fresh run, persist across shop visits within a run (never auto-reset), and are correctly included in Task 08's full run-reset flow
- [ ] Shop UI shows current reroll count and a working Reroll button that decrements points and re-rolls the offer
- [ ] Reroll button is disabled/non-functional at 0 reroll points
- [ ] Reroll action does not deduct currency; potion purchases do not deduct reroll points (the two resources stay independent)
- [ ] No SO asset is mutated at runtime
- [ ] No static `Instance` patterns introduced
- [ ] Full playtest: Play → reach shop → see reroll count (3) → reroll items a couple times → count decreases → buy a Reroll Potion → count increases by the correct tier amount → trigger "Play Again" (Task 08) → reroll count resets to 3 for the new run

## Reviewer Notes
Flag as blocking if:
- Reroll Potion purchase uses a special-cased code path instead of going through the existing `ShopController.TryPurchase` → effect-application flow
- Reroll points reset anywhere other than the Task 08 full-run-reset flow (e.g. accidentally resetting on each shop open would defeat the entire point of this task)
- Reroll and currency resources are conflated (e.g. reroll consuming currency, or currency purchases affecting reroll count)
