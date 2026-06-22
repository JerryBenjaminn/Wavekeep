#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Wavekeep.Core;
using Wavekeep.Data;
using Wavekeep.UI;
using Wavekeep.Waves;

namespace Wavekeep.EditorTools
{
    /// <summary>
    /// Builds the Task 08 additions on top of Tasks 01–07: a Victory/Defeat end-screen Canvas wired to
    /// a <see cref="RunEndScreen"/>, and the Part A range fix — bumping the two AoE ability assets'
    /// radii so they actually reach wall-edge enemies (see the Task 08 report for the diagnosis).
    ///
    /// The ability feedback indicators need NO scene/asset wiring: <c>HeroRuntime</c> adds the
    /// <c>AbilityIndicatorPresenter</c> at runtime and it builds its own LineRenderers. So this script
    /// only handles the end screen + the data fix. Built in code like Tasks 01–07 (scenes can't be
    /// reliably hand-authored as YAML). Run "Wavekeep/Setup Task 08 (Feedback &amp; End Screen)" after
    /// the Task 01–07 setups, then save the scene. Editor-only.
    /// </summary>
    public static class Task08SceneSetup
    {
        // The two AoE abilities whose radii were too small for the wall width (Part A diagnosis).
        private const string NovaPath = "Assets/Data/Abilities/UltimateNova.asset";       // Hero A ultimate
        private const string FrostNovaPath = "Assets/Data/Abilities/BasicFrostNova.asset"; // Hero B basic

        // Edge enemies sit ~12.8 units from the set-back caster; these cover the full wall width + margin.
        private const float NovaFixedRange = 16f;
        private const float FrostNovaFixedRange = 14f;

        [MenuItem("Wavekeep/Setup Task 08 (Feedback & End Screen)")]
        public static void SetupScene()
        {
            var bootstrap = Object.FindFirstObjectByType<GameSessionBootstrap>();
            var waveSpawner = Object.FindFirstObjectByType<WaveSpawner>();
            var canvas = Object.FindFirstObjectByType<Canvas>();
            if (bootstrap == null || waveSpawner == null || canvas == null)
            {
                Debug.LogError("[Task08SceneSetup] Missing GameSessionBootstrap / WaveSpawner / Canvas. Run the Task 01–07 setups first.");
                return;
            }

            // --- Part A range fix: enlarge the AoE radii so they reach wall-edge enemies. ---
            FixAbilityRange(NovaPath, NovaFixedRange);
            FixAbilityRange(FrostNovaPath, FrostNovaFixedRange);
            AssetDatabase.SaveAssets();

            // --- Part B: end-screen UI (idempotent re-run). ---
            DestroyIfExists("RunEndPanel");
            DestroyIfExists("RunEndScreen");
            BuildEndScreen(canvas, bootstrap, waveSpawner);

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log("[Task08SceneSetup] End screen built + AoE ranges fixed. Play: clear all waves → Victory, or let the wall fall → Defeat; Play Again reloads for a fresh run. Save the scene (Ctrl+S).");
        }

        private static void FixAbilityRange(string path, float range)
        {
            var ability = AssetDatabase.LoadAssetAtPath<AbilityDefinitionSO>(path);
            if (ability == null)
            {
                Debug.LogWarning($"[Task08SceneSetup] Ability asset not found at {path}; skipping range fix. Run the Task 04/05 setups first.");
                return;
            }
            var so = new SerializedObject(ability);
            so.FindProperty("_range").floatValue = range;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void BuildEndScreen(Canvas canvas, GameSessionBootstrap bootstrap, WaveSpawner waveSpawner)
        {
            if (TMP_Settings.defaultFontAsset == null)
            {
                Debug.LogWarning("[Task08SceneSetup] TMP has no default font asset. If end-screen text doesn't render, " +
                                 "import via Window > TextMeshPro > Import TMP Essential Resources, then re-run.");
            }

            // Full-screen dimmed panel (controller hides it on Start; shown only when the run ends).
            var panel = new GameObject("RunEndPanel", typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(canvas.transform, false);
            var prt = (RectTransform)panel.transform;
            prt.anchorMin = Vector2.zero;
            prt.anchorMax = Vector2.one;
            prt.offsetMin = Vector2.zero;
            prt.offsetMax = Vector2.zero;
            panel.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.85f);

            var title = CreateText(panel.transform, "Title", "Victory!", 60f, TextAlignmentOptions.Center,
                new Vector2(0.5f, 0.5f), new Vector2(0f, 140f), new Vector2(700f, 90f));

            var stats = CreateText(panel.transform, "Stats", "Wave reached: 0\nCurrency: 0\nLevel: 1", 28f,
                TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), new Vector2(0f, 20f), new Vector2(500f, 140f));

            var playAgain = CreateButton(panel.transform, "PlayAgainButton", "Play Again",
                new Vector2(0.5f, 0.5f), new Vector2(0f, -120f), new Vector2(260f, 60f));

            // Controller on a separate root object so hiding the panel doesn't disable it.
            var screenGo = new GameObject("RunEndScreen", typeof(RunEndScreen));
            var controller = screenGo.GetComponent<RunEndScreen>();
            var so = new SerializedObject(controller);
            so.FindProperty("_bootstrap").objectReferenceValue = bootstrap;
            so.FindProperty("_waveSpawner").objectReferenceValue = waveSpawner;
            so.FindProperty("_panel").objectReferenceValue = panel;
            so.FindProperty("_titleText").objectReferenceValue = title;
            so.FindProperty("_statsText").objectReferenceValue = stats;
            so.FindProperty("_playAgainButton").objectReferenceValue = playAgain;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static TextMeshProUGUI CreateText(
            Transform parent, string name, string text, float fontSize, TextAlignmentOptions alignment,
            Vector2 anchor, Vector2 anchoredPosition, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.color = Color.white;
            tmp.alignment = alignment;
            if (TMP_Settings.defaultFontAsset != null) tmp.font = TMP_Settings.defaultFontAsset;
            var rt = tmp.rectTransform;
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = anchor;
            rt.anchoredPosition = anchoredPosition;
            rt.sizeDelta = size;
            return tmp;
        }

        private static Button CreateButton(
            Transform parent, string name, string label, Vector2 anchor, Vector2 anchoredPosition, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = anchor;
            rt.anchoredPosition = anchoredPosition;
            rt.sizeDelta = size;

            var labelGo = new GameObject("Label", typeof(RectTransform));
            labelGo.transform.SetParent(go.transform, false);
            var tmp = labelGo.AddComponent<TextMeshProUGUI>();
            tmp.text = label;
            tmp.fontSize = 26f;
            tmp.color = Color.black;
            tmp.alignment = TextAlignmentOptions.Center;
            if (TMP_Settings.defaultFontAsset != null) tmp.font = TMP_Settings.defaultFontAsset;
            var lrt = tmp.rectTransform;
            lrt.anchorMin = Vector2.zero;
            lrt.anchorMax = Vector2.one;
            lrt.offsetMin = Vector2.zero;
            lrt.offsetMax = Vector2.zero;

            return go.GetComponent<Button>();
        }

        private static void DestroyIfExists(string objectName)
        {
            var existing = GameObject.Find(objectName);
            if (existing != null)
            {
                Object.DestroyImmediate(existing);
            }
        }
    }
}
#endif
