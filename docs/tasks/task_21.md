# Task 21 — Real Ultimate Cooldown & Visible Charge Bar

> Read `CLAUDE.md` in full, and review Task 04 (`AbilityRuntime`/`IAbility`) and Task 05 (`HeroRuntime`) before starting. This task replaces the debug-key-only ultimate trigger with a real cooldown-gated resource and a visible UI bar showing charge progress, so the player can see when the ultimate is actually ready.

## Goal

The ultimate ability uses `AbilityRuntime`'s existing `IsReady`/cooldown mechanism (already present since Task 04, just never enforced for the ultimate because it was purely debug-key-triggered) as a real gate — pressing the debug key (or a future real input) only triggers the ultimate if it's off cooldown — and a UI bar visibly fills as the ultimate charges, reaching full when ready.

## Scope

### 1. Enforce Ultimate Cooldown
- Confirm `AbilityRuntime.IsReady`/cooldown-tick logic (Task 04) already works correctly for the ultimate instance — it was built generically, just not gated on use before. Wire the debug-key trigger (and `HeroRuntime`'s ultimate-tick call) to check `IsReady` before calling `Execute`, and to start the cooldown timer on successful execution.
- Increase the ultimate's base cooldown to a meaningfully longer value appropriate for an arena-wide DoT+Slow effect (e.g. 30–45s as a starting placeholder — document your chosen value; this is a tunable, not a locked number).

### 2. Ultimate Charge Bar UI
- Add a simple UI element (Canvas-based, consistent with existing placeholder-tier UI) showing ultimate charge progress: empty/low at cast, filling over the cooldown duration, full and visually distinct (e.g. a color change or "Ready!" label) when `IsReady` becomes true.
- Read progress directly from `AbilityRuntime`'s existing cooldown-timer state (expose a `0–1` normalized progress getter if one doesn't already exist) — don't duplicate cooldown tracking in the UI layer.
- Update live every frame (or on a reasonable UI tick) so the player can watch it fill.

## Out of Scope (do not implement)
- Real player input replacing the debug key (still debug-key triggered, just now properly gated)
- Per-upgrade cooldown-reduction balancing beyond what already exists (Task 19's "Rapid Bolts" etc. are basic-ability only; ultimate cooldown reduction upgrades, if wanted, are a future task)
- Visual/art polish on the bar beyond a basic fill + ready-state indicator

## Acceptance Criteria
- [ ] Pressing the ultimate debug key while on cooldown does nothing (no execution, no effect)
- [ ] Pressing the ultimate debug key when ready executes it and immediately starts the cooldown
- [ ] UI bar visibly fills over the cooldown duration and clearly indicates "ready" state
- [ ] UI bar reads from `AbilityRuntime`'s existing cooldown state, no duplicated/parallel cooldown tracking
- [ ] No SO asset is mutated at runtime
- [ ] No static `Instance` patterns introduced
- [ ] Full playtest: Play → cast ultimate → bar resets and fills over the cooldown period → pressing the debug key mid-cooldown does nothing → bar reaches full → pressing the key now casts again

## Reviewer Notes
Flag as blocking if:
- The UI bar implements its own independent cooldown timer instead of reading `AbilityRuntime`'s actual state
- The ultimate can still be cast while on cooldown
