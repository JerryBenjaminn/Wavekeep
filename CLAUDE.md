# CLAUDE.md — Project: Wavekeep (working title)

> Tower/Wave Defence + Hero Roster + Vampire-Survivors-style skill upgrades
> Engine: Unity 6 | Workflow: Two-agent Claude Code (Coder + Reviewer)

This file is the single source of truth for architecture, conventions, and locked design decisions. Read this fully before starting any task. Task-specific instructions live in `/docs/tasks/`.

---

## 1. Game Concept

A wave-defence game where the player defends a position against incoming enemy waves. Killing enemies grants two separate resources:

- **Currency** — spent between waves in a **Shop** on potions/elixirs that grant temporary or permanent run-bonuses.
- **XP** — spent on **upgrading the active hero's abilities**, Vampire-Survivors style (pick/level up skills as you gain levels).

The player selects a **Hero** before a run. Each hero has a unique set of abilities that can be upgraded independently. Levels/waves scale from easy to hard to keep progression interesting across a single run and across difficulty tiers.

### Core Loop (per run)
1. Pick Hero
2. Enter Level (defend point against wave spawner)
3. Kill enemies → gain Currency + XP
4. XP level-up → choose/upgrade an ability (Vampire-Survivors style pick)
5. Between waves → optional Shop visit (spend Currency on potions/elixirs)
6. Survive escalating waves → Level complete / Endless mode push

---

## 2. Locked Design Decisions

These are decided and should NOT be re-litigated without explicit discussion:

- **Not** a Vampire-Survivors-style "move avatar around an open arena." This is tower/wave-defence: player position(s) are fixed or constrained; enemies path toward a defended point.
- **Single spawn direction:** enemies spawn from one side of the arena only (the far side, opposite the player) and advance toward the player. This is NOT a 360°/perimeter spawn — it's open *width-wise* across that one approach direction, not open in all directions. Player/hero sits at the near edge; enemies approach from the far edge.
- **Defended wall:** a wall sits between the approaching enemies and the player/hero. Enemies walk to the wall and attack it (not the hero directly, not a separate abstract "defended point"). The wall has HP. Wall HP reaching 0 is the lose condition for the run.
- Two separate progression currencies: **Currency** (shop, between-wave) and **XP** (in-run ability upgrades). They must remain mechanically distinct — do not let one system silently absorb the other.
- Heroes are **data-defined**, not hardcoded. Adding a new hero must require zero new code in the common case — only new ScriptableObject assets + designer-authored values.
- Abilities are **composable and independently upgradeable** — a hero is a bag of abilities, not a monolithic class with baked-in behavior.
- Difficulty progression is **data-driven** (WaveConfig assets per level/difficulty), not scattered magic numbers in spawner code.
- Architecture must support adding heroes/abilities/enemies/consumables via **ScriptableObject assets only** wherever possible.

---

## 3. Architecture Overview

### 3.1 Data Layer (ScriptableObjects)

| Asset | Purpose |
|---|---|
| `HeroDefinitionSO` | Hero name, icon, base stats, unique `BasicAbilityDefinitionSO`, unique `UltimateAbilityDefinitionSO`, optional `TagInteractionRule` list (see §3.8) |
| `AbilityDefinitionSO` | Ability identity + ordered list of `AbilityUpgradeLevel` (damage/cooldown/range/etc. modifiers per level) |
| `UpgradeDefinitionSO` | Shared upgrade pool entry: tags (`UpgradeTag` list), base effect type/value, used by level-up card selection |
| `ConsumableDefinitionSO` | Shop item: effect type, magnitude, duration, price |
| `EnemyDefinitionSO` | Base stats, movement type, visual/prefab ref, loot (currency/xp yield) |
| `WaveConfigSO` | Per-wave enemy composition, spawn timing/rate, stat multipliers |
| `DifficultyTierSO` | Wraps a sequence of `WaveConfigSO`, global multipliers for a difficulty tier |

**Rule:** Designer-tunable values (numbers, curves, references) belong in SOs. Behavior belongs in code that *reads* SOs. Never bake tunable numbers into MonoBehaviours.

### 3.2 Runtime Layer

- `IAbility` interface: `Execute(context)`, `Upgrade()`, `CurrentLevel`, `Definition` (back-ref to SO). Each equipped ability is a runtime instance wrapping its `AbilityDefinitionSO`.
- `HeroRuntime`: holds a list of active `IAbility` instances + current stats. Does not know ability internals — only orchestrates (tick abilities, expose state to UI).
- `CurrencyManager` / `XPManager`: singleton-ish services (or scene-scoped, see §3.4) that listen to an `EnemyKilled` event and distribute rewards. Single source of truth for current Currency/XP/Level.
- `WaveSpawner`: reads active `WaveConfigSO`/`DifficultyTierSO`, spawns enemies on schedule, exposes wave-complete events.
- `ShopController`: reads available `ConsumableDefinitionSO` list, handles purchase validation against `CurrencyManager`, applies effects.
- `LevelUpController`: listens for XP level-up, presents ability choices (new ability or upgrade existing), applies the chosen result to `HeroRuntime`.

### 3.3 Event-Driven Communication

Prefer C# events / a lightweight event bus over direct cross-references between systems:
- `OnEnemyKilled(EnemyRuntime, KillContext)`
- `OnWaveStarted(int waveIndex)` / `OnWaveCompleted(int waveIndex)`
- `OnXPLevelUp(int newLevel)`
- `OnCurrencyChanged(int newTotal)`
- `OnRunEnded(RunResult)`

This keeps Shop, UI, Hero, and Spawner decoupled — UI just subscribes and renders; it never reaches into game logic directly.

### 3.4 Scene / Lifecycle Notes
- Keep `Update()` loops minimal; abilities/spawners should use coroutines or a centralized tick rather than each running independent `Update()` calls once the ability count grows.

### 3.5 Additional Locked Architecture Decisions

- **Dependency management:** No `static Instance` singletons. Use a `GameSession` root object, assembled at scene start, that holds references to `EventBus`, `CurrencyManager`, `XPManager`, `WaveSpawner`, `HeroRuntime`, etc. Systems receive dependencies via constructor/init injection from `GameSession`, not via static global access. This avoids cross-scene/cross-run state leakage and keeps systems testable in isolation.
- **SO data is read-only at runtime:** ScriptableObjects (`HeroDefinitionSO`, `AbilityDefinitionSO`, etc.) are templates only — never mutated at runtime. All mutable state (current ability level, cooldown timers, active buffs) lives in a parallel Runtime class/wrapper (e.g. `AbilityRuntime` wraps `AbilityDefinitionSO`). Writing live state into an SO field is a reviewer-blocking violation, since it persists into the editor asset.
- **Event bus:** Instance-based `EventBus` class (not static events), owned by `GameSession`, with explicit subscribe/unsubscribe lifecycle tied to run/scene start and end. Static C# events are disallowed for gameplay signals — they risk ghost listeners surviving across scene reloads.
- **Enemy pooling from day one:** Enemies are object-pooled from the start (no `Instantiate`/`Destroy` per enemy in steady-state gameplay). `EnemyPoolManager` pre-warms and recycles `EnemyRuntime` instances. This is a Task 01 architectural concern, not a later optimization pass, since wave counts can scale into the hundreds and retrofitting pooling later means touching every spawn/death code path.
- **Shared ability pool:** `AbilityDefinitionSO` assets are global/shared, not duplicated per hero. A `HeroDefinitionSO` references existing `AbilityDefinitionSO` assets by ID/list, even when multiple heroes share an ability (e.g. "Fireball" available to two heroes at different starting levels). Avoids data duplication and keeps balance changes centralized.

### 3.6 Cross-Platform (Mobile + PC) Considerations

The game targets **both mobile and PC** from the start. This affects architecture decisions made in Task 01 onward:

- **Input abstraction:** No direct `Input.touchCount` or mouse-only calls scattered in gameplay code. Use Unity's Input System package with an abstraction layer (e.g. `IPlacementInput` / `IInteractionInput`) so tower placement, targeting, and UI interaction work identically whether driven by touch or mouse+keyboard.
- **UI scaling:** UI Canvas setup (Canvas Scaler, anchors) must be decided with both aspect ratios in mind (tall mobile portrait/landscape vs. wider PC) from Task 01's project setup, not retrofitted later. Avoid hardcoded pixel positions in early UI work.
- **Performance budget:** Enemy pooling (§3.5) and centralized ticking (§3.4) matter more on mobile — design with the lower-spec target (mobile) as the performance baseline, PC gets headroom for free.
- **Resolution/orientation:** **Landscape only**, both mobile and PC. No portrait support. Arena layout, camera framing, and UI anchors are designed for landscape aspect ratios from Task 01 onward.

### 3.7 3D + Camera

- **The game is 3D**, not 2D/sprite-based. Enemies, arena, and defended point are all 3D models/prefabs with 3D colliders.
- **Camera: fixed 3/4 top-down (isometric-style) angle**, similar to typical tower-defence framing. Camera does not rotate during gameplay (no free orbit) — locked angle, locked or constrained position/zoom per level.
- **Interaction implication:** `IInteractionInput` (§3.6) must resolve screen-space touch/click into a **world-space position via `Physics.Raycast`** (e.g. against a ground plane or placement-layer collider), not a raw 2D screen coordinate. `TryGetInteractionPoint` should return a world position (or expose both screen and resolved world position — decide and document in Task 01).
- **Pooled enemies (§3.5) are 3D prefabs** with `Rigidbody`/`Collider` as appropriate — pooling must account for resetting 3D transform/physics state on reuse (position, rotation, velocity), not just reactivating the GameObject.

### 3.8 Hero Ability Model: Basic/Ultimate + Shared Upgrade Pool + Hero-Exclusive Pool

- **Per-hero unique abilities:** every `HeroDefinitionSO` defines exactly one `BasicAbilityDefinitionSO` and one `UltimateAbilityDefinitionSO` — both unique to that hero, not shared.
- **Two upgrade pools, drawn together:** level-up card choices are drawn from **both** a shared generic pool (identical across all heroes — generic AoE burst, slow field, stat boosts) **and** a hero-exclusive pool (`HeroDefinitionSO` references its own list of `UpgradeDefinitionSO` that only that hero can draw). The card picker (Task 07's `LevelUpCardPicker`) pulls candidates from the union of both pools when drawing cards for the active hero.
- **Hero/upgrade interaction via tags:** `UpgradeDefinitionSO` carries one or more `UpgradeTag`s (e.g. `AoE`, `Slow`, `DoT`, `Elemental_Fire`). A hero's `BasicAbilityDefinitionSO`/`UltimateAbilityDefinitionSO` can optionally define a list of `TagInteractionRule` entries (`matchTag` + effect modifier) describing how that hero's ability responds when the player has picked upgrades carrying that tag.
  - Chosen over explicit per-upgrade ID references because tags scale automatically as new upgrades are added — a hero's "+20% effect on all AoE-tagged upgrades" rule keeps working against future AoE upgrades without per-hero edits.
  - `UpgradeTag` should be its own lightweight type (enum to start; revisit as a tag-SO only if tags need their own data later) so it's trivial to add new tags without touching unrelated code.
  - All effect magnitudes (upgrade base values, interaction modifiers) must be exposed as SO fields, not hardcoded, so values are testable/tunable without code changes.
  - Runtime resolution: `AbilityRuntime` checks the player's currently-held `UpgradeDefinitionSO` selections' tags against its own `TagInteractionRule` list when computing final ability output (damage, effect duration, etc.).
- **Status effects (behavior-changing upgrades):** beyond numeric modifiers, upgrades (generic or hero-exclusive) can apply a `StatusEffectType` (e.g. `Freeze`, `Slow`, `Burn`) to hit enemies — this is a small, fixed enum of effect types implemented once in `EnemyRuntime`/`AbilityRuntime`, not an open-ended scripting system. `UpgradeDefinitionSO`/`TagInteractionRule` can specify a `StatusEffectType` + magnitude/duration to apply on hit, alongside or instead of a numeric stat modifier. Example: "Frost Warden's ultimate applies Freeze" is a hero-exclusive upgrade (or a baseline behavior on that hero's ultimate `AbilityDefinitionSO`, with an upgrade only modifying freeze duration/strength — exact split decided per-case in design).
  - `EnemyRuntime` needs a small status-effect state machine (which effects are active, their remaining duration, and how they modify movement speed/damage taken/etc. while active) — kept generic across effect types rather than one-off booleans per effect.

---

## 4. Coding Conventions

- C# standard naming: `PascalCase` for types/methods, `camelCase` for locals/fields (private fields `_camelCase`).
- One class per file, file name matches class name.
- ScriptableObject assets live under `Assets/Data/[Heroes|Abilities|Enemies|Waves|Consumables]/`.
- No magic numbers in MonoBehaviours — pull from SO references.
- Favor composition over inheritance for abilities and enemy behaviors.
- Every new system gets a short XML doc comment block explaining responsibility (`/// <summary>`).

---

## 5. Two-Agent Workflow

- **Coder agent**: implements the current task from `/docs/tasks/NN-taskname.md` exactly as scoped. Does not expand scope. Flags ambiguity instead of guessing on locked decisions.
- **Reviewer agent**: checks implementation against this file's architecture rules + the task file's acceptance criteria. Flags violations of §2 (Locked Design Decisions) and §3 (Architecture) as blocking issues.
- Tasks are completed sequentially. Do not start Task N+1 until Task N is reviewed and accepted.

---

## 6. Out of Scope (for now)

- Multiplayer/co-op
- Monetization/IAP integration
- Full art/VFX polish — placeholder art is fine until loop is validated
- Wall repair/upgrade mechanics (wall currently has fixed HP per level; repair/upgrade-via-currency can be considered post-MVP)

> **Note (superseded decision):** "No meta-progression beyond single-run" was the original MVP scope. This is now overridden — a persistent Gear/Artifact loot system is planned: battle → earn loot → equip → start a new, stronger run → reach further.
>
> **Locked sub-decisions:**
> - **Equip slots:** `Helmet`, `Body`, `Hands`, `Legs`, `Feet` (gear) + one `Artifact` slot — six slots total per hero.
> - **Rarity tiers:** `Common`, `Uncommon`, `Rare`, `Epic`, `Legendary`, `Unique`, ascending power. Regular enemies drop only the lower tiers (exact cutoff TBD when loot tables are designed); higher tiers (Legendary/Unique) are boss-exclusive for now.
> - **Persistence:** gear/artifact ownership and hero equip-loadouts persist between runs (saved to disk). In-run currency/XP/upgrade-inventory/consumable-inventory remain per-run only.
> - **Management UI:** a separate hub/main-menu scene handles equip management (and likely becomes the home for other cross-run management as the project grows), rather than folding equip UI into the existing hero-select screen.
> - **Future tooling (not yet scheduled):** custom Editor tools for authoring new enemies (stats, loot tables, abilities) and new abilities (targeting type, damage, DoT/stun/slow effects) without hand-writing SO assets one field at a time. Planned once the underlying data shapes (loot tables, status effects) are stable enough to build a tool around.
> Design and implementation are split across multiple tasks (gear/equip core, loot tables, hub scene) — see `/docs/tasks/`.

---

## 7. Glossary

- **Run** — one full playthrough from hero select to defeat/victory.
- **Wave** — one discrete enemy spawn batch within a level.
- **Level** — a defined arena/map with its own `DifficultyTierSO` sequence.
- **Endless mode** — post-final-wave mode with continuously scaling `WaveConfigSO` (post-MVP, design TBD).

---

## 8. Task Index

See `/docs/tasks/` for sequential task files:
- `01-core-architecture.md` — SO schemas, event bus, project structure
- `02-wave-spawner.md` — spawner + WaveConfig/DifficultyTier system
- `03-currency-xp-systems.md` — CurrencyManager, XPManager, EnemyKilled pipeline
- `04-ability-system.md` — IAbility, AbilityDefinitionSO, upgrade levels
- `05-hero-system.md` — HeroDefinitionSO, HeroRuntime, hero select screen
- `06-shop-system.md` — ConsumableDefinitionSO, ShopController, purchase flow
- `07-levelup-flow.md` — XP level-up UI, ability pick/upgrade choice screen
- *(more added as design solidifies)*
