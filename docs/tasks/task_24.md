# Task 24 — Luck Stat System

> Read `CLAUDE.md` in full, and review Task 09/17 (shop tiers, offer generation), Task 12 (gear/artifact equip,
> persistence), Task 13 (loot tables, rarity weighting, boss-exclusive lock), and Task 22 (stat panel) before
> starting. This task introduces a new non-combat stat that primarily reshapes shop potion/elixir tier odds, with
> a secondary, deliberately weaker effect on loot drop tier odds.

## Goal

Add a **Luck** stat composed of a hero base value, a sum of equipped gear/artifact bonuses, and a temporary
in-run bonus from new Luck potions. Luck should make better-tier shop offers progressively more likely as the
player accumulates Luck and survives further into a run, with only a minor, intentionally limited effect on loot
drop tiers — Luck's main purpose is shop potion improvement, not gear farming.

## Scope

### 1. Luck Sources
- `HeroDefinitionSO`: add a `baseLuck` field — a small per-hero starting value (placeholder magnitude, designer-tunable).
- Gear/Artifact SO (Task 12): add a `luckBonus` field, authored per item like other stat bonuses already on that asset.
- `HeroRuntime` tracks Luck as the sum of three parts: hero base, the total of `luckBonus` across all six equipped
  slots, and a separate runtime-only bonus gained from Luck potions during the current run. The combined total is
  clamped to a 0–100 display range.
- The gear-derived portion persists between runs the same way other equip data already persists (Task 12's JSON
  save). The potion-derived portion does not persist — it resets to zero at the end of each run, the same way other
  per-run-only state (in-run currency, XP, consumable inventory per CLAUDE.md §6) already resets.
- Recalculate the gear-derived portion whenever equipment changes (reuse or add an equip-changed event), not every
  frame.

### 2. Luck Potions (New Consumables)
- Three new `ConsumableDefinitionSO` assets, following the same authoring pattern as Task 23's new potions: Luck
  Potion I, II, and III, granting an increasing flat Luck bonus per tier (placeholder magnitudes, e.g. roughly
  5/10/15 — exact balance to be tuned later).
- These potions appear in the shop's existing tiered offer pool like any other consumable (Task 09 pattern) — no
  new purchase path, routed through the existing `ShopController.TryPurchase` flow.
- On consumption, the granted amount adds to the runtime-only Luck bonus described above.

### 3. Shop Tier Weighting
- Extend the shop's existing offer-tier-roll logic (Task 09/17) so that the odds of rolling a higher-tier
  consumable increase with the player's current total Luck and with how far the run has progressed (current wave).
  Both factors should matter — a low-Luck player late in a run, and a high-Luck player early in a run, should both
  see some improvement over the current flat/static odds, with Luck weighted as the stronger of the two factors.
- Implement this as a shared, reusable weighting step rather than duplicating tier-roll logic — it should be
  straightforward to apply the same approach to loot tables (see below) with a different strength setting.
- Author the tunable inputs (base tier odds, how strongly each tier benefits from increasing Luck/wave progress, the
  wave value at which the wave-progress contribution maxes out) as fields on a new or existing config asset, not as
  hardcoded values in code — consistent with CLAUDE.md §4's no-magic-numbers rule.
- No tier's odds should ever drop to zero — the lowest tier should always remain reachable, just decreasingly likely.

### 4. Loot Table Weighting (Secondary, Weaker Effect)
- Apply the same weighting approach to Task 13's loot table rolls, but with a clearly weaker influence than the
  shop (e.g. roughly a quarter of the shop's strength, as a tunable multiplier — exact value to be balanced later).
- This must not touch or bypass the existing boss-exclusive lock on the highest rarity tiers (Legendary/Unique) —
  Luck only reweights odds among the tiers already eligible to drop at that point in the run; it never makes a
  boss-locked tier reachable outside a boss wave.

### 5. Stat Panel
- Add a Luck row to the existing in-game stat panel (Task 22), reading the live computed total directly from
  `HeroRuntime` — consistent with that panel's existing convention of reading real runtime values rather than
  approximating.

## Out of Scope (do not implement)
- Any interaction between Luck and combat stats/damage calculations — Luck is strictly non-combat
- Final balance numbers — all magnitudes (base Luck per hero, gear Luck bonuses, potion magnitudes, tier weighting
  inputs) are placeholders for now and are expected to be tuned later in the editor
- Any change to which rarity tiers are boss-exclusive

## Acceptance Criteria
- [ ] Luck correctly combines hero base + summed equipped gear/artifact bonuses + in-run potion bonus, clamped to
      a 0–100 range
- [ ] Equip-derived Luck persists across runs via existing save data; potion-derived Luck resets to zero at run end
- [ ] Three Luck Potion tiers exist as `ConsumableDefinitionSO` assets, purchasable through the existing
      `ShopController.TryPurchase` flow, and appear in the shop's normal tiered offer rotation
- [ ] Shop offer tier odds visibly shift toward higher tiers as Luck and/or current wave increase (testable via
      debug: set Luck high, reroll repeatedly, confirm higher tiers appear more often than at low Luck)
- [ ] Loot table tier odds shift only slightly with Luck; boss-exclusive tiers remain unreachable outside boss waves
- [ ] Stat panel displays the current total Luck value
- [ ] No SO asset is mutated at runtime
- [ ] No static `Instance` patterns introduced
- [ ] All weighting inputs and potion magnitudes live in SO/config fields, not hardcoded in code

## Reviewer Notes
Flag as blocking if:
- Loot table reweighting bypasses or weakens the existing boss-exclusive rarity lock from Task 13
- Shop and loot tier weighting are implemented as two separate, duplicated calculations instead of one shared
  approach with a strength/multiplier difference
- Any tier's odds can reach zero
- Luck potions use a different purchase code path than `ShopController.TryPurchase`