\# Task 056 — Frost Warden: Basic Attack + Idle Animator Integration



> Status: Ready for implementation

> Depends on: Task 054 (Skeleton enemy model + animation integration — reference pattern for Animation Event-driven hit timing), Task 036 (dual-hero runtime, auto-attack to nearest target), Task 045 (Frost Warden Basic/Ultimate VFX — projectile + ice-burst), §3.8 (Hero Ability Model)



\---



\## Instructions to Claude Code (read first)



1\. Read CLAUDE.md in full before starting, even if read in a prior session.

2\. Read this entire task file before writing any code.

3\. Do not expand scope. Implement exactly what's specified; flag suggestions separately instead of acting on them.

4\. Flag, don't guess, on:

&#x20;  - Any mismatch between this task's "Pre-confirmed State" section and what you actually find in the project.

&#x20;  - Any conflict with locked decisions in CLAUDE.md §2/§3.

&#x20;  - Anything requiring manual Editor judgment (exact Animation Event frame timing) — implement with the event in a clearly reasonable default position and note in your summary that the developer may want to fine-tune the exact frame.

5\. Do not touch ScriptableObject template assets at runtime.

6\. At the end of your response, summarize what was implemented, anything flagged, and what (if anything) needs manual Editor action/tuning.



\---



\## 0. Context / Pre-confirmed State (do not re-verify, treat as given)



\- Frost Warden uses the project's existing data-driven hero architecture (§3.1): `HeroDefinitionSO` has a "Visual" sub-section with a \*\*Prefab\*\* field that determines the hero's spawned model/visual. This field previously pointed at the shared `PlaceholderHero` capsule; the developer has already swapped it to point at `SM\_Chr\_Male\_Rogue\_01` (Synty model), confirmed working — Frost Warden now spawns correctly in-game with the real model via this Visual.Prefab reference. There is no separate dedicated "FrostWarden\_Hero" prefab to create — the model swap is fully handled through this existing SO field.

\- The Synty model referenced has a Humanoid Animator component (consistent with the shared Humanoid rig convention already established for enemies in Task 054/055).

\- An Animator Controller named \*\*`FrostWarden\_AnimatorController`\*\* has already been created manually by the developer (same workflow as `Skeleton\_Animator` in Task 054) and contains two states with Motion fields populated:

&#x20; - `Idle` → `A\_Idle\_Base\_Sheated\_Sword` (Synty AnimationSwordCombat pack)

&#x20; - `Attack` → a single unified clip from the Synty AnimationSwordCombat `Block` folder (developer-selected; read the exact clip name from the actual asset if you need to reference it directly)

\- This Controller already exists in the project — \*\*do not recreate it from scratch.\*\* Open and extend the existing asset in place, following the same pattern as Task 054's Skeleton Animator extension.

\- This same Attack clip is planned for reuse on Pyromancer later — keep Frost-specific logic in Frost Warden's own MonoBehaviour/Animation Event target method, not baked into shared animation plumbing.

\- Frost Warden is part of an auto-battler (Task 036): heroes auto-attack the nearest enemy target autonomously, with downtime when no target is in range — hence the Idle state.

\- \*\*Ultimate ability animation is explicitly deferred\*\* — no Ultimate clip has been selected yet. Do not add an Ultimate state, parameter, or any placeholder for it in this task.



\---



\## 1. Goal



Complete `FrostWarden\_AnimatorController` (parameters, transitions, Animation Event) so Frost Warden correctly idles when no target is in range and plays its Basic-ability attack animation when attacking, with damage/VFX synced to the animation's impact frame.



\## 2. Scope



\### 2.1 Animator Controller — Parameters



Add to `FrostWarden\_AnimatorController`:

\- `Attack` (Trigger)



(No `Speed`/movement parameter needed — Frost Warden doesn't move during combat per the auto-battler model; flag if you find this assumption wrong.)



\### 2.2 Transitions



\- \*\*Default state: `Idle`.\*\*

\- \*\*Idle → Attack\*\*: condition `Attack` (Trigger), `Has Exit Time = false`.

\- \*\*Attack → Idle\*\*: `Has Exit Time = true`, Exit Time ≈ 1.0, no parameter condition. The Animator returns to Idle after each attack; repeated attacks are driven by gameplay code re-firing the `Attack` trigger on each attack tick/cooldown completion — the Animator does not need to loop Attack internally.



\### 2.3 Animation Event — Hit/VFX Sync



\- Add an Animation Event on the Attack clip at the frame where the weapon visually connects, calling a method `OnBasicAttackImpactFrame()` on Frost Warden's MonoBehaviour (add the method if it doesn't exist).

\- This event must become the trigger point for the Basic ability's existing damage application and VFX spawn (Task 045's projectile/ice-burst). Check what currently triggers damage/VFX for Frost Warden's Basic ability — if it currently fires instantly on ability-cast rather than synced to this animation frame, this is a meaningful behavior change (affects ability feel/timing). Implement the re-sync, but flag clearly in your summary that this changes existing timing, don't make this change silently.

\- Keep the hookup generic/reusable in naming/structure so Pyromancer can wire its own equivalent later using the same clip, without needing changes to this clip or shared plumbing. Frost-specific effects stay in Frost Warden's own ability code reacting to the event.



\### 2.4 Repeated-Trigger Sanity Check



\- Verify that re-triggering `Attack` repeatedly at the hero's actual attack speed/cooldown (from its `AbilityDefinitionSO`) doesn't cause visual popping or restart artifacts. If you observe this, flag it with specifics rather than silently working around it.



\---



\## 3. Out of Scope



\- Ultimate ability animation/state — explicitly deferred, do not add anything for it.

\- Death animation for heroes — not yet scoped.

\- Applying this clip to Pyromancer — future work; only keep the Animation Event hookup generic enough to make that easy later.

\- Any changes to the Frost emission shader (separate task) — unrelated, don't touch.

\- Movement/locomotion states — Frost Warden doesn't move during combat per the auto-battler model.



\---



\## 4. Acceptance Criteria



\- \[ ] `FrostWarden\_AnimatorController` has the `Attack` Trigger parameter added.

\- \[ ] Idle ↔ Attack transitions match §2.2 exactly.

\- \[ ] Animation Event fires at the correct visual impact frame and is wired to the existing Basic-ability damage/VFX logic (Task 045), not firing instantly at ability-cast time — and this timing change is explicitly called out in the summary if it changes prior behavior.

\- \[ ] Repeated rapid triggering at the hero's actual attack speed shows no visual restart glitches.

\- \[ ] The Animation Event hookup is generic enough that Pyromancer can reuse the same clip later without changes to shared plumbing.

\- \[ ] No Ultimate-related additions of any kind.

