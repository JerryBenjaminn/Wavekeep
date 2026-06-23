# Task 19 — Frost Warden Full Kit, Frost Stack/Decay, & Branch Exclusivity

> Read `CLAUDE.md` in full (especially §3.8), and review Task 04 (abilities), Task 07 (card picker), and Task 11 (hero-exclusive upgrades, status effects) before starting. This task fully implements the Frost Warden hero's basic ability and ultimate as designed, adds a new "stacking status effect" mechanic (Frost stacks building toward a Freeze trigger, with decay over time), authors the full Mage/Defender hero-exclusive upgrade set, wires tag-interactions to the generic pool, and adds mutual-exclusivity logic so picking from one branch removes the opposite branch from the draw pool for the rest of the run.

## Goal

By the end of this task: Frost Warden's basic attack ("Frost Bolt Burst") applies AoE damage + a stacking Frost effect that triggers a Freeze at max stacks and decays over time if not refreshed; the ultimate ("Frost Zone") applies arena-wide DoT + Slow for its duration; 9 hero-exclusive upgrades (1 neutral + 4 Mage + 4 Defender) exist and are correctly gated so that choosing a Mage or Defender upgrade permanently removes the opposite branch from that hero's draw pool for the rest of the current run; and two `TagInteractionRule` entries connect Frost Warden's abilities to the generic upgrade pool's `AoE` and `Slow` tags.

## Scope

### 1. Frost Stack/Decay Status Mechanic (extends Task 11's status-effect system)
- Add a new stacking status type distinct from Task 11's simple timed effects (Freeze/Slow/Burn): a per-enemy `FrostStack` counter (separate from, but capable of *triggering*, a `Freeze` application from Task 11's existing system).
- `EnemyRuntime` status state gains: current frost stack count, a per-stack movement-speed-reduction value, a decay timer (stack count decreases by 1 every N seconds if not refreshed by a new hit), and a max-stack threshold that, when reached, triggers a `Freeze` (via the existing Task 11 status API) and resets stacks to 0.
- This is implemented as a generic extension (e.g. a reusable "stacking effect" structure) rather than a Frost-Warden-specific hack in `EnemyRuntime`, so a future hero could reuse the same stacking-effect pattern with different parameters if needed — but don't over-engineer beyond what's needed for Frost specifically right now.
- Base values: +1 stack per basic-attack hit, -8% move speed per stack, max 5 stacks, decay -1 stack/4s if not refreshed, reaching max stack triggers Freeze (1.5s) and resets to 0.

### 2. Frost Warden Basic Ability — "Frost Bolt Burst"
- `AbilityDefinitionSO`: `AreaOfEffect` targeting, damage 8, cooldown 1.2s, AoE radius 3m.
- On hit: applies damage (existing pipeline) + applies/refreshes Frost stack (per §1) to each hit enemy.
- `TagInteractionRule`: matching the generic `AoE` tag → +15% AoE radius.

### 3. Frost Warden Ultimate — "Frost Zone"
- `AbilityDefinitionSO`: arena-wide AoE (radius large enough to cover the full arena, or a dedicated "affects all active enemies" flag if that's a cleaner fit given existing `AbilityExecutionContext` — your call, document it), DoT tick 4 dmg/s, duration 6s, Slow -25% applied for the duration to all enemies in range.
- `TagInteractionRule`: matching the generic `Slow` tag → +10% to the ultimate's slow magnitude.
- Ultimate triggers via the existing Task 04 debug key for now (no charge resource yet, consistent with prior tasks).

### 4. Hero-Exclusive Upgrade Pool — Branch Tagging
- Add an `UpgradeBranch` enum: `Neutral`, `Mage`, `Defender`. Add this field to `UpgradeDefinitionSO` (applies to hero-exclusive upgrades; generic-pool upgrades default to `Neutral`/unused).
- Author the 9 upgrades exactly per the agreed design:
  - **Neutral:** Deep Freeze (max stack 5→4, Freeze duration 1.5s→2.0s)
  - **Mage:** Wider Bolt (AoE 3m→4.5m), Arcane Amplification (basic dmg +40%), Extended Zone (ultimate duration 6s→9s), Ultimate Freeze (ultimate freezes 2s any enemy with ≥3 frost stacks, per tick)
  - **Defender:** Chain Frost (max-stack trigger spreads +2 stacks to enemies within 2m), Brittle Ice (max stack 5→3), Glacial Zone (ultimate slow -25%→-45%), Rapid Bolts (basic cooldown 1.2s→0.8s)
- Register all 9 into Frost Warden's `HeroDefinitionSO.exclusiveUpgrades` (reuse Task 15's Ability Authoring Tool for this if convenient, since it already supports hero-exclusive registration).

### 5. Branch Exclusivity Logic
- Add per-run branch-lock state (e.g. on `UpgradeInventory` or `HeroRuntime`, wherever makes most sense given existing Task 04/05 ownership — document your choice): once the player picks an upgrade tagged `Mage`, lock out `Defender`-tagged upgrades from this hero's exclusive-pool draws for the remainder of the run, and vice versa. `Neutral`-tagged upgrades remain drawable regardless.
- This must affect the **draw pool**, not just disable already-drawn cards — i.e. `LevelUpCardPicker` (Task 07/11's draw logic) must exclude locked-branch upgrades before drawing candidates, so they simply stop appearing rather than appearing-then-being-blocked.
- The lock is permanent for the run once triggered (no un-committing) and resets naturally on a new run (per existing Task 08 full-state-reset — confirm branch-lock state is included in that reset, since it's new state this task introduces).
- This logic should be general (driven by the `UpgradeBranch` field) so it'll work unmodified if a future hero also uses Mage/Defender-style branching — don't hardcode it to Frost Warden specifically.

## Out of Scope (do not implement)
- Wall-protection-style Defender upgrades requiring new `WallRuntime` integration (deferred per earlier discussion)
- Visual indicators for frost stacks (e.g. a stack counter UI above enemies) — can follow in a future visual pass
- Branching systems for other heroes (this task only wires Frost Warden; the exclusivity *mechanism* should generalize, but no other hero needs branch-tagged upgrades yet)
- Ultimate charge-resource mechanics (still debug-key triggered)

## Acceptance Criteria
- [ ] Frost stack/decay mechanic implemented as a generic stacking-effect structure on `EnemyRuntime`, triggering Freeze at max stacks and decaying over time if not refreshed
- [ ] Frost Bolt Burst (basic) and Frost Zone (ultimate) implemented per the specified base values, including their `TagInteractionRule` entries for `AoE`/`Slow`
- [ ] All 9 Frost Warden hero-exclusive upgrades authored with correct effects and registered in `HeroDefinitionSO.exclusiveUpgrades`
- [ ] `UpgradeBranch` field exists on `UpgradeDefinitionSO`; Frost Warden's 9 upgrades are correctly tagged Neutral/Mage/Defender
- [ ] Picking any Mage-tagged upgrade permanently removes all Defender-tagged upgrades from Frost Warden's future draw pool for the rest of the run (and vice versa); Neutral upgrades remain drawable throughout
- [ ] Branch-lock state is included in Task 08's full run-reset flow
- [ ] No SO asset is mutated at runtime
- [ ] No static `Instance` patterns introduced
- [ ] Full playtest: Play as Frost Warden → basic attack stacks frost and freezes at 5 stacks → let stacks decay by not hitting an enemy for several seconds, confirm stack count drops → trigger ultimate, confirm arena-wide DoT+Slow → level up and pick a Mage upgrade → confirm Defender upgrades no longer appear in subsequent card draws → confirm Neutral upgrades still appear → restart run, confirm branch lock is cleared

## Reviewer Notes
Flag as blocking if:
- Frost stacking is implemented as Frost-Warden-specific hardcoded fields on `EnemyRuntime` rather than a reusable generic stacking-effect structure
- Branch exclusivity only disables/hides already-drawn cards in the UI rather than filtering the draw pool itself before cards are presented
- Branch-lock state isn't reset on a new run (leaking from a previous run)
- Tag-interaction rules for `AoE`/`Slow` are hardcoded checks in `AbilityRuntime` specific to Frost Warden rather than using the existing generic `TagInteractionRule` mechanism
