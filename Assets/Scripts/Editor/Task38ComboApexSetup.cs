#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Wavekeep.Core;
using Wavekeep.Data;
using Wavekeep.UI;

namespace Wavekeep.EditorTools
{
    /// <summary>
    /// Task 38 — authors Frozen Lightning (the first cross-hero combo apex) and wires it into the gameplay
    /// scene:
    /// <list type="number">
    /// <item>Creates a <see cref="ComboApexTalentDefinitionSO"/> (Passive) referencing Frost Warden's
    ///   Remorseless Winter (primer) + Bolt Striker's Lethal Surge (consumer), window 2s, multiplier ×2.5.</item>
    /// <item>Assigns it to the scene's <see cref="GameSessionBootstrap"/> combo list so the run resolver sees
    ///   it.</item>
    /// <item>Adds a <see cref="ComboApexIndicatorDisplay"/> (a passive "ACTIVE" badge, distinct from the
    ///   cooldown-bar apex display) above the apex cooldown bars.</item>
    /// </list>
    /// Run "Wavekeep/Setup Task 38 (Frozen Lightning)" in the gameplay scene AFTER the Task 31/35/36 setups
    /// (the two apex assets must exist), then save the scene. Idempotent. Editor-only.
    /// </summary>
    public static class Task38ComboApexSetup
    {
        private const string ComboFolder = "Assets/Data/ComboApexes";
        private const string ComboPath = "Assets/Data/ComboApexes/ComboApex_FrozenLightning.asset";
        private const string RemorselessWinterPath = "Assets/Data/UpgradeLines/ApexTalent_RemorselessWinter.asset";
        private const string LethalSurgePath = "Assets/Data/UpgradeLines/ApexTalent_LethalSurge.asset";
        private const string IndicatorRootName = "ComboApexIndicators";

        [MenuItem("Wavekeep/Setup Task 38 (Frozen Lightning)")]
        public static void SetupScene()
        {
            var remorselessWinter = AssetDatabase.LoadAssetAtPath<ApexTalentDefinitionSO>(RemorselessWinterPath);
            var lethalSurge = AssetDatabase.LoadAssetAtPath<ApexTalentDefinitionSO>(LethalSurgePath);
            if (remorselessWinter == null || lethalSurge == null)
            {
                Debug.LogError("[Task38] Missing apex assets (Remorseless Winter / Lethal Surge). Run the Task 31 " +
                               "and Task 35 content setups first.");
                return;
            }

            var combo = AuthorFrozenLightning(remorselessWinter, lethalSurge);

            var bootstrap = Object.FindFirstObjectByType<GameSessionBootstrap>();
            if (bootstrap == null)
            {
                Debug.LogError("[Task38] No GameSessionBootstrap in the open scene. Open the gameplay scene and " +
                               "run the earlier setups first. (The Frozen Lightning asset was still authored.)");
                return;
            }

            WireBootstrap(bootstrap, combo);
            BuildIndicatorDisplay(bootstrap);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log("[Task38] Frozen Lightning authored (Remorseless Winter primes → Lethal Surge consumes, " +
                      "2s window, ×2.5) and wired into the scene bootstrap; passive 'ACTIVE' badge added above the " +
                      "apex cooldown bars. Save the scene (Ctrl+S).");
        }

        private static ComboApexTalentDefinitionSO AuthorFrozenLightning(
            ApexTalentDefinitionSO primer, ApexTalentDefinitionSO consumer)
        {
            EnsureFolder("Assets/Data", "ComboApexes");
            var combo = AbilityAssetUtil.LoadOrCreate<ComboApexTalentDefinitionSO>(ComboPath);
            var so = new SerializedObject(combo);
            so.FindProperty("_comboName").stringValue = "Frozen Lightning";
            so.FindProperty("_triggerType").enumValueIndex = (int)ComboApexTriggerType.Passive;
            so.FindProperty("_primingApex").objectReferenceValue = primer;
            so.FindProperty("_consumingApex").objectReferenceValue = consumer;
            so.FindProperty("_primeWindowSeconds").floatValue = 2f;
            so.FindProperty("_consumeDamageMultiplier").floatValue = 2.5f;
            so.ApplyModifiedPropertiesWithoutUndo();
            return combo;
        }

        private static void WireBootstrap(GameSessionBootstrap bootstrap, ComboApexTalentDefinitionSO combo)
        {
            var so = new SerializedObject(bootstrap);
            var list = so.FindProperty("_comboApexes");
            // Idempotent: keep the asset present exactly once.
            bool present = false;
            for (int i = 0; i < list.arraySize; i++)
                if (list.GetArrayElementAtIndex(i).objectReferenceValue == combo) present = true;
            if (!present)
            {
                list.arraySize += 1;
                list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = combo;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void BuildIndicatorDisplay(GameSessionBootstrap bootstrap)
        {
            var canvas = Object.FindFirstObjectByType<Canvas>();
            if (canvas == null)
            {
                Debug.LogWarning("[Task38] No Canvas in scene; skipped the combo-apex badge. Run the Task 01/07 setups.");
                return;
            }

            DestroyIfExists(IndicatorRootName); // idempotent re-run

            var uiSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");

            var rootGo = new GameObject(IndicatorRootName,
                typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            rootGo.transform.SetParent(canvas.transform, false);
            var rt = (RectTransform)rootGo.transform;
            rt.anchorMin = new Vector2(0.5f, 0f);
            rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = new Vector2(0f, 260f); // above the apex cooldown bars (Task 36 lifted them to ~200)
            rt.sizeDelta = new Vector2(380f, 0f);

            var vlg = rootGo.GetComponent<VerticalLayoutGroup>();
            vlg.spacing = 4f;
            vlg.childAlignment = TextAnchor.LowerCenter;
            vlg.childControlWidth = true;
            vlg.childControlHeight = false;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            rootGo.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var display = rootGo.AddComponent<ComboApexIndicatorDisplay>();
            var so = new SerializedObject(display);
            so.FindProperty("_container").objectReferenceValue = rt;
            so.FindProperty("_badgeSprite").objectReferenceValue = uiSprite;
            so.FindProperty("_bootstrap").objectReferenceValue = bootstrap;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void EnsureFolder(string parent, string child)
        {
            if (!AssetDatabase.IsValidFolder($"{parent}/{child}"))
                AssetDatabase.CreateFolder(parent, child);
        }

        private static void DestroyIfExists(string objectName)
        {
            var existing = GameObject.Find(objectName);
            if (existing != null) Object.DestroyImmediate(existing);
        }
    }
}
#endif
