#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using Wavekeep.Runtime;

namespace Wavekeep.EditorTools
{
    /// <summary>
    /// Task 54 — one-time, idempotent setup for the Skeleton enemy's animation pipeline. Claude Code cannot drive
    /// the Unity Editor directly, so the controller/clip/prefab changes that must happen in-editor are scripted
    /// here as a single <see cref="MenuItem"/> the developer runs once (re-runnable safely after tweaks).
    ///
    /// What it does:
    /// 1. Completes the existing <b>Skeleton_AnimatorController</b> (it is NOT recreated — only extended):
    ///    adds the <c>Attack</c> and <c>Die</c> Trigger parameters (§2.1) and the transitions in §2.2
    ///    (Any State → Attack, Attack ↔ AttackRecovery exit-time loop, Any State → Death with priority so
    ///    death interrupts the attack loop).
    /// 2. Adds the <c>OnAttackImpactFrame()</c> Animation Event to the Attack clip at the weapon-contact frame (§2.3).
    /// 3. Wires the Skeleton prefab: assigns the controller to its Animator, disables root motion (movement is
    ///    driven by EnemyRuntime — prevents drift, §2.4), adds <see cref="EnemyAnimationDriver"/>, and assigns the
    ///    Animator + best-guess bone VFX anchors (§2.5) + the Death clip length (§2.7).
    ///
    /// The Attack-impact event TIME and the bone anchor choices are best-guesses that need a human eye to confirm —
    /// see the console log this prints and the developer-action notes in the task response.
    /// </summary>
    public static class Task54SkeletonAnimatorSetup
    {
        private const string ControllerPath = "Assets/Data/Animations/Skeleton/Skeleton_AnimatorController.controller";
        private const string AttackFbxPath =
            "Assets/Synty/AnimationSwordCombat/Animations/Sidekick/Attack/HeavyCombo01/A_MOD_SWD_Attack_HeavyCombo01A_Neut.fbx";
        private const string AttackClipName = "A_MOD_SWD_Attack_HeavyCombo01A_Neut";
        // Every enemy prefab that drives the shared Skeleton_AnimatorController needs the same wiring (controller
        // assigned, root motion OFF so EnemyRuntime owns movement, EnemyAnimationDriver added + configured). The
        // EvilGod boss reuses the controller, so it is wired here too. Scaling is never touched, so a boss scaled
        // up in its prefab keeps its size.
        private static readonly string[] EnemyPrefabPaths =
        {
            "Assets/Synty/PolygonGeneric/Prefabs/Characters/SM_Gen_Chr_Skeleton_01.prefab",
            "Assets/Synty/PolygonFantasyRivals/Prefabs/Characters/SM_Chr_EvilGod_01.prefab",
        };

        private const string RunState = "Run";
        private const string AttackState = "Attack";
        private const string AttackRecoveryState = "AttackRecovery";
        private const string DeathState = "Death";
        private const string AttackParam = "Attack";
        private const string DieParam = "Die";
        private const string ImpactEventMethod = "OnAttackImpactFrame";

        // Where in the Attack clip the weapon visually connects, as a fraction of clip length. The clip's bundled
        // sub-takes mark the "Hit" window at frames 30–36 of the 1–62 range, so contact begins ~0.48 in. This is a
        // documented best-guess — fine-tune in the FBX import's Animation tab if the hit reads early/late.
        private const float ImpactNormalizedTime = 0.48f;

        // Candidate bone names (in priority order) for the two VFX anchors, matched against the prefab hierarchy.
        private static readonly string[] HitAnchorBoneCandidates = { "Spine_03", "Chest", "Spine_02", "Spine_01", "Head" };
        private static readonly string[] DeathAnchorBoneCandidates = { "Hips", "Pelvis", "Root" };

        [MenuItem("Wavekeep/Setup Task 54 (Skeleton Animator)")]
        public static void Run()
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller == null)
            {
                Debug.LogError($"[Task54] Animator Controller not found at '{ControllerPath}'. Aborting — this script " +
                               "extends the existing controller and does not create one.");
                return;
            }

            if (controller.layers == null || controller.layers.Length == 0)
            {
                Debug.LogError("[Task54] Controller has no layers. Aborting.");
                return;
            }

            var sm = controller.layers[0].stateMachine;

            var run = FindState(sm, RunState);
            var attack = FindState(sm, AttackState);
            var recovery = FindState(sm, AttackRecoveryState);
            var death = FindState(sm, DeathState);

            if (run == null || attack == null || recovery == null || death == null)
            {
                Debug.LogError($"[Task54] Expected states Run/Attack/AttackRecovery/Death not all present " +
                               $"(found: Run={run != null}, Attack={attack != null}, AttackRecovery={recovery != null}, " +
                               $"Death={death != null}). Aborting rather than recreating them (§0 says the controller " +
                               "already exists with these states).");
                return;
            }

            EnsureDefaultState(sm, run);
            EnsureTrigger(controller, AttackParam);
            EnsureTrigger(controller, DieParam);
            BuildTransitions(sm, attack, recovery, death);

            EditorUtility.SetDirty(controller);

            float impactTime = AddAttackImpactEvent();
            float deathLen = ResolveDeathClipLength(death);
            foreach (var prefabPath in EnemyPrefabPaths)
                WireEnemyPrefab(prefabPath, controller, deathLen);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[Task54] Skeleton animator setup complete. Params: Attack/Die. Transitions: AnyState→Attack, " +
                      $"Attack↔AttackRecovery (exitTime 1.0), AnyState→Death (priority). Impact event '{ImpactEventMethod}' " +
                      $"@ {impactTime:0.000}s on '{AttackClipName}'. Death clip length ≈ {deathLen:0.000}s. " +
                      $"Wired {EnemyPrefabPaths.Length} enemy prefab(s) (controller + root-motion-off + driver). " +
                      "VERIFY: the impact frame timing and the auto-picked bone anchors (see warnings above, if any).");
        }

        // ---- Controller: parameters --------------------------------------------------------------------------

        private static void EnsureTrigger(AnimatorController controller, string name)
        {
            foreach (var p in controller.parameters)
            {
                if (p.name != name) continue;
                if (p.type != AnimatorControllerParameterType.Trigger)
                    Debug.LogWarning($"[Task54] Parameter '{name}' exists but is {p.type}, expected Trigger. Left as-is.");
                return; // already present — idempotent
            }
            controller.AddParameter(name, AnimatorControllerParameterType.Trigger);
        }

        private static void EnsureDefaultState(AnimatorStateMachine sm, AnimatorState run)
        {
            if (sm.defaultState != run) sm.defaultState = run; // §2.2: Run is the entry/default state
        }

        // ---- Controller: transitions -------------------------------------------------------------------------

        private static void BuildTransitions(AnimatorStateMachine sm, AnimatorState attack,
            AnimatorState recovery, AnimatorState death)
        {
            // Idempotent: strip any transitions this script manages, then rebuild them fresh so re-running never
            // duplicates. (The controller starts with no transitions; this also cleans up after earlier runs.)
            RemoveAnyStateTransitionsTo(sm, attack);
            RemoveAnyStateTransitionsTo(sm, death);
            RemoveTransitionsBetween(attack, recovery);
            RemoveTransitionsBetween(recovery, attack);

            // Any State → Death FIRST so it has the highest AnyState priority — death always wins over an
            // in-progress Attack/AttackRecovery loop (§2.2). hasExitTime=false → fires the instant Die is set.
            var toDeath = sm.AddAnyStateTransition(death);
            toDeath.hasExitTime = false;
            toDeath.duration = 0f;
            toDeath.hasFixedDuration = true;
            toDeath.canTransitionToSelf = false;
            toDeath.AddCondition(AnimatorConditionMode.If, 0f, DieParam);

            // Any State → Attack: interrupts Run the instant Attack is triggered on wall arrival.
            var toAttack = sm.AddAnyStateTransition(attack);
            toAttack.hasExitTime = false;
            toAttack.duration = 0f;
            toAttack.hasFixedDuration = true;
            toAttack.canTransitionToSelf = false;
            toAttack.AddCondition(AnimatorConditionMode.If, 0f, AttackParam);

            // Attack → AttackRecovery: exit-time loop, no parameter condition (§2.2).
            var attackToRecovery = attack.AddTransition(recovery);
            attackToRecovery.hasExitTime = true;
            attackToRecovery.exitTime = 1.0f;
            attackToRecovery.duration = 0.1f;
            attackToRecovery.hasFixedDuration = true;

            // AttackRecovery → Attack: closes the loop; the Skeleton keeps attacking until it dies (§2.2).
            var recoveryToAttack = recovery.AddTransition(attack);
            recoveryToAttack.hasExitTime = true;
            recoveryToAttack.exitTime = 1.0f;
            recoveryToAttack.duration = 0.1f;
            recoveryToAttack.hasFixedDuration = true;
        }

        private static void RemoveAnyStateTransitionsTo(AnimatorStateMachine sm, AnimatorState dest)
        {
            foreach (var t in new List<AnimatorStateTransition>(sm.anyStateTransitions))
                if (t.destinationState == dest) sm.RemoveAnyStateTransition(t);
        }

        private static void RemoveTransitionsBetween(AnimatorState from, AnimatorState to)
        {
            foreach (var t in new List<AnimatorStateTransition>(from.transitions))
                if (t.destinationState == to) from.RemoveTransition(t);
        }

        private static AnimatorState FindState(AnimatorStateMachine sm, string name)
        {
            foreach (var cs in sm.states)
                if (cs.state != null && cs.state.name == name) return cs.state;
            return null;
        }

        // ---- Attack clip: impact Animation Event -------------------------------------------------------------

        // Returns the event time in seconds (for logging), or a negative value if the event couldn't be added.
        private static float AddAttackImpactEvent()
        {
            var importer = AssetImporter.GetAtPath(AttackFbxPath) as ModelImporter;
            if (importer == null)
            {
                Debug.LogError($"[Task54] Attack FBX importer not found at '{AttackFbxPath}'. Animation Event NOT added.");
                return -1f;
            }

            float clipLength = GetClipLength(AttackFbxPath, AttackClipName);
            float eventTime = clipLength > 0f ? clipLength * ImpactNormalizedTime : 1.0f;
            if (clipLength <= 0f)
                Debug.LogWarning($"[Task54] Could not read length of '{AttackClipName}'; defaulting impact event to {eventTime:0.000}s.");

            var clips = importer.clipAnimations;
            if (clips == null || clips.Length == 0) clips = importer.defaultClipAnimations; // honour the FBX's own takes

            bool applied = false;
            for (int i = 0; i < clips.Length; i++)
            {
                if (clips[i].name != AttackClipName) continue;

                // Idempotent: drop any prior OnAttackImpactFrame event before adding the fresh one.
                var events = new List<AnimationEvent>(clips[i].events ?? new AnimationEvent[0]);
                events.RemoveAll(e => e.functionName == ImpactEventMethod);
                events.Add(new AnimationEvent { functionName = ImpactEventMethod, time = eventTime });
                events.Sort((a, b) => a.time.CompareTo(b.time));
                clips[i].events = events.ToArray();
                applied = true;
                break;
            }

            if (!applied)
            {
                Debug.LogError($"[Task54] Clip '{AttackClipName}' not found in the FBX importer's clip list. Event NOT added.");
                return -1f;
            }

            importer.clipAnimations = clips;
            EditorUtility.SetDirty(importer);
            importer.SaveAndReimport();
            return eventTime;
        }

        private static float GetClipLength(string fbxPath, string clipName)
        {
            foreach (var obj in AssetDatabase.LoadAllAssetsAtPath(fbxPath))
                if (obj is AnimationClip clip && clip.name == clipName) return clip.length;
            return -1f;
        }

        private static float ResolveDeathClipLength(AnimatorState death)
        {
            if (death.motion is AnimationClip clip && clip.length > 0f) return clip.length;
            Debug.LogWarning("[Task54] Could not read Death state clip length; EnemyAnimationDriver keeps its inspector default.");
            return -1f;
        }

        // ---- Skeleton prefab: controller + driver + anchors --------------------------------------------------

        private static void WireEnemyPrefab(string prefabPath, AnimatorController controller, float deathClipLength)
        {
            var root = PrefabUtility.LoadPrefabContents(prefabPath);
            if (root == null)
            {
                Debug.LogError($"[Task54] Enemy prefab not found at '{prefabPath}'. Prefab wiring skipped — " +
                               "assign the controller + EnemyAnimationDriver manually (see task response).");
                return;
            }

            try
            {
                var animator = root.GetComponent<Animator>();
                if (animator == null)
                {
                    Debug.LogError($"[Task54] Prefab '{prefabPath}' root has no Animator component. Prefab wiring skipped.");
                    return;
                }

                animator.runtimeAnimatorController = controller;
                // §2.4: EnemyRuntime owns movement. Leaving root motion ON makes the Run clip's root translation
                // fight/override the transform-driven movement, so the enemy never settles at the wall (the boss
                // "won't react to the wall"). Must be off for every enemy using this controller.
                animator.applyRootMotion = false;

                var driver = root.GetComponent<EnemyAnimationDriver>();
                if (driver == null) driver = root.AddComponent<EnemyAnimationDriver>();

                var hitAnchor = FindFirstBone(root.transform, HitAnchorBoneCandidates);
                var deathAnchor = FindFirstBone(root.transform, DeathAnchorBoneCandidates);

                if (hitAnchor == null)
                    Debug.LogWarning($"[Task54] {root.name}: no hit-VFX bone found ({string.Join("/", HitAnchorBoneCandidates)}); " +
                                     "anchor falls back to the root. Assign a chest/head bone in the inspector.");
                if (deathAnchor == null)
                    Debug.LogWarning($"[Task54] {root.name}: no death-VFX bone found ({string.Join("/", DeathAnchorBoneCandidates)}); " +
                                     "anchor falls back to the root. Assign a hips/root bone in the inspector.");

                driver.EditorConfigure(animator, hitAnchor, deathAnchor, deathClipLength);

                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                Debug.Log($"[Task54] Prefab '{root.name}' wired: controller assigned, root motion off, " +
                          $"EnemyAnimationDriver(hit='{(hitAnchor != null ? hitAnchor.name : "root")}', " +
                          $"death='{(deathAnchor != null ? deathAnchor.name : "root")}').");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static Transform FindFirstBone(Transform root, string[] candidateNames)
        {
            foreach (var name in candidateNames)
            {
                var found = FindInHierarchy(root, name);
                if (found != null) return found;
            }
            return null;
        }

        private static Transform FindInHierarchy(Transform root, string name)
        {
            if (root.name == name) return root;
            foreach (Transform child in root)
            {
                var found = FindInHierarchy(child, name);
                if (found != null) return found;
            }
            return null;
        }
    }
}
#endif
