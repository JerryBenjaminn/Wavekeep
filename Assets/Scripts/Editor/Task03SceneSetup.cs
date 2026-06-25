#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Wavekeep.Core;
using Wavekeep.UI;
using Wavekeep.Waves;

namespace Wavekeep.EditorTools
{
    /// <summary>
    /// One-shot editor utility that adds the Task 03 placeholder economy display to the existing
    /// Task 01 Canvas: two TextMeshPro readouts (currency total, level/XP progress) and an
    /// <see cref="EconomyDebugHud"/> wired to them and to the scene's <see cref="GameSessionBootstrap"/>.
    ///
    /// Built in code for the same reason as Tasks 01/02 (scenes can't be reliably hand-authored as
    /// YAML outside the editor). Run "Wavekeep/Setup Task 03 (Economy HUD)" after the Task 01/02
    /// setups, then save the scene. Editor-only; not part of the runtime build.
    /// </summary>
    public static class Task03SceneSetup
    {
        [MenuItem("Wavekeep/Setup Task 03 (Economy HUD)")]
        public static void SetupScene()
        {
            var bootstrap = Object.FindFirstObjectByType<GameSessionBootstrap>();
            if (bootstrap == null)
            {
                Debug.LogError("[Task03SceneSetup] No GameSessionBootstrap in scene. Run the Task 01/02 setups first.");
                return;
            }

            var canvas = Object.FindFirstObjectByType<Canvas>();
            if (canvas == null)
            {
                Debug.LogError("[Task03SceneSetup] No Canvas in scene. Run 'Wavekeep/Setup Task 01 Scene' first.");
                return;
            }

            if (TMP_Settings.defaultFontAsset == null)
            {
                Debug.LogWarning("[Task03SceneSetup] TMP has no default font asset. If the text doesn't render, " +
                                 "import it via Window > TextMeshPro > Import TMP Essential Resources, then re-run.");
            }

            // Idempotent re-run.
            DestroyIfExists("CurrencyText");
            DestroyIfExists("LevelXpText");
            DestroyIfExists("WaveText");
            DestroyIfExists("EconomyDebugHud");

            var currencyText = CreateText(canvas.transform, "CurrencyText", "Currency: 0", new Vector2(20f, -20f));
            var levelXpText = CreateText(canvas.transform, "LevelXpText", "Lv. 1 — 0/15 XP", new Vector2(20f, -64f));
            // Task 41: always-on wave readout, stacked directly under XP/Currency in the same top-left group.
            var waveText = CreateText(canvas.transform, "WaveText", "Wave: —", new Vector2(20f, -108f));

            // Task 41: the wave label reads the live wave from the scene's WaveSpawner.
            var waveSpawner = Object.FindFirstObjectByType<WaveSpawner>();

            var hudGo = new GameObject("EconomyDebugHud", typeof(RectTransform));
            hudGo.transform.SetParent(canvas.transform, false);
            var hud = hudGo.AddComponent<EconomyDebugHud>();

            var so = new SerializedObject(hud);
            so.FindProperty("_bootstrap").objectReferenceValue = bootstrap;
            so.FindProperty("_currencyText").objectReferenceValue = currencyText;
            so.FindProperty("_levelXpText").objectReferenceValue = levelXpText;
            so.FindProperty("_waveText").objectReferenceValue = waveText;
            so.FindProperty("_waveSpawner").objectReferenceValue = waveSpawner;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log("[Task03SceneSetup] Economy HUD added. Press Play and kill enemies (K) to see currency/XP update. Save the scene (Ctrl+S) to persist it.");
        }

        private static TextMeshProUGUI CreateText(Transform parent, string name, string initial, Vector2 anchoredPosition)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = initial;
            tmp.fontSize = 28f;
            tmp.color = Color.white;
            if (TMP_Settings.defaultFontAsset != null)
            {
                tmp.font = TMP_Settings.defaultFontAsset;
            }

            // Anchor to the top-left corner of the canvas.
            var rt = tmp.rectTransform;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = anchoredPosition;
            rt.sizeDelta = new Vector2(600f, 40f);

            return tmp;
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
