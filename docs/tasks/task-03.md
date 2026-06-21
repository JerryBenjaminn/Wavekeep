# Task 03 — Currency & XP Systems

> Read `CLAUDE.md` in full, and review the Task 01 and Task 02 implementations before starting. This task wires up the two reward pipelines (Currency and XP) that Task 02 already produces data for but doesn't consume. No shop UI, no level-up ability picker yet — just the managers, the event consumption, and basic on-screen numbers so the loop is verifiable.

## Goal

Implement `CurrencyManager` and `XPManager` as services owned by `GameSession`, wire them to consume `EnemyKilledEvent` (already published by Task 02's `EnemyRuntime.Die()`), and publish `CurrencyChangedEvent`/`XPLevelUpEvent` so future UI/systems can react. Add minimal on-screen text (currency total, XP/level) purely for verification — this is not the final HUD.

## Scope

### 1. `EnemyKilledEvent` Payload Check
- Confirm (and extend if needed) `EnemyKilledEvent` carries enough data for reward distribution: at minimum a reference to the `EnemyDefinitionSO` (or just the `currencyReward`/`xpReward` values directly — your call, but if passing the SO reference, the consumer reads `currencyReward`/`xpReward` from it, never writes back to it).

### 2. `CurrencyManager` (Scripts/Core or Scripts/Economy — document your choice)
- Plain C# class, owned by `GameSession` (no static singleton), following the same non-static pattern as `EnemyPoolManager`/`EventBus`.
- Subscribes to `EnemyKilledEvent` via `EventBus`, adds `currencyReward` to a running total on each kill.
- Exposes `CurrentCurrency` (read-only getter) and a `TrySpend(int amount) : bool` method (validates sufficient funds, deducts, returns success) — needed later for the shop, but build it now since it's trivial and the manager owns the only mutable total.
- Publishes `CurrencyChangedEvent` (carrying new total) whenever the total changes, whether from a kill or a future spend.

### 3. `XPManager` (Scripts/Core or Scripts/Economy)
- Same non-static pattern, owned by `GameSession`.
- Subscribes to `EnemyKilledEvent`, adds `xpReward` to a running XP total on each kill.
- Holds a simple level-up curve: a flat or simple formula-based XP-per-level threshold is fine for MVP (e.g. `xpToNextLevel = baseXP + (level * increment)` — store `baseXP`/`increment` as serialized/configurable values, not hardcoded magic numbers, so they're easy to tune).
- When accumulated XP crosses the threshold for the current level: increment `CurrentLevel`, carry over leftover XP (don't discard remainder), and publish `XPLevelUpEvent` (carrying new level). Handle multiple level-ups in a single large XP gain correctly (e.g. a big kill reward could cross two thresholds at once) rather than only checking once per kill.
- Exposes `CurrentLevel`, `CurrentXP` (progress within current level), and `XPToNextLevel` (read-only getters) for future UI.

### 4. Minimal On-Screen Verification Display
- Add simple `TextMeshProUGUI` (or basic `Text` if TMP isn't already in the project — check Task 01's Canvas setup) elements on the existing Canvas: one for currency total, one for current level + XP progress (e.g. "Lv. 2 — 30/50 XP").
- These subscribe to `CurrencyChangedEvent`/`XPLevelUpEvent` (and update XP progress text on every kill, not just level-ups) to update text — this proves the event pipeline works end-to-end without needing the real HUD design yet.
- This display is explicitly temporary/placeholder — don't invest time in layout/styling polish.

### 5. Wiring
- `CurrencyManager` and `XPManager` are instantiated and owned by `GameSession` (extending the placeholder slots left in Task 01), subscribing to the existing `EventBus` instance on `GameSession`, not a new one.
- Both managers must unsubscribe cleanly on session teardown, consistent with the `EventBus` lifecycle rule from CLAUDE.md §3.5.

## Out of Scope (do not implement)
- Shop UI or actual potion/elixir purchasing (TrySpend exists as an API but nothing calls it yet)
- Ability upgrade picker UI (XPLevelUpEvent fires, but no card-selection screen consumes it yet — that's a later task per CLAUDE.md §8)
- Final HUD art/layout — today's text is throwaway
- Persisting currency/XP between runs (single-run scope only, per CLAUDE.md §6)

## Acceptance Criteria
- [ ] `CurrencyManager` and `XPManager` exist as non-static classes owned by `GameSession`
- [ ] Both correctly subscribe to `EnemyKilledEvent` and accumulate rewards from Task 02's existing kill pipeline with no changes needed to `EnemyRuntime`'s death logic
- [ ] `CurrencyManager.TrySpend` correctly validates and deducts funds, returning false on insufficient balance, without going negative
- [ ] `XPManager` correctly handles level-up including the case of a single XP gain crossing multiple level thresholds at once
- [ ] `CurrencyChangedEvent` and `XPLevelUpEvent` are published correctly and consumed by the placeholder UI text
- [ ] Playing the Task 02 test scene now shows currency and level/XP numbers updating live as enemies die
- [ ] No SO asset is mutated at runtime (reward values are read from `EnemyDefinitionSO`, never written to it)
- [ ] No static `Instance` patterns introduced

## Reviewer Notes
Flag as blocking if:
- `CurrencyManager`/`XPManager` use static singleton access instead of being owned/injected via `GameSession`
- XP threshold values or currency reward logic are hardcoded as magic numbers instead of configurable fields
- A large single XP reward that should cross multiple levels only triggers one level-up
- `TrySpend` allows currency to go negative or doesn't validate before deducting
