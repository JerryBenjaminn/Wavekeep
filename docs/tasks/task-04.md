# Task 04 — Ability System Core

> Read `CLAUDE.md` in full (especially §3.8 — Hero Ability Model), and review the Task 01–03 implementations before starting. This task builds the actual ability execution system: `IAbility`, `AbilityDefinitionSO`, upgrade levels, the shared `UpgradeDefinitionSO` pool, and tag-based interaction resolution. This is the first task where enemies can actually be killed by something other than the debug key — abilities will call `EnemyRuntime.TakeDamage` for real.

This task does NOT build the hero selection screen (Task 05) or the level-up card-picker UI (Task 07). To test and verify ability execution, this task uses one placeholder stand-in hero (a capsule, per your existing convention) with a hard-coded basic ability and ultimate ability wired in code/inspector — this stand-in is throwaway and will be replaced by the real `HeroDefinitionSO`-driven flow in Task 05.

## Goal

By the end of this task, the placeholder hero capsule auto-attacks enemies approaching the wall using a `BasicAbilityDefinitionSO`-driven ability, has an `UltimateAbilityDefinitionSO`-driven ability triggerable via a debug key, and the shared `UpgradeDefinitionSO` pool with tags exists and can modify ability output through `TagInteractionRule` resolution — verified by manually granting upgrades via a debug trigger (since the real card-picker UI is Task 07).

## Scope

### 1. `IAbility` Interface (Scripts/Abilities)
- `void Tick(float deltaTime)` — handles cooldown countdown and auto-execution if applicable
- `void Execute(AbilityExecutionContext context)` — performs the actual effect (damage, AoE, etc.)
- `bool IsReady` — true when cooldown has elapsed
- `int CurrentLevel` — current upgrade level of this ability instance
- `AbilityDefinitionSO Definition` — back-reference to the SO template (read-only access)

`AbilityExecutionContext` is a small struct/class carrying whatever an ability needs at execution time (e.g. caster position, target enemy or target point, `EnemyPoolManager`/active enemy list reference for AoE queries). Keep it minimal for now — extend as needed in later tasks.

### 2. `AbilityDefinitionSO` (flesh out from Task 01 stub)
Fields:
- `abilityName`, `icon`
- `baseDamage`, `baseCooldown`, `range` (or AoE radius, depending on ability shape)
- ordered list of `AbilityUpgradeLevel` entries: `{ damageModifier, cooldownModifier, rangeModifier }` or similar — values are multipliers/additions applied on top of base values per level
- `targetingType` enum (e.g. `SingleTarget`, `AreaOfEffect`) — keep simple, just enough to drive execution logic differences

### 3. `AbilityRuntime` (Scripts/Runtime)
- Wraps an `AbilityDefinitionSO` + current level + cooldown timer state. This is the mutable runtime instance — **never writes back to the SO** (consistent with CLAUDE.md §3.5).
- Implements `IAbility`.
- On `Execute`, applies `UpgradeLevel`-driven stat modifiers (level-based) AND any active `TagInteractionRule` modifiers (see §5) to compute final damage/effect, then calls `EnemyRuntime.TakeDamage(finalDamage)` on the resolved target(s).
- For `SingleTarget`: find nearest enemy within range (simple distance check against active enemies is fine — no spatial partitioning needed yet at this enemy-count scale).
- For `AreaOfEffect`: apply to all enemies within radius of caster or a target point.

### 4. `BasicAbilityDefinitionSO` / `UltimateAbilityDefinitionSO`
- These can be the same underlying `AbilityDefinitionSO` class (per CLAUDE.md §3.8, they're just two instances assigned to a hero, not different types) — confirm this interpretation matches the original intent, or introduce a thin marker/subclass only if you find a concrete reason `AbilityDefinitionSO` alone isn't sufficient. Document whichever you choose.
- Basic ability: auto-executes whenever `IsReady` and a valid target exists (no player input required — this is an idle/auto-battler per the game's genre).
- Ultimate ability: same execution model, but for this task, trigger it manually via a debug key (e.g. `U`) instead of building real "ultimate charge" mechanics — that nuance can be revisited once currency/mastery-style resources (if any) are designed later. Document this as a placeholder simplification.

### 5. Shared Upgrade Pool & Tag Interactions
- `UpgradeDefinitionSO` (flesh out from Task 01 stub): `upgradeName`, `tags` (list of `UpgradeTag`), `effectType` (e.g. flat damage bonus, cooldown reduction, AoE radius bonus — keep the enum small for MVP), `effectValue`.
- `UpgradeTag` enum: start with a handful covering what you'd expect heroes to react to (e.g. `AoE`, `SingleTarget`, `DoT`, `Slow`, `Elemental_Fire`, `Elemental_Dark`) — fine to expand later, this isn't a locked list.
- `TagInteractionRule` (referenced from `AbilityDefinitionSO` or a wrapper around it per hero): `{ matchTag, effectModifierType, effectModifierValue }`.
- A simple `UpgradeInventory` (per-run, owned by `GameSession` or the placeholder hero) holds the list of `UpgradeDefinitionSO` the player currently has. For this task, add a debug trigger (e.g. number keys 1–3) that grants a specific test `UpgradeDefinitionSO` to the inventory, since the real XP-level-up card picker doesn't exist until Task 07.
- `AbilityRuntime.Execute` checks `UpgradeInventory`'s held upgrades' tags against its own `Definition`'s `TagInteractionRule` list (if any) and applies matching modifiers on top of the level-based stat calculation.

### 6. Placeholder Hero Stand-In
- One capsule GameObject at the player's position (reuse/extend whatever Task 01/02 already placed there), with a `BasicAbilityDefinitionSO` and `UltimateAbilityDefinitionSO` assigned via inspector for this task's testing purposes.
- A simple `HeroAbilityController` (throwaway-ish, but keep it clean since Task 05 will likely absorb/replace it) ticks both abilities each frame and triggers ultimate on the debug key.

## Out of Scope (do not implement)
- `HeroDefinitionSO`-driven hero selection or multiple heroes (Task 05)
- Real level-up card-picker UI consuming `XPLevelUpEvent` (Task 07) — use debug keys to grant upgrades for now
- Shop/consumables (Task 06)
- Visual VFX for abilities beyond placeholder shapes/debug gizmos if helpful
- Ultimate "charge" resource mechanics — manual debug trigger is sufficient for this task

## Acceptance Criteria
- [ ] `IAbility`, `AbilityDefinitionSO`, `AbilityRuntime` implemented per the above
- [ ] Placeholder hero's basic ability auto-fires at enemies within range/cooldown, dealing real damage via `EnemyRuntime.TakeDamage`
- [ ] Enemies killed by abilities correctly flow through the existing Task 02/03 pipeline (pool release, `EnemyKilledEvent`, currency/XP reward) with no changes needed to that pipeline
- [ ] Ultimate ability triggers via debug key and executes correctly (single-target or AoE per its definition)
- [ ] `UpgradeDefinitionSO` + `UpgradeTag` + `TagInteractionRule` implemented; granting a test upgrade via debug key measurably changes ability output (e.g. visible damage increase) when its tag matches a `TagInteractionRule` on the active ability
- [ ] No SO asset (`AbilityDefinitionSO`, `UpgradeDefinitionSO`, etc.) is mutated at runtime — all level/upgrade state lives in `AbilityRuntime`/`UpgradeInventory`
- [ ] No static `Instance` patterns introduced
- [ ] Test scene demonstrates: Play → waves spawn → basic ability kills enemies before/as they reach the wall → currency/XP visibly climb without pressing the old debug-kill key → granting a test upgrade changes visible ability behavior

## Reviewer Notes
Flag as blocking if:
- Ability damage is applied by writing into `AbilityDefinitionSO` fields instead of computing into a local/runtime value
- Basic ability requires player input to fire (it must be an idle auto-attack per the game's core loop)
- Tag interaction resolution is hardcoded per-ability in a switch statement rather than data-driven via `TagInteractionRule` entries
- The existing Task 02/03 kill/reward pipeline had to be modified rather than simply being called into by the new ability system
