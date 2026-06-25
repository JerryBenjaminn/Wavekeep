#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Wavekeep.Core;
using Wavekeep.Data;
using Wavekeep.Runtime;
using Wavekeep.UI;

namespace Wavekeep.EditorTools
{
    /// <summary>
    /// Task 36 — wires the gameplay scene for dual-hero runs:
    /// <list type="number">
    /// <item>Sets the <see cref="HeroSelectController"/>'s debug team to Frost Warden + Bolt Striker (the
    ///   hardcoded pair this task assumes; the Hub team-select UI is a later task).</item>
    /// <item>Adds a <see cref="HeroTeamController"/> (global auto-ultimate toggle + per-hero manual keys).</item>
    /// <item>Rebuilds the ultimate charge bar as a per-hero vertical stack (the component is now multi-bar),
    ///   and wires both HUD displays to the session so they read the hero registry.</item>
    /// </list>
    /// Run "Wavekeep/Setup Task 36 (Dual Hero)" in the gameplay scene after the Task 21/32 setups, then save
    /// the scene. Idempotent. Editor-only.
    /// </summary>
    public static class Task36DualHeroSetup
    {
        private const string FrostWardenPath = "Assets/Data/Heroes/Hero_FrostWarden.asset";
        private const string BoltStrikerPath = "Assets/Data/Heroes/Hero_BoltStriker.asset";
        private const string UltimateBarName = "UltimateChargeBar";
        private const string TeamControllerName = "HeroTeamController";
        private const string ApexRootName = "ApexCooldownBars";

        [MenuItem("Wavekeep/Setup Task 36 (Dual Hero)")]
        public static void SetupScene()
        {
            var bootstrap = Object.FindFirstObjectByType<GameSessionBootstrap>();
            if (bootstrap == null)
            {
                Debug.LogError("[Task36] No GameSessionBootstrap in scene. Run the earlier scene setups first.");
                return;
            }

            ConfigureDebugTeam();
            AddTeamController(bootstrap);
            RebuildUltimateBars(bootstrap);
            WireApexDisplay(bootstrap);

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log("[Task36] Dual-hero wiring applied: debug team = Frost Warden + Bolt Striker; " +
                      "HeroTeamController added (T toggles auto-ultimate; U/I = per-hero manual cast when off); " +
                      "ultimate bar is now one bar per hero; HUD displays read the session registry. Save (Ctrl+S).");
        }

        private static void ConfigureDebugTeam()
        {
            var select = Object.FindFirstObjectByType<HeroSelectController>();
            if (select == null)
            {
                Debug.LogWarning("[Task36] No HeroSelectController found; skipped debug-team wiring. Run Task 05 setup.");
                return;
            }

            var frost = AssetDatabase.LoadAssetAtPath<HeroDefinitionSO>(FrostWardenPath);
            var bolt = AssetDatabase.LoadAssetAtPath<HeroDefinitionSO>(BoltStrikerPath);
            if (frost == null || bolt == null)
            {
                Debug.LogWarning("[Task36] Missing Frost Warden / Bolt Striker hero assets; debug team left as-is.");
                return;
            }

            var so = new SerializedObject(select);
            var team = so.FindProperty("_debugTeam");
            team.arraySize = 2;
            team.GetArrayElementAtIndex(0).objectReferenceValue = frost;
            team.GetArrayElementAtIndex(1).objectReferenceValue = bolt;
            so.FindProperty("_teamSpacing").floatValue = 3f;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void AddTeamController(GameSessionBootstrap bootstrap)
        {
            DestroyIfExists(TeamControllerName);
            var go = new GameObject(TeamControllerName, typeof(HeroTeamController));
            var so = new SerializedObject(go.GetComponent<HeroTeamController>());
            so.FindProperty("_bootstrap").objectReferenceValue = bootstrap;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // The ultimate bar is now a generated per-hero stack, so rebuild its object as a vertical container.
        private static void RebuildUltimateBars(GameSessionBootstrap bootstrap)
        {
            var canvas = Object.FindFirstObjectByType<Canvas>();
            if (canvas == null)
            {
                Debug.LogWarning("[Task36] No Canvas; skipped ultimate-bar rebuild.");
                return;
            }

            DestroyIfExists(UltimateBarName);

            var uiSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");

            var rootGo = new GameObject(UltimateBarName,
                typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            rootGo.transform.SetParent(canvas.transform, false);
            var rt = (RectTransform)rootGo.transform;
            rt.anchorMin = new Vector2(0.5f, 0f);
            rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = new Vector2(0f, 12f); // bottom-centre; grows upward as bars are added
            rt.sizeDelta = new Vector2(420f, 0f);

            var vlg = rootGo.GetComponent<VerticalLayoutGroup>();
            vlg.spacing = 4f;
            vlg.childAlignment = TextAnchor.LowerCenter;
            vlg.childControlWidth = true;
            vlg.childControlHeight = false;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            rootGo.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var bar = rootGo.AddComponent<UltimateChargeBar>();
            var so = new SerializedObject(bar);
            so.FindProperty("_container").objectReferenceValue = rt;
            so.FindProperty("_barSprite").objectReferenceValue = uiSprite;
            so.FindProperty("_bootstrap").objectReferenceValue = bootstrap;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // Wire the Task 32 apex display to the session (so it reads ALL heroes' apexes) and lift it clear of
        // the now-taller (two-bar) ultimate stack so the two HUD groups don't overlap.
        private static void WireApexDisplay(GameSessionBootstrap bootstrap)
        {
            var display = Object.FindFirstObjectByType<ApexCooldownDisplay>();
            if (display == null)
            {
                Debug.LogWarning("[Task36] No ApexCooldownDisplay found; run the Task 32 setup. Skipped.");
                return;
            }

            var so = new SerializedObject(display);
            so.FindProperty("_bootstrap").objectReferenceValue = bootstrap;
            so.ApplyModifiedPropertiesWithoutUndo();

            var apexRoot = GameObject.Find(ApexRootName);
            if (apexRoot != null)
            {
                var rt = (RectTransform)apexRoot.transform;
                rt.anchoredPosition = new Vector2(0f, 200f); // above the two-hero ultimate stack
            }
        }

        private static void DestroyIfExists(string objectName)
        {
            var existing = GameObject.Find(objectName);
            if (existing != null) Object.DestroyImmediate(existing);
        }
    }
}
#endif
