#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Wavekeep.EditorTools
{
    /// <summary>
    /// Task 56 — completes <b>FrostWarden_AnimatorController</b> (Attack param, Idle↔Attack transitions, the
    /// basic-attack impact Animation Event) and wires the controller onto the Frost Warden model, following the
    /// same MenuItem pattern as Task 54's Skeleton setup.
    ///
    /// IMPORTANT (see the Task 56 summary): this script does NOT create the Idle/Attack states or pick the Attack
    /// clip — §0 says the developer already authored them. If the controller has no Idle/Attack states (it is
    /// currently EMPTY), the script aborts with a clear message rather than fabricating them or guessing the clip.
    /// Once the two states exist (Idle → A_Idle_Base_Sheated_Sword, Attack → the chosen Block clip), re-run this
    /// to finish the wiring; it reads the Attack clip directly from the Attack state's motion.
    /// </summary>
    public static class Task56FrostWardenAnimatorSetup
    {
        private const string ControllerPath = "Assets/Data/Animations/Heroes/FrostWarden_AnimatorController.controller";
        private const string FrostWardenPrefabPath =
            "Assets/Synty/PolygonFantasyCharacters/Prefabs/SM_Chr_Male_Rouge_01.prefab";

        private const string IdleState = "Idle";
        private const string AttackState = "Attack";
        private const string AttackParam = "Attack";
        private const string ImpactEventMethod = "OnBasicAttackImpactFrame";

        // Where the weapon visually connects, as a fraction of the (developer-chosen, unknown-here) Attack clip.
        // A reasonable default — fine-tune in the FBX Animation import tab once the real clip is in place.
        private const float ImpactNormalizedTime = 0.4f;

        [MenuItem("Wavekeep/Setup Task 56 (Frost Warden Animator)")]
        public static void Run()
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller == null)
            {
                Debug.LogError($"[Task56] Animator Controller not found at '{ControllerPath}'. Aborting.");
                return;
            }

            if (controller.layers == null || controller.layers.Length == 0)
            {
                Debug.LogError($"[Task56] '{ControllerPath}' is EMPTY (no layers/states). Task 56 §0 expected two " +
                               "populated states (Idle → A_Idle_Base_Sheated_Sword, Attack → a Block clip), but the " +
                               "controller has none. Add those two states (do not need the transitions — this script " +
                               "adds those), then re-run. Not fabricating states / guessing the Attack clip.");
                return;
            }

            var sm = controller.layers[0].stateMachine;
            var idle = FindState(sm, IdleState);
            var attack = FindState(sm, AttackState);
            if (idle == null || attack == null)
            {
                Debug.LogError($"[Task56] Expected states not found (Idle={idle != null}, Attack={attack != null}). " +
                               "Add both to FrostWarden_AnimatorController per §0, then re-run. Aborting rather than " +
                               "recreating them / guessing the Attack clip.");
                return;
            }

            EnsureDefaultState(sm, idle);
            EnsureTrigger(controller, AttackParam);
            BuildTransitions(idle, attack);
            EditorUtility.SetDirty(controller);

            float impactTime = AddAttackImpactEvent(attack);
            WirePrefab(controller);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[Task56] Frost Warden animator setup complete. Param: Attack. Transitions: Idle→Attack " +
                      $"(no exit time), Attack→Idle (exitTime 1.0). Impact event '{ImpactEventMethod}' " +
                      $"{(impactTime >= 0f ? $"@ {impactTime:0.000}s" : "NOT added (see warnings)")}. Controller assigned " +
                      "to the prefab, root motion off. VERIFY the impact frame timing by eye.");
        }

        // ---- Controller: parameter / default state / transitions ---------------------------------------------

        private static void EnsureTrigger(AnimatorController controller, string name)
        {
            foreach (var p in controller.parameters)
            {
                if (p.name != name) continue;
                if (p.type != AnimatorControllerParameterType.Trigger)
                    Debug.LogWarning($"[Task56] Parameter '{name}' exists but is {p.type}, expected Trigger. Left as-is.");
                return;
            }
            controller.AddParameter(name, AnimatorControllerParameterType.Trigger);
        }

        private static void EnsureDefaultState(AnimatorStateMachine sm, AnimatorState idle)
        {
            if (sm.defaultState != idle) sm.defaultState = idle; // §2.2: Idle is the default state
        }

        private static void BuildTransitions(AnimatorState idle, AnimatorState attack)
        {
            // Idempotent: strip the transitions we manage, then rebuild (safe to re-run).
            RemoveTransitionsBetween(idle, attack);
            RemoveTransitionsBetween(attack, idle);

            // Idle → Attack: fires the instant the Attack trigger is set (gameplay re-fires it per attack).
            var idleToAttack = idle.AddTransition(attack);
            idleToAttack.hasExitTime = false;
            idleToAttack.duration = 0f;
            idleToAttack.hasFixedDuration = true;
            idleToAttack.AddCondition(AnimatorConditionMode.If, 0f, AttackParam);

            // Attack → Idle: plays the full Attack clip, then returns to Idle (no loop; repeats are code-driven).
            var attackToIdle = attack.AddTransition(idle);
            attackToIdle.hasExitTime = true;
            attackToIdle.exitTime = 1.0f;
            attackToIdle.duration = 0.1f;
            attackToIdle.hasFixedDuration = true;
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

        // ---- Attack clip: impact Animation Event (read from the Attack state's own motion) --------------------

        private static float AddAttackImpactEvent(AnimatorState attack)
        {
            if (!(attack.motion is AnimationClip clip))
            {
                Debug.LogWarning("[Task56] Attack state has no AnimationClip motion (blend tree / empty?); impact " +
                                 "event NOT added. Assign the Attack clip to the Attack state, then re-run.");
                return -1f;
            }

            string clipPath = AssetDatabase.GetAssetPath(clip);
            float eventTime = clip.length > 0f ? clip.length * ImpactNormalizedTime : 0.4f;

            // Synty clips are FBX sub-assets → set the event through the ModelImporter (persists across reimport).
            var importer = AssetImporter.GetAtPath(clipPath) as ModelImporter;
            if (importer == null)
            {
                Debug.LogWarning($"[Task56] Attack clip '{clipPath}' is not an FBX/ModelImporter asset; impact event " +
                                 "NOT added automatically. Add an Animation Event calling " +
                                 $"'{ImpactEventMethod}' to the clip manually at the weapon-contact frame.");
                return -1f;
            }

            var clips = importer.clipAnimations;
            if (clips == null || clips.Length == 0) clips = importer.defaultClipAnimations;

            bool applied = false;
            for (int i = 0; i < clips.Length; i++)
            {
                if (clips[i].name != clip.name) continue;
                var events = new List<AnimationEvent>(clips[i].events ?? new AnimationEvent[0]);
                events.RemoveAll(e => e.functionName == ImpactEventMethod); // idempotent
                events.Add(new AnimationEvent { functionName = ImpactEventMethod, time = eventTime });
                events.Sort((a, b) => a.time.CompareTo(b.time));
                clips[i].events = events.ToArray();
                applied = true;
                break;
            }

            if (!applied)
            {
                Debug.LogWarning($"[Task56] Clip '{clip.name}' not found in importer clip list at '{clipPath}'; " +
                                 "event NOT added.");
                return -1f;
            }

            importer.clipAnimations = clips;
            EditorUtility.SetDirty(importer);
            importer.SaveAndReimport();
            return eventTime;
        }

        // ---- Prefab: assign controller + disable root motion -------------------------------------------------

        private static void WirePrefab(AnimatorController controller)
        {
            var root = PrefabUtility.LoadPrefabContents(FrostWardenPrefabPath);
            if (root == null)
            {
                Debug.LogError($"[Task56] Frost Warden prefab not found at '{FrostWardenPrefabPath}'. Controller " +
                               "completed but not assigned — assign it to the model's Animator manually.");
                return;
            }

            try
            {
                var animator = root.GetComponentInChildren<Animator>(true);
                if (animator == null)
                {
                    Debug.LogError("[Task56] No Animator found on the Frost Warden prefab; cannot assign controller.");
                    return;
                }

                animator.runtimeAnimatorController = controller;
                // Same rationale as Task 54: the hero is positioned by gameplay, not root motion — leaving it on
                // lets the Attack/Idle clips drift the model.
                animator.applyRootMotion = false;

                PrefabUtility.SaveAsPrefabAsset(root, FrostWardenPrefabPath);
                Debug.Log($"[Task56] Controller assigned to '{animator.name}' on the Frost Warden prefab; root motion off.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }
}
#endif
