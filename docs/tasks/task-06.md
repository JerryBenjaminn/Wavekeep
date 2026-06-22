# Task 06 — Shop System

> Read `CLAUDE.md` in full, and review the Task 01–05 implementations before starting. This task builds the between-wave shop: `ConsumableDefinitionSO`, `ShopController`, and the purchase flow that finally calls the `CurrencyManager.TrySpend` API Task 03 built but left unused.

## Goal

By the end of this task, the player sees a shop screen between waves, can spend currency on potions/elixirs that grant run-bonuses, and the existing `WaveSpawner` correctly pauses for shop interaction between waves rather than spawning continuously.

## Scope

### 1. `ConsumableDefinitionSO` (flesh out from Task 01 stub)
Fields:
- `itemName`, `icon`, `price`
- `effectType` enum (e.g. `FlatDamageBoost`, `CooldownReduction`, `TemporaryShield`, `HealWall` — pick a small starting set that's meaningful given the existing wall-HP and ability systems; document your final list)
- `effectValue`, `duration` (use `0`/`-1` convention for "permanent for this run" vs. timed — document your choice)
- `stackable` bool (can the player buy multiples of the same item in one run, e.g. for stacking flat boosts)

### 2. Effect Application
- Decide where consumable effects apply given the existing systems: e.g. `FlatDamageBoost` should plug into the same modifier pipeline `AbilityRuntime` already uses for `TagInteractionRule`-driven modifiers (Task 04) — ideally consumable effects are just another modifier source `AbilityRuntime` checks, not a separate parallel system. `HealWall` calls into the existing `WallRuntime` (Task 02). Document how each effect type wires into existing systems rather than introducing parallel ad-hoc logic.
- Active consumable effects for the current run are held in something like a `ConsumableInventory` (mirroring Task 04's `UpgradeInventory` pattern) — owned by `GameSession` or wherever `UpgradeInventory` ended up living (per Task 05's documented decision), for consistency.

### 3. `ShopController` (Scripts/Economy or Scripts/Shop — document choice)
- Reads a configurable list of available `ConsumableDefinitionSO` (a fixed shop assortment per run is fine for MVP — randomized/rotating shop inventory is a later refinement, not required now).
- `TryPurchase(ConsumableDefinitionSO item) : bool` — checks `stackable` rules, calls `CurrencyManager.TrySpend(item.price)`, and on success applies the effect (per §2) and adds it to `ConsumableInventory`.
- Publishes a `CurrencyChangedEvent` indirectly via `CurrencyManager` (no new event needed) — but consider whether a `ConsumablePurchasedEvent` is useful for UI feedback; add it if it meaningfully simplifies the UI wiring, skip it if not (your call).

### 4. Shop UI Screen
- A simple Canvas-based screen (reuse/extend existing UI patterns from Task 03/05) shown between waves: lists available consumables with name/price/short description, a buy button per item, current currency total visible, and a "Continue" button to proceed to the next wave.
- Buy button calls `ShopController.TryPurchase` and updates state (e.g. disable/grey out if currency insufficient, or if non-stackable item already owned).
- No fancy art — text + buttons is sufficient, consistent with the project's placeholder-first approach.

### 5. Wave/Shop Flow Integration
- Wire the shop screen to appear after `WaveCompletedEvent` fires (Task 02) and before the next wave's `WaveStartedEvent` — i.e. `WaveSpawner` (or whatever orchestrates the run flow) must pause between waves for shop interaction rather than starting the next wave immediately.
- Check how Task 02's `WaveSpawner` currently sequences waves (immediately back-to-back, or with any existing gap) and adjust minimally to insert this pause point — document what you changed and why, since this touches Task 02's existing flow.
- "Continue" button (or shop auto-closing after a timer, if you prefer — document your choice) resumes wave spawning.

## Out of Scope (do not implement)
- Randomized/rotating shop inventory (fixed assortment is fine for now)
- Shop UI visual polish
- New consumable effect types beyond the small starting set (easy to add more later since they're SO-driven)
- Persisting purchases between runs

## Acceptance Criteria
- [ ] `ConsumableDefinitionSO` fully fleshed out per the fields above, with at least 3 distinct items authored
- [ ] `ShopController.TryPurchase` correctly validates funds via `CurrencyManager.TrySpend`, respects `stackable` rules, and applies effects on success
- [ ] At least one consumable effect type plugs into the existing `AbilityRuntime` modifier pipeline rather than introducing a parallel damage-modification path
- [ ] Wall-affecting consumable (if authored) correctly calls into existing `WallRuntime` rather than a new parallel wall-health path
- [ ] Shop UI appears between waves, correctly blocks/pauses next-wave spawning until the player continues
- [ ] Currency total displayed in shop updates live and matches `CurrencyManager.CurrentCurrency`
- [ ] No SO asset is mutated at runtime
- [ ] No static `Instance` patterns introduced
- [ ] Full playtest: Play → wave 1 clears → shop appears → purchase an item → currency deducts correctly → continue → wave 2 starts → purchased effect is visibly active (e.g. measurable damage/cooldown difference, or wall HP visibly restored)

## Reviewer Notes
Flag as blocking if:
- Consumable effects are applied via a new, separate damage/effect calculation path instead of plugging into the existing `AbilityRuntime`/`WallRuntime` systems
- `TryPurchase` allows a purchase to succeed without actually deducting currency, or allows currency to go negative
- The wave-to-shop-to-wave flow change introduces a hardcoded hero-specific or wave-specific branch rather than a general pause/resume hook
- `ConsumableDefinitionSO`/other SO fields are written to at runtime
