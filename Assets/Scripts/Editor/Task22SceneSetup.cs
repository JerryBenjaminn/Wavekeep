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

            // Root: an always-active holder for the controller (its Update reads the toggle key). It must
            // STRETCH to fill the Canvas (Task 40 fix): the panel below now uses vertical-stretch anchoring,
            // whose resolved height is relative to THIS parent. A default RectTransform is zero-sized, so the
            // panel would resolve to height ≈ −120 (invisible — "won't open"). Filling the canvas gives the
            // panel the full screen height to stretch against. (The old fixed-size panel didn't depend on this.)
            var rootGo = new GameObject(RootName, typeof(RectTransform));
            rootGo.transform.SetParent(canvas.transform, false);
            var rootRt = (RectTransform)rootGo.transform;
            rootRt.anchorMin = Vector2.zero;
            rootRt.anchorMax = Vector2.one;
            rootRt.offsetMin = Vector2.zero;
            rootRt.offsetMax = Vector2.zero;

            // Task 40: the toggled dark panel now sits on the RIGHT edge, vertically centred and stretched to
            // (screen height − margins), instead of a fixed top-left box. Right-anchored with vertical stretch
            // so it scales with any landscape resolution (§3.6) and never collides with the bottom-centre HUD
            // (ultimate bars / apex indicators) on the left/centre.
            var panelGo = new GameObject("Panel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            panelGo.transform.SetParent(rootGo.transform, false);
            var panelRt = (RectTransform)panelGo.transform;
            panelRt.anchorMin = new Vector2(1f, 0f);      // right edge, vertical stretch
            panelRt.anchorMax = new Vector2(1f, 1f);
            panelRt.pivot = new Vector2(1f, 0.5f);
            panelRt.sizeDelta = new Vector2(440f, -120f); // 440 wide; height = screen height − 120 (60 top/bottom)
            panelRt.anchoredPosition = new Vector2(-16f, 0f); // 16px gap from the right edge, centred vertically
            panelGo.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.78f);

            // Task 40: a scroll view fills the panel so the (now two heroes' worth of) stats can never clip —
            // content taller than the panel scrolls instead of running off-screen. Mirrors the Hub's list pattern.
            var scrollGo = new GameObject("Scroll", typeof(RectTransform), typeof(ScrollRect), typeof(Image), typeof(RectMask2D));
            scrollGo.transform.SetParent(panelGo.transform, false);
            var scrollRt = (RectTransform)scrollGo.transform;
            scrollRt.anchorMin = Vector2.zero;
            scrollRt.anchorMax = Vector2.one;
            scrollRt.offsetMin = new Vector2(6f, 6f);
            scrollRt.offsetMax = new Vector2(-6f, -6f);
            scrollGo.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0f); // transparent viewport (panel is the dark bg)

            // Content: a vertical layout + size fitter that grows with the text, so the scroll range tracks it.
            var contentGo = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            contentGo.transform.SetParent(scrollGo.transform, false);
            var contentRt = (RectTransform)contentGo.transform;
            contentRt.anchorMin = new Vector2(0f, 1f);
            contentRt.anchorMax = new Vector2(1f, 1f);
            contentRt.pivot = new Vector2(0.5f, 1f);
            contentRt.anchoredPosition = Vector2.zero;
            contentRt.sizeDelta = Vector2.zero;
            var vlg = contentGo.GetComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(14, 14, 14, 14);
            vlg.childAlignment = TextAnchor.UpperLeft;
            vlg.childControlWidth = true; vlg.childControlHeight = true;        // size the TMP to its preferred height
            vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;
            contentGo.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // Content text: top-left aligned, wraps to the content width; height driven by the layout above.
            var textGo = new GameObject("Text", typeof(RectTransform));
            textGo.transform.SetParent(contentGo.transform, false);
            var text = textGo.AddComponent<TextMeshProUGUI>();
            text.text = "RUN STATS";
            text.fontSize = 16f; // slightly smaller so two heroes' blocks read densely within 440px width
            text.color = Color.white;
            text.alignment = TextAlignmentOptions.TopLeft; // TMP wraps to the content width by default
            if (TMP_Settings.defaultFontAsset != null) text.font = TMP_Settings.defaultFontAsset;

            var scroll = scrollGo.GetComponent<ScrollRect>();
            scroll.content = contentRt;
            scroll.viewport = scrollRt;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 24f;

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
