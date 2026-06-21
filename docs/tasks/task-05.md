# Task 05 — Hero System & Hero Select

> Read `CLAUDE.md` in full (especially §3.8 — Hero Ability Model), and review the Task 01–04 implementations before starting. This task replaces the Task 04 throwaway `HeroAbilityController`/placeholder capsule with a real `HeroDefinitionSO`-driven system, and adds a basic hero-select screen so the player picks a hero before a run starts.

## Goal

By the end of this task, there are at least two distinct `HeroDefinitionSO` assets (still capsule placeholders visually, per your earlier note — just tinted/labeled differently so they're distinguishable), a hero-select screen that lets the player pick one before the run begins, and a `HeroRuntime` that drives the chosen hero's basic/ultimate abilities using the Task 04 `AbilityRuntime`/`IAbility` system — with zero new code required to add a third hero later.

## Scope

### 1. `HeroDefinitionSO` (flesh out from Task 01 stub, per CLAUDE.md §3.1/§3.8)
Fields:
- `heroName`, `icon`, `prefab` (the capsule prefab — can be the same mesh for all heroes for now, differentiated by material color or a label, since real art comes later)
- `baseStats` (whatever's relevant now — e.g. a base damage/cooldown multiplier applied on top of ability base values, or leave at 1.0 if heroes only differ by ability choice for now — your call, document it)
- `basicAbility` : `AbilityDefinitionSO` reference (unique per hero)
- `ultimateAbility` : `AbilityDefinitionSO` reference (unique per hero)
- optional `tagInteractionRules` — per CLAUDE.md §3.8, confirm whether these live on the `AbilityDefinitionSO` itself (as Task 04 implemented) or need to additionally be referenceable from `HeroDefinitionSO`; if Task 04 already attached rules directly to each hero's ability assets, no structural change is needed here — just confirm and document.

### 2. Author Two Hero Assets
- Create two `HeroDefinitionSO` assets, each with its own `basicAbility`/`ultimateAbility` `AbilityDefinitionSO` instances (reuse Task 04's ability definitions as a starting point for one hero; author a second distinct combination for the other — e.g. different targeting type, damage/cooldown balance, or tag-interaction rule, so the two heroes are meaningfully different to test with).
- Differentiate the two heroes visually at a placeholder level (e.g. material color tint on the capsule) purely so it's obvious which hero is active during testing.

### 3. `HeroRuntime` (Scripts/Runtime)
- Replaces Task 04's `HeroAbilityController` as the real driver. Holds references to the selected `HeroDefinitionSO`, an `AbilityRuntime` for the basic ability, and an `AbilityRuntime` for the ultimate ability.
- Ticks both abilities each frame (basic auto-fires per Task 04's model; ultimate still triggers via the existing debug key for now — real ultimate-charge mechanics remain out of scope per Task 04's note).
- Owns or references the `UpgradeInventory` (decide whether this stays on `GameSession` as Task 04 built it, or moves to be per-`HeroRuntime` — document your reasoning; either is defensible, but pick one and be consistent).

### 4. Hero Select Screen
- A simple UI screen (Canvas-based, can reuse/extend the existing Task 03 Canvas) shown before the run starts: displays the available heroes (icon/name is enough — no fancy card art) and lets the player pick one via a button/tap per hero.
- On selection, instantiate/configure the chosen hero's capsule at the existing hero position (replacing Task 04's hardcoded single hero), construct its `HeroRuntime`, and start the run (trigger `WaveSpawner` to begin, if it isn't already auto-starting — check Task 02's current behavior and wire accordingly).
- This screen can be the very first thing shown on scene load, before any wave spawns — no need for a main menu beyond this for now.

### 5. Zero-Code Hero Addition Check
- As a self-check (not a deliverable, but verify before reporting done): could a third hero be added by only creating new SO assets (a new `HeroDefinitionSO` + new `AbilityDefinitionSO` instances) and adding it to the hero-select list, with no code changes? If not, identify and fix the friction point before finishing this task.

## Out of Scope (do not implement)
- More than two hero assets (two is enough to prove the pattern)
- Hero-select screen visual polish (placeholder buttons/text are fine)
- Real ultimate-charge resource mechanics (still debug-key triggered)
- Shop, level-up card-picker UI (Tasks 06/07)
- Persisting hero selection between runs/sessions

## Acceptance Criteria
- [ ] `HeroDefinitionSO` fully fleshed out per the fields above
- [ ] Two distinct, differentiable `HeroDefinitionSO` assets exist with their own basic/ultimate `AbilityDefinitionSO` instances
- [ ] `HeroRuntime` correctly drives the selected hero's abilities via the existing Task 04 `AbilityRuntime`/`IAbility` system, with no changes needed to `AbilityRuntime`/`IAbility` themselves
- [ ] Hero-select screen lets the player choose between the two heroes before the run starts, and the correct hero (visually + mechanically distinct) is active afterward
- [ ] Selecting either hero correctly starts the wave spawner / run flow already built in Task 02
- [ ] A third hero could be added via SO assets alone — no code changes required (verified per §5 above; document how you verified this)
- [ ] No SO asset is mutated at runtime
- [ ] No static `Instance` patterns introduced

## Reviewer Notes
Flag as blocking if:
- Hero differentiation requires any code branching (e.g. `if (heroName == "X")`) instead of being driven purely by `HeroDefinitionSO` field values
- The hero-select screen hardcodes the two heroes in code rather than reading from a list of `HeroDefinitionSO` assets (even if that list itself is a serialized array for now — the point is no hero-specific logic in code)
- `AbilityRuntime`/`IAbility` needed modification to support hero-driven abilities (Task 04's system should already be hero-agnostic)
