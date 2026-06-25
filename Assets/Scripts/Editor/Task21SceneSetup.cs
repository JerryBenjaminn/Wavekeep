#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Wavekeep.UI;

namespace Wavekeep.EditorTools
{
    /// <summary>
    /// One-shot editor utility that adds the Task 21 ultimate charge bar to the existing Canvas: a
    /// bottom-centre background, a horizontally-filled bar driven by the ultimate's cooldown progress,
    /// and a label that flips to a "ready" state. Wires a <see cref="UltimateChargeBar"/> to those
    /// elements; the bar finds the runtime-spawned hero itself.
    ///
    /// Built in code for the same reason as the other task setups (scenes aren't hand-authored as YAML).
    /// Run "Wavekeep/Setup Task 21 (Ultimate Charge Bar)" after the earlier setups, then save the scene.
    /// Editor-only; not part of the runtime build.
    /// </summary>
    public static class Task21SceneSetup
    {
        private const string RootName = "UltimateChargeBar";

        [MenuItem("Wavekeep/Setup Task 21 (Ultimate Charge Bar)")]
        public static void SetupScene()
        {
            var canvas = Object.FindFirstObjectByType<Canvas>();
            if (canvas == null)
            {
                Debug.LogError("[Task21SceneSetup] No Canvas in scene. Run 'Wavekeep/Setup Task 01 Scene' first.");
                return;
            }

            if (TMP_Settings.defaultFontAsset == null)
            {
                Debug.LogWarning("[Task21SceneSetup] TMP has no default font asset. If the label doesn't render, " +
                                 "import it via Window > TextMeshPro > Import TMP Essential Resources, then re-run.");
            }

            DestroyIfExists(RootName); // idempotent re-run

            var uiSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");

            // Task 36: UltimateChargeBar is now multi-bar (one generated bar per active hero), so the root is
            // a bottom-centre VERTICAL CONTAINER the component fills at runtime — not a single fixed bar.
            var rootGo = new GameObject(RootName,
                typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            rootGo.transform.SetParent(canvas.transform, false);
            var rootRt = (RectTransform)rootGo.transform;
            rootRt.anchorMin = new Vector2(0.5f, 0f);
            rootRt.anchorMax = new Vector2(0.5f, 0f);
            rootRt.pivot = new Vector2(0.5f, 0f);
            rootRt.sizeDelta = new Vector2(420f, 0f);
            rootRt.anchoredPosition = new Vector2(0f, 12f);

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
            so.FindProperty("_container").objectReferenceValue = rootRt;
            so.FindProperty("_barSprite").objectReferenceValue = uiSprite;
            // Wire the session so the bar reads the hero registry (falls back to a scene scan if left null).
            var bootstrap = Object.FindFirstObjectByType<Wavekeep.Core.GameSessionBootstrap>();
            if (bootstrap != null) so.FindProperty("_bootstrap").objectReferenceValue = bootstrap;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log("[Task21SceneSetup] Ultimate charge bar added (now one bar per active hero, Task 36). " +
                      "For the full dual-hero wiring run 'Setup Task 36' too. Save the scene (Ctrl+S).");
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
