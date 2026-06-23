#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Wavekeep.Core;
using Wavekeep.UI;

namespace Wavekeep.EditorTools
{
    /// <summary>
    /// One-shot editor utility that adds the Task 22 in-game stat panel to the existing Canvas: a
    /// top-left dark panel with a single TMP readout, toggled with Tab, wired to a
    /// <see cref="StatPanelController"/> and the scene's <see cref="GameSessionBootstrap"/>. The panel
    /// finds the runtime-spawned hero itself.
    ///
    /// Built in code like the other task setups (scenes aren't hand-authored as YAML). Run
    /// "Wavekeep/Setup Task 22 (Stat Panel)" after the earlier setups, then save the scene. Editor-only.
    /// </summary>
    public static class Task22SceneSetup
    {
        private const string RootName = "StatPanel";

        [MenuItem("Wavekeep/Setup Task 22 (Stat Panel)")]
        public static void SetupScene()
        {
            var bootstrap = Object.FindFirstObjectByType<GameSessionBootstrap>();
            if (bootstrap == null)
            {
                Debug.LogError("[Task22SceneSetup] No GameSessionBootstrap in scene. Run the Task 01/02 setups first.");
                return;
            }

            var canvas = Object.FindFirstObjectByType<Canvas>();
            if (canvas == null)
            {
                Debug.LogError("[Task22SceneSetup] No Canvas in scene. Run 'Wavekeep/Setup Task 01 Scene' first.");
                return;
            }

            if (TMP_Settings.defaultFontAsset == null)
            {
                Debug.LogWarning("[Task22SceneSetup] TMP has no default font asset. If text doesn't render, " +
                                 "import it via Window > TextMeshPro > Import TMP Essential Resources, then re-run.");
            }

            DestroyIfExists(RootName); // idempotent re-run

            // Root: an always-active holder for the controller (its Update reads the toggle key).
            var rootGo = new GameObject(RootName, typeof(RectTransform));
            rootGo.transform.SetParent(canvas.transform, false);

            // Panel: the toggled, visible container (dark translucent), anchored top-left below the HUD.
            var panelGo = new GameObject("Panel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            panelGo.transform.SetParent(rootGo.transform, false);
            var panelRt = (RectTransform)panelGo.transform;
            panelRt.anchorMin = new Vector2(0f, 1f);
            panelRt.anchorMax = new Vector2(0f, 1f);
            panelRt.pivot = new Vector2(0f, 1f);
            panelRt.sizeDelta = new Vector2(470f, 660f);
            panelRt.anchoredPosition = new Vector2(16f, -110f);
            panelGo.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.78f);

            // Content text: padded, top-left aligned, wraps.
            var textGo = new GameObject("Content", typeof(RectTransform));
            textGo.transform.SetParent(panelGo.transform, false);
            var text = textGo.AddComponent<TextMeshProUGUI>();
            text.text = "RUN STATS";
            text.fontSize = 18f;
            text.color = Color.white;
            text.alignment = TextAlignmentOptions.TopLeft;
            if (TMP_Settings.defaultFontAsset != null) text.font = TMP_Settings.defaultFontAsset;
            var textRt = text.rectTransform;
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = new Vector2(14f, 14f);
            textRt.offsetMax = new Vector2(-14f, -14f);

            var controller = rootGo.AddComponent<StatPanelController>();
            var so = new SerializedObject(controller);
            so.FindProperty("_bootstrap").objectReferenceValue = bootstrap;
            so.FindProperty("_panel").objectReferenceValue = panelGo;
            so.FindProperty("_contentText").objectReferenceValue = text;
            so.ApplyModifiedPropertiesWithoutUndo();

            panelGo.SetActive(false); // hidden until toggled

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log("[Task22SceneSetup] Stat panel added. Play, then press Tab to open/close it. " +
                      "Save the scene (Ctrl+S) to persist it.");
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
