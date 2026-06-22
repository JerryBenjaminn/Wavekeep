# Task 17 — Shop Purchase Limits & Reduced Shop Frequency

> Read `CLAUDE.md` in full, and review the Task 01–16 implementations before starting, especially Task 06 (shop core), Task 09 (tiers/reroll). This task fixes two shop issues found in playtesting: (1) the player can repeatedly buy the same offered item multiple times in one shop visit, draining currency into one stacked effect; (2) the shop currently appears too often (every wave), which should be reduced to every 5 waves.

## Goal

By the end of this task: each of the shop's offered items can be purchased at most once per shop offer — buying it disables/removes it from the current offer until the next reroll (which generates a fresh purchasable set) or the next shop visit; and the shop only appears every 5 waves instead of every wave.

## Scope

### 1. Per-Offer Purchase Limit
- Extend the shop's offer-tracking state (wherever the current offered item list lives, per Task 06/09) to track which of the currently-offered items have already been purchased this offer.
- `ShopController.TryPurchase` (or the UI layer calling it) must reject/block re-purchasing an item already bought from the current offer — UI should grey out/disable the buy button for already-purchased items, consistent with how insufficient-funds is already handled (per Task 06).
- **Reroll resets this:** triggering a reroll (Task 09) generates a fresh offer set, and the purchased-tracking resets accordingly (a freshly rolled offer has nothing marked as purchased yet, even if the same item happens to reroll into the new offer).
- A new shop visit (next wave's shop) also starts with a fresh, unpurchased offer — confirm this already happens naturally from how offers are generated per visit (per Task 06/09), don't introduce a redundant reset mechanism if one already exists.
- This applies per-item-in-the-offer, not a single global "X purchases per visit" counter — e.g. if the shop offers 4 distinct items, the player can buy each of those 4 exactly once, for up to 4 purchases total per offer (then must reroll or wait for next visit for more).

### 2. Reduced Shop Frequency
- Change the wave-to-shop integration (Task 06 §5: shop appears after `WaveCompletedEvent`, before next `WaveStartedEvent`) so the shop only triggers every 5th wave-completion (waves 5, 10, 15...) rather than every wave. Waves that don't trigger the shop should proceed directly from `WaveCompletedEvent` into the next `WaveStartedEvent` with no pause.
- Make the frequency a configurable value (e.g. `shopIntervalWaves = 5`), not a hardcoded `% 5` scattered inline, so it's easy to tune later without re-opening this task.
- Confirm this doesn't conflict with Task 10's milestone scaling (every 5 waves) or boss waves (every 10 waves) — document how shop timing relates to those (e.g. shop on wave 5 lines up with the first milestone; shop on wave 10 lines up with the first boss wave — confirm this is sensible and not awkward, e.g. shop appearing mid-boss-wave-resolution would be wrong; shop should still only appear after a wave's `WaveCompletedEvent`, including boss waves, per Task 02/10's existing "all enemies resolved" rule).

## Out of Scope (do not implement)
- New consumable effects or shop content
- Changing the reroll point system itself (Task 09's reroll mechanics are unchanged, just now interacting correctly with per-offer purchase tracking)
- Tier-weighted offer probability (still a documented future follow-up per Task 09/10)

## Acceptance Criteria
- [ ] Each shop-offered item can be purchased at most once per offer; buy button correctly disables after purchase
- [ ] Rerolling generates a fresh offer with all items purchasable again (fresh purchased-tracking)
- [ ] A new shop visit (next eligible wave) starts with a fresh, unpurchased offer
- [ ] Purchasing 4 distinct offered items in one visit correctly results in 4 separate purchases (not blocked), but attempting a 5th purchase of an already-bought item is blocked
- [ ] Shop now appears only every 5th wave (configurable interval, not hardcoded inline), with no pause/shop on the other 4 waves
- [ ] Boss waves (every 10th, a multiple of 5) correctly resolve fully (boss dead, `WaveCompletedEvent` fired) before the shop appears — no race condition or early trigger
- [ ] No SO asset is mutated at runtime
- [ ] No static `Instance` patterns introduced
- [ ] Full playtest: Play → waves 1–4 flow with no shop → wave 5 completes → shop appears → buy one item twice (second attempt blocked) → reroll → buy the same item again (now allowed, fresh offer) → continue → waves 6–9 flow with no shop → wave 10 (boss) completes → shop appears again

## Reviewer Notes
Flag as blocking if:
- Purchase-limit tracking is a single global counter instead of per-item-in-offer tracking (would incorrectly block buying multiple *different* items)
- Reroll doesn't correctly reset purchased-tracking for the new offer
- Shop interval is hardcoded as a magic number inline rather than a named configurable value
- Shop can trigger before a boss wave's boss is actually dead
