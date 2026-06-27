\# Task 054 — Skeleton Enemy: Model + Animation Integration



> Status: Ready for implementation

> Depends on: Task 01 (core architecture, EventBus, GameSession), Task 02 (WaveSpawner), §3.5 (Enemy pooling), §3.7 (3D + Camera), §2 (Locked Design Decisions — single spawn direction, enemies walk to wall and attack it)



\---



\## Instructions to Claude Code (read first)



1\. \*\*Read `CLAUDE.md` in full before starting\*\*, even if you've read it in a prior session — confirm you're still aligned with §2 (Locked Design Decisions) and §3 (Architecture), since this task touches enemy behavior, pooling, and runtime structure directly.

2\. \*\*Read this entire task file before writing any code.\*\* Do not start implementing from the top before you've seen the Out of Scope and Acceptance Criteria sections below.

3\. \*\*Do not expand scope.\*\* Implement exactly what's specified. If something seems like it should obviously also be done (e.g. "this would be a good place to also add X"), do not add it — flag it instead as a suggestion at the end of your response.

4\. \*\*Flag, don't guess, on any of the following:\*\*

&#x20;  - Any ambiguity in how this task's requirements interact with an existing system you find while reading the code (e.g. if existing enemy movement code already has a hook this task assumes doesn't exist, or vice versa).

&#x20;  - Any conflict between this task's instructions and a locked decision in CLAUDE.md §2/§3.

&#x20;  - Any pre-existing asset/state described in "Context / Pre-confirmed State" below that you find does NOT match what's described (e.g. if `Skeleton\_Animator` doesn't actually have the states/Motion fields listed) — stop and report rather than silently fixing or recreating it.

&#x20;  - Anything requiring a Unity Editor action that can't be scripted (manual visual judgment calls, asset assignment requiring human eyes) — produce the Editor script/instructions for the developer to run, don't attempt to fake or skip it.

5\. \*\*Do not touch ScriptableObject template assets at runtime\*\* — all mutable state belongs in runtime classes per §3.5.

6\. \*\*At the end of your response\*\*, give a short summary of: what was implemented, anything flagged per point 4, and what (if anything) still requires manual action in the Unity Editor from the developer.



\---



\## 0. Context / Pre-confirmed State (do not re-verify, treat as given)



\- `SM\_Gen\_Chr\_Skeleton\_01` (Synty PolygonGeneric) is rigged \*\*Humanoid\*\*, using the shared `Generic\_CharactersAvatar` Avatar asset common across PolygonGeneric characters.

\- `EnemyDefinitionSO` named `Skeleton` exists at `Assets/Data/Enemies/Skeleton`, Custom Prefab field points at the Skeleton prefab. Confirmed working end-to-end through `EnemyPoolManager`/`WaveSpawner` (previously rendered in T-pose pre-Animator, which was expected).

\- Sword Combat / Sidekick animation clips have been verified as Humanoid-rig-compatible and visually correct on the Skeleton model (no limb-clipping/proportion artifacts) by manual preview.

\- An `Animator Controller` asset named \*\*`Skeleton\_Animator`\*\* has already been created manually and already contains four states with Motion fields populated:

&#x20; - `Run` → `Skeleton\_Run` (custom FBX, Mixamo-sourced, renamed clip)

&#x20; - `Attack` → `A\_MOD\_SWD\_Attack\_HeavyCombo01A\_Neut` (Synty Sidekick)

&#x20; - `AttackRecovery` → `A\_MOD\_SWD\_Attack\_HeavyCombo01A\_ReturnToIdle\_Neut` (Synty Sidekick)

&#x20; - `Death` → `A\_MOD\_SWD\_Death\_B\_Neut` (Synty Sidekick)

\- \*\*No Idle state exists or is needed\*\* — per §2 locked decisions, this enemy is always moving toward the wall from spawn until it either reaches the wall (→ Attack loop) or dies en route. Do not add an Idle state or a `Speed` parameter.

\- This Animator Controller already exists in the project — \*\*do not recreate it from scratch or regenerate it via editor script.\*\* Open and extend the existing asset in place.



\---



\## 1. Goal



Complete the `Skeleton\_Animator` Controller (parameters, transitions, Animation Event) via an Editor script (per CLAUDE.md's note that Claude Code cannot drive the Unity Editor directly — scene/asset changes happen via `MenuItem` editor scripts that the developer runs locally), wire it into the Skeleton enemy's runtime behavior, and ensure full compatibility with pooling.



\## 2. Scope



\### 2.1 Animator Controller — Parameters



Add to `Skeleton\_Animator` (via `UnityEditor.Animations.AnimatorController` API in an Editor script):

\- `Attack` (Trigger)

\- `Die` (Trigger)



\### 2.2 Animator Controller — Transitions



\- \*\*Default state on entry: `Run`.\*\* No incoming transition needed; this is simply the Controller's default state.

\- \*\*Any State → Attack\*\*: condition `Attack` (Trigger). `Has Exit Time = false`. Can interrupt `Run`.

\- \*\*Attack → AttackRecovery\*\*: `Has Exit Time = true`, Exit Time ≈ 1.0, no parameter condition.

\- \*\*AttackRecovery → Attack\*\*: `Has Exit Time = true`, Exit Time ≈ 1.0, no parameter condition. (Loop — the Skeleton keeps attacking the wall repeatedly until it dies or the wall is destroyed; it does not return to `Run`.)

\- \*\*Any State → Death\*\*: condition `Die` (Trigger). This transition must be able to interrupt any other state, including mid-`Attack`/`AttackRecovery`. Give it appropriate priority/ordering in the state machine so death always wins over an in-progress attack loop.



\### 2.3 Animation Event — Attack Impact



\- On the `Attack` clip (`A\_MOD\_SWD\_Attack\_HeavyCombo01A\_Neut`), add an Animation Event at the frame where the weapon visually connects, calling a method `OnAttackImpactFrame()`.

\- `OnAttackImpactFrame()` must exist on the Skeleton's enemy MonoBehaviour (add it if it doesn't exist) and is the single point where wall-damage application happens for this attack — i.e. damage-to-wall must be triggered from this animation-driven callback, not from the moment the `Attack` trigger is fired. This decouples "attack started" from "weapon visually lands," consistent with how hit-timing should work across the project going forward.



\### 2.4 Movement Stop on Attack



\- Check the existing enemy movement code (whatever currently drives the Skeleton toward the wall). When the enemy reaches attack range of the wall and the `Attack` trigger fires, movement must be explicitly halted at that same moment — do not rely on the animation alone to look stationary. If movement is already gated by an "in range of wall" state check, confirm it also disables further position updates while `Attack`/`AttackRecovery` are active, not just suppresses new pathing.

\- If no such hook currently exists, add the minimal stop-movement call at the point where the Attack trigger is invoked, and resume is not needed since this enemy never moves again after reaching the wall (per §2.2, it loops Attack/AttackRecovery until death).



\### 2.5 VFX Anchor Point Migration



\- Migrate any capsule-anchored impact/hit-reaction or death VFX spawn points to appropriate bone transforms on the Skeleton model (e.g. chest/head bone for hit impacts taken, root/hips for death effect origin).

\- Add explicit `\[SerializeField] Transform` bone-reference fields on the enemy's MonoBehaviour for editor-assignment, rather than runtime bone-name lookups.



\### 2.6 Pooling Compatibility (§3.5 — Hard Requirement)



\- On reuse from the pool, force-reset the Animator to `Run` before the object becomes visible again (e.g. `Animator.Play("Run", 0, 0f)` or equivalent), so a recycled Skeleton never flashes a leftover Death pose or mid-attack frame.

\- Extend whatever existing pool-reset method already handles transform/physics reset (§3.5) — add the Animator reset there, don't create a second parallel reset path.



\### 2.7 Death Timing



\- The `Death` clip's length must complete before the object returns to the pool — via a timer/coroutine matching clip length, or an Animation Event firing pool-return at the correct end frame. Do not pool-return immediately on the `Die` trigger.



\---



\## 3. Out of Scope



\- Ragdoll physics on death.

\- Any Idle state or Speed-based locomotion blending — explicitly not needed for this enemy (see §0).

\- Applying this pipeline to other enemy types or heroes — this is the Skeleton reference implementation only.

\- Changes to `EnemyDefinitionSO`/`WaveConfigSO` schema.



\---



\## 4. Acceptance Criteria



\- \[ ] `Skeleton\_Animator` has `Attack` and `Die` Trigger parameters added.

\- \[ ] Transitions match §2.2 exactly, including Any State → Death being able to interrupt the Attack/AttackRecovery loop.

\- \[ ] Animation Event on `Attack` clip correctly calls `OnAttackImpactFrame()` at the visual impact frame, and wall damage is applied from that callback, not from Attack-trigger time.

\- \[ ] Skeleton movement halts the instant it reaches the wall and the Attack state begins — no visible sliding/drift during Attack/AttackRecovery.

\- \[ ] Pooled re-spawn never shows a leftover Death pose or mid-attack frame on reactivation.

\- \[ ] VFX spawn points are bone-anchored on the real model, not at old capsule-derived offsets.

\- \[ ] Death animation completes visually before pool-return.

\- \[ ] No regressions to existing enemy movement/wave-spawner integration — only animation-driving and movement-stop hooks added.



\---



\## 5. Implementation Note



Since Unity Editor cannot be driven directly: implement the Controller parameter/transition/event setup as a one-time `MenuItem` Editor script (e.g. `Tools/Wavekeep/Setup Skeleton Animator`) that the developer runs once from the Unity Editor menu, following the project's existing convention for editor-driven asset changes. Confirm the script is idempotent (safe to re-run without duplicating parameters/transitions) in case it needs to be re-run after adjustments.

