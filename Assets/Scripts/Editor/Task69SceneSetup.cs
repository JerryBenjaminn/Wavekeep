#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Wavekeep.Core;
using Wavekeep.Runtime;
using Wavekeep.UI;

namespace Wavekeep.EditorTools
{
    /// <summary>
    /// Task 69 — visual loot drops (gear redesign part 3, purely presentational):
    /// <list type="bullet">
    /// <item>Removes the old <c>LootDropHud</c> text-toast objects (the class itself is deleted).</item>
    /// <item>Adds a <see cref="LootDropVfxController"/> to the scene (rarity-coloured arena drop markers, hooked to
    /// the existing <c>GearDroppedEvent</c>).</item>
    /// <item>Adds a <see cref="RunLootSummary"/> side panel to the run-end flow (additive — it does NOT touch the
    /// existing <c>RunEndScreen</c> victory/defeat panel), listing every instance that dropped that run.</item>
    /// </list>
    /// No gear generation/grant logic is touched. Run "Wavekeep/Setup Task 69 (Visual Loot Drops)" from the
    /// gameplay scene after the Task 08/13 setups, then save the scene. Idempotent. Editor-only.
    /// </summary>
    public static class Task69SceneSetup
    {
        [MenuItem("Wavekeep/Setup Task 69 (Visual Loot Drops)")]
        public static void SetupScene()
        {
            var bootstrap = Object.FindFirstObjectByType<GameSessionBootstrap>();
            var canvas = Object.FindFirstObjectByType<Canvas>();
            if (bootstrap == null || canvas == null)
            {
                Debug.LogError("[Task69SceneSetup] Missing GameSessionBootstrap / Canvas. Run the Task 01–13 setups first.");
                return;
            }

            // 1) Purge the old text toast (replaced by this task).
            DestroyIfExists("LootDropText");
            DestroyIfExists("LootDropHud");

            // 2) Arena visual drop controller (idempotent).
            DestroyIfExists("LootDropVfx");
            var vfxGo = new GameObject("LootDropVfx", typeof(LootDropVfxController));
            var vfx = vfxGo.GetComponent<LootDropVfxController>();
            var vso = new SerializedObject(vfx);
            vso.FindProperty("_bootstrap").objectReferenceValue = bootstrap;
            vso.ApplyModifiedPropertiesWithoutUndo();

            // 3) End-of-run loot summary side panel (additive to RunEndScreen).
            DestroyIfExists("RunLootSummaryPanel");
            DestroyIfExists("RunLootSummary");
            BuildSummaryPanel(canvas, bootstrap);

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log("[Task69SceneSetup] Visual loot drops wired: arena markers + end-of-run summary; text toast " +
                      "removed. Play: kill grunts → coloured beam markers; on run end → loot summary panel. Save (Ctrl+S).");
        }

        private static void BuildSummaryPanel(Canvas canvas, GameSessionBootstrap bootstrap)
        {
            if (TMP_Settings.defaultFontAsset == null)
            {
                Debug.LogWarning("[Task69SceneSetup] TMP has no default font asset. If the summary text doesn't " +
                                 "render, import via Window > TextMeshPro > Import TMP Essential Resources, then re-run.");
            }

            // Right-side panel so it sits ON TOP of the full-screen RunEndPanel without covering its centred
            // victory/defeat title+stats. Created after RunEndPanel (and forced last sibling) so it draws above it.
            var panel = new GameObject("RunLootSummaryPanel", typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(canvas.transform, false);
            panel.transform.SetAsLastSibling();
            var prt = (RectTransform)panel.transform;
            prt.anchorMin = new Vector2(1f, 0.5f);
            prt.anchorMax = new Vector2(1f, 0.5f);
            prt.pivot = new Vector2(1f, 0.5f);
            prt.anchoredPosition = new Vector2(-24f, 0f);
            prt.sizeDelta = new Vector2(440f, 620f);
            panel.GetComponent<Image>().color = new Color(0.05f, 0.05f, 0.07f, 0.92f);

            var title = CreateText(panel.transform, "Title", "Loot This Run", 30f, TextAlignmentOptions.Center,
                new Vector2(0.5f, 1f), new Vector2(0f, -16f), new Vector2(-24f, 48f), stretchWidth: true);
            title.color = new Color(1f, 0.92f, 0.6f);

            var list = CreateText(panel.transform, "List", "", 20f, TextAlignmentOptions.TopLeft,
                new Vector2(0.5f, 1f), new Vector2(0f, -76f), new Vector2(-32f, 520f), stretchWidth: true);
            list.overflowMode = TextOverflowModes.Truncate; // word-wrap is on by default; keep long lists contained

            // Controller on a separate root so hiding the panel never disables it.
            var ctrlGo = new GameObject("RunLootSummary", typeof(RunLootSummary));
            var ctrl = ctrlGo.GetComponent<RunLootSummary>();
            var so = new SerializedObject(ctrl);
            so.FindProperty("_bootstrap").objectReferenceValue = bootstrap;
            so.FindProperty("_panel").objectReferenceValue = panel;
            so.FindProperty("_titleText").objectReferenceValue = title;
            so.FindProperty("_listText").objectReferenceValue = list;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static TextMeshProUGUI CreateText(Transform parent, string name, string text, float fontSize,
            TextAlignmentOptions alignment, Vector2 anchor, Vector2 anchoredPosition, Vector2 size, bool stretchWidth)
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
            if (stretchWidth)
            {
                // Anchor to the top, stretching horizontally within the panel; size.x is the horizontal inset.
                rt.anchorMin = new Vector2(0f, 1f);
                rt.anchorMax = new Vector2(1f, 1f);
                rt.pivot = new Vector2(0.5f, 1f);
                rt.offsetMin = new Vector2(16f, anchoredPosition.y - size.y);
                rt.offsetMax = new Vector2(-16f, anchoredPosition.y);
            }
            else
            {
                rt.anchorMin = anchor;
                rt.anchorMax = anchor;
                rt.pivot = anchor;
                rt.anchoredPosition = anchoredPosition;
                rt.sizeDelta = size;
            }
            return tmp;
        }

        private static void DestroyIfExists(string objectName)
        {
            var existing = GameObject.Find(objectName);
            if (existing != null) Object.DestroyImmediate(existing);
        }
    }
}
#endif
