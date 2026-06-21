# Task 01 — Core Architecture & Project Setup

> Read `CLAUDE.md` in full before starting. This task establishes the foundational structure every later task builds on. Do not skip ahead into ability/hero/wave logic — that's Tasks 02–05.

## Goal

Set up the project skeleton: folder structure, core SO schemas (empty/minimal), `GameSession`, `EventBus`, Input System integration with `IInteractionInput`, and the `EnemyPoolManager` scaffold. No actual gameplay yet — this task is plumbing.

## Scope

### 1. Project Structure
Create the following folder structure under `Assets/`:
```
Assets/
  Data/
    Heroes/
    Abilities/
    Enemies/
    Waves/
    DifficultyTiers/
    Consumables/
  Scripts/
    Core/          (GameSession, EventBus, service interfaces)
    Data/          (SO class definitions — not assets)
    Runtime/       (Runtime wrapper classes — AbilityRuntime, EnemyRuntime, HeroRuntime)
    Input/         (IInteractionInput + implementations)
    Pooling/       (EnemyPoolManager)
    UI/            (empty for now)
  Scenes/
  Prefabs/
    Enemies/
```

### 2. Core Systems

**`GameSession` (Scripts/Core)**
- Plain C# class (not a MonoBehaviour singleton) instantiated by a thin `GameSessionBootstrap : MonoBehaviour` in the scene.
- Holds references to: `EventBus`, `EnemyPoolManager`, and placeholder slots for `CurrencyManager`, `XPManager`, `WaveSpawner`, `HeroRuntime` (to be filled in later tasks — leave as `null`/TODO comments referencing the task that will populate them).
- No static `Instance` access. `GameSessionBootstrap` exposes the session via a serialized field or a single explicit reference passed to dependent MonoBehaviours in `Awake`/`Start`.

**`EventBus` (Scripts/Core)**
- Instance-based class, not static.
- Generic `Subscribe<T>(Action<T> handler)` / `Unsubscribe<T>(Action<T> handler)` / `Publish<T>(T evt)` pattern using a `Dictionary<Type, Delegate>` internally, OR explicit typed events if you prefer simplicity for now — pick one and document the choice in code comments. Either is acceptable for Task 01; consistency matters more than the specific pattern.
- Define empty marker structs/classes for the events listed in CLAUDE.md §3.3 (`EnemyKilledEvent`, `WaveStartedEvent`, `WaveCompletedEvent`, `XPLevelUpEvent`, `CurrencyChangedEvent`, `RunEndedEvent`) — fields can be minimal/placeholder, later tasks will flesh them out.
- `EventBus` instance is owned by `GameSession` and cleared (`UnsubscribeAll` or equivalent) on session teardown.

**`IInteractionInput` (Scripts/Input)**
- Interface defining at minimum: `bool TryGetInteractionPoint(out Vector3 worldPosition)` (resolved via `Physics.Raycast` against a ground/placement layer — this is a **3D game with a fixed 3/4 top-down camera**, not 2D, so screen taps/clicks must be raycast into world space) and `bool InteractionTriggeredThisFrame`. Optionally also expose the raw screen position if useful for UI hit-testing separately.
- Two implementations: `TouchInteractionInput` and `MouseInteractionInput`, both using the new Input System package (not legacy `Input` class). Both perform the same raycast-against-ground-plane resolution — they differ only in how they get the screen position (touch vs. mouse), not in the 3D resolution logic.
- A simple `InteractionInputProvider` (MonoBehaviour or plain class) that picks the correct implementation based on platform (`Application.isMobilePlatform` or build target define) and exposes it through `GameSession`.

**`EnemyPoolManager` (Scripts/Pooling)**
- Scaffold only — generic object pool capable of pre-warming and recycling GameObjects.
- Public API: `Prewarm(GameObject prefab, int count)`, `Get(GameObject prefab) : GameObject`, `Release(GameObject instance)`.
- This is a **3D game** — pooled prefabs use 3D `Collider`/`Rigidbody` as appropriate. `Get`/`Release` must reset 3D transform state (position, rotation, scale) and physics state (velocity, angular velocity) on reuse, not just toggle `SetActive`.
- No enemy-specific logic yet (that's Task 02). Just prove the pooling mechanism works with a placeholder 3D primitive (cube/capsule with a `Collider`).

### 3. SO Schema Stubs (Scripts/Data)

Create the **class definitions** (with `[CreateAssetMenu]`) for all SOs listed in CLAUDE.md §3.1, with fields matching the table — but only the fields needed structurally right now (name, icon, base stats placeholders). Do not implement upgrade-level logic or wave-composition logic yet (Tasks 02–05 will extend these). The goal is that these asset types exist and can be created as empty assets in the Project window.

Required stub classes:
- `HeroDefinitionSO`
- `AbilityDefinitionSO`
- `ConsumableDefinitionSO`
- `EnemyDefinitionSO`
- `WaveConfigSO`
- `DifficultyTierSO`

### 4. Camera / Orientation Setup
- Configure project for **landscape-only** (Player Settings → both mobile orientation lock and a fixed-aspect consideration for PC window).
- Set up a basic Canvas with Canvas Scaler configured for landscape aspect ratios (Scale With Screen Size, reference resolution sensible for landscape, e.g. 1920x1080).
- Set up the **main camera at a fixed 3/4 top-down (isometric-style) angle** over a placeholder ground plane. No camera rotation/orbit controls — locked angle. Position/zoom can be a sensible placeholder; exact framing gets tuned once a real arena exists (Task 02+).
- Add a placeholder ground plane with a collider on a dedicated "Placement" or "Ground" layer, used by `IInteractionInput`'s raycast resolution.

## Out of Scope (do not implement)
- Actual hero/ability/enemy/wave gameplay logic
- Shop UI or purchase flow
- Currency/XP accumulation logic
- Any visual polish beyond placeholder shapes

## Acceptance Criteria
- [ ] Folder structure matches §1 exactly
- [ ] `GameSession` exists, is not a static singleton, instantiated via `GameSessionBootstrap`
- [ ] `EventBus` is instance-based, owned by `GameSession`, with all six placeholder event types defined
- [ ] `IInteractionInput` + both implementations exist and compile; switching is provable (e.g. via a build-target check or editor toggle for testing)
- [ ] `EnemyPoolManager` can prewarm and recycle a placeholder prefab without runtime errors, verified with a simple test scene or temporary debug script
- [ ] All six SO stub classes exist with `[CreateAssetMenu]` and can be instantiated as assets in `Assets/Data/`
- [ ] No SO is mutated at runtime anywhere in this task's code (there's no runtime logic yet, but keep this in mind for code review going forward)
- [ ] No static `Instance` patterns anywhere in the codebase
- [ ] Project is set to landscape orientation; Canvas Scaler configured accordingly
- [ ] Project builds without errors on at least one platform (Editor play mode is sufficient for this task)

## Reviewer Notes
Flag as blocking if:
- Any static singleton (`public static X Instance`) appears anywhere
- Any SO field is written to at runtime (should be none yet, but check)
- Input handling bypasses `IInteractionInput` and calls `Input.*` or touch APIs directly in gameplay-adjacent code
- Folder structure deviates from §1 without a documented reason
