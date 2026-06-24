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
    /// Adds the Task 25 Gear Detail Panel to the existing Task 14 Hub scene (scenes are built by editor
    /// scripts, never hand-authored YAML). It opens Assets/Scenes/Hub.unity, docks a detail panel to the
    /// right of the inventory column (shrinking that column to make room), and wires the panel's icon,
    /// name, rarity, stat container, and Equip/Unequip buttons into the existing <see cref="HubController"/>.
    ///
    /// HubController fills the stat container at runtime (one row per StatModifier + luckBonus, each with a
    /// hover tooltip) and routes Equip/Unequip through the existing Task 12 GearManager flow. The panel is
    /// hidden by HubController on Start, so it is closed by default.
    ///
    /// Run "Wavekeep/Setup Task 25 (Gear Detail Panel)" AFTER the Task 14 setup. Idempotent — re-running
    /// rebuilds the panel in place. Editor-only.
    /// </summary>
    public static class Task25SceneSetup
    {
        private const string HubScenePath = "Assets/Scenes/Hub.unity";
        private const float PanelWidth = 460f;

        [MenuItem("Wavekeep/Setup Task 25 (Gear Detail Panel)")]
        public static void SetupScene()
        {
            if (!System.IO.File.Exists(HubScenePath))
            {
                Debug.LogError("[Task25SceneSetup] Hub scene not found at " + HubScenePath +
                               ". Run 'Wavekeep/Setup Task 14 (Hub Scene)' first.");
                return;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(HubScenePath, OpenSceneMode.Single);

            var hub = Object.FindFirstObjectByType<HubController>();
            var canvas = Object.FindFirstObjectByType<Canvas>();
            if (hub == null || canvas == null)
            {
                Debug.LogError("[Task25SceneSetup] HubController or Canvas missing in the Hub scene. " +
                               "Re-run 'Wavekeep/Setup Task 14 (Hub Scene)'.");
                return;
            }

            var hubRoot = canvas.transform.Find("HubRoot") as RectTransform;
            if (hubRoot == null)
            {
                Debug.LogError("[Task25SceneSetup] 'HubRoot' not found under the Canvas; Hub scene layout changed.");
                return;
            }

            // Make room: pull the inventory column's right edge in by the panel width + a small gap.
            var rightColumn = hubRoot.Find("RightColumn") as RectTransform;
            if (rightColumn != null)
            {
                var max = rightColumn.offsetMax;
                rightColumn.offsetMax = new Vector2(-(40f + PanelWidth + 20f), max.y);
            }
            else
            {
                Debug.LogWarning("[Task25SceneSetup] 'RightColumn' not found; the detail panel may overlap the inventory.");
            }

            // Idempotency: remove any panel from a previous run before rebuilding.
            var existing = hubRoot.Find("DetailPanel");
            if (existing != null) Object.DestroyImmediate(existing.gameObject);

            BuildDetailPanel(hubRoot, hub);

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            Debug.Log("[Task25SceneSetup] Gear detail panel added + wired into HubController and saved. Press " +
                      "Play from the Hub: click an inventory item or an equipped slot to inspect it, hover a stat " +
                      "row for its tooltip, and use Equip/Unequip. Click the same item again to close the panel.");
        }

        private static void BuildDetailPanel(RectTransform hubRoot, HubController hub)
        {
            // Panel docked to the right edge of HubRoot, matching the inventory column's vertical insets.
            var panel = NewRect("DetailPanel", hubRoot, new Vector2(1f, 0f), new Vector2(1f, 1f),
                new Vector2(-(40f + PanelWidth), 110f), new Vector2(-40f, -110f));
            panel.gameObject.AddComponent<Image>().color = new Color(0.13f, 0.14f, 0.18f, 1f);

            CreateColumnHeader(panel, "ITEM DETAILS");

            // Icon placeholder (sprite bound at runtime; tinted box until real art exists).
            var iconRect = NewRect("Icon", panel, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                Vector2.zero, Vector2.zero);
            iconRect.pivot = new Vector2(0.5f, 1f);
            iconRect.anchoredPosition = new Vector2(0f, -52f);
            iconRect.sizeDelta = new Vector2(96f, 96f);
            var icon = iconRect.gameObject.AddComponent<Image>();
            icon.color = new Color(0.30f, 0.30f, 0.38f, 1f);

            var name = CreateText(panel, "Item Name", 26f, TextAlignmentOptions.Center);
            AnchorTop(name.rectTransform, -160f, 32f);

            var rarity = CreateText(panel, "[Rarity]", 20f, TextAlignmentOptions.Center);
            AnchorTop(rarity.rectTransform, -196f, 28f);

            var statsHeader = CreateText(panel, "STATS", 20f, TextAlignmentOptions.Left);
            AnchorTop(statsHeader.rectTransform, -232f, 26f);
            statsHeader.rectTransform.offsetMin = new Vector2(16f, statsHeader.rectTransform.offsetMin.y);

            // Stat list (scrollable) between the header and the action buttons.
            var statContent = CreateScrollView("StatScroll", panel,
                new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(16f, 70f), new Vector2(-16f, -264f));

            // Action buttons along the bottom.
            var equip = CreateButton(panel, "Equip", new Color(0.2f, 0.55f, 0.3f), Color.white, new Vector2(200f, 44f));
            var ert = (RectTransform)equip.transform;
            ert.anchorMin = new Vector2(0f, 0f); ert.anchorMax = new Vector2(0f, 0f); ert.pivot = new Vector2(0f, 0f);
            ert.anchoredPosition = new Vector2(16f, 14f);

            var unequip = CreateButton(panel, "Unequip", new Color(0.55f, 0.3f, 0.3f), Color.white, new Vector2(200f, 44f));
            var urt = (RectTransform)unequip.transform;
            urt.anchorMin = new Vector2(1f, 0f); urt.anchorMax = new Vector2(1f, 0f); urt.pivot = new Vector2(1f, 0f);
            urt.anchoredPosition = new Vector2(-16f, 14f);

            // Wire into HubController's serialized fields.
            var so = new SerializedObject(hub);
            so.FindProperty("_detailPanel").objectReferenceValue = panel.gameObject;
            so.FindProperty("_detailIcon").objectReferenceValue = icon;
            so.FindProperty("_detailNameText").objectReferenceValue = name;
            so.FindProperty("_detailRarityText").objectReferenceValue = rarity;
            so.FindProperty("_detailStatContainer").objectReferenceValue = statContent;
            so.FindProperty("_detailEquipButton").objectReferenceValue = equip;
            so.FindProperty("_detailUnequipButton").objectReferenceValue = unequip;
            so.ApplyModifiedPropertiesWithoutUndo();

            VerifyWiring(hub);
        }

        private static void VerifyWiring(HubController hub)
        {
            var so = new SerializedObject(hub);
            string[] fields =
            {
                "_detailPanel", "_detailIcon", "_detailNameText", "_detailRarityText",
                "_detailStatContainer", "_detailEquipButton", "_detailUnequipButton"
            };
            foreach (var f in fields)
            {
                if (so.FindProperty(f).objectReferenceValue == null)
                {
                    Debug.LogError($"[Task25SceneSetup] HubController.{f} wired NULL — re-run the setup.");
                    return;
                }
            }
            Debug.Log("[Task25SceneSetup] Wiring verified: all 7 detail-panel references referenced.");
        }

        // --- UI helpers (local copies; Task14's are private) ---------------------------------------

        private static void CreateColumnHeader(RectTransform parent, string text)
        {
            var t = CreateText(parent, text, 26f, TextAlignmentOptions.Left);
            AnchorTop(t.rectTransform, -16f, 36f);
            t.rectTransform.offsetMin = new Vector2(16f, t.rectTransform.offsetMin.y);
        }

        private static void AnchorTop(RectTransform rt, float anchoredY, float height)
        {
            rt.anchorMin = new Vector2(0f, 1f); rt.anchorMax = new Vector2(1f, 1f); rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, anchoredY); rt.sizeDelta = new Vector2(0f, height);
        }

        private static RectTransform CreateScrollView(string name, RectTransform parent,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            var scrollGo = new GameObject(name, typeof(RectTransform), typeof(ScrollRect), typeof(Image), typeof(RectMask2D));
            scrollGo.transform.SetParent(parent, false);
            var srt = (RectTransform)scrollGo.transform;
            srt.anchorMin = anchorMin; srt.anchorMax = anchorMax; srt.offsetMin = offsetMin; srt.offsetMax = offsetMax;
            scrollGo.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.25f);

            var content = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            content.transform.SetParent(scrollGo.transform, false);
            var crt = (RectTransform)content.transform;
            crt.anchorMin = new Vector2(0f, 1f); crt.anchorMax = new Vector2(1f, 1f); crt.pivot = new Vector2(0f, 1f);
            crt.anchoredPosition = Vector2.zero; crt.sizeDelta = Vector2.zero;
            var vlg = content.GetComponent<VerticalLayoutGroup>();
            vlg.spacing = 4f;
            vlg.childAlignment = TextAnchor.UpperLeft;
            vlg.childControlWidth = false; vlg.childControlHeight = false;
            vlg.childForceExpandWidth = false; vlg.childForceExpandHeight = false;
            vlg.padding = new RectOffset(6, 6, 6, 6);
            content.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var scroll = scrollGo.GetComponent<ScrollRect>();
            scroll.content = crt; scroll.viewport = srt;
            scroll.horizontal = false; scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            return crt;
        }

        private static RectTransform NewRect(string name, Transform parent,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = anchorMin; rt.anchorMax = anchorMax; rt.offsetMin = offsetMin; rt.offsetMax = offsetMax;
            return rt;
        }

        private static TextMeshProUGUI CreateText(Transform parent, string text, float fontSize, TextAlignmentOptions alignment)
        {
            var go = new GameObject("Text", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.color = Color.white;
            tmp.alignment = alignment;
            tmp.richText = true;
            if (TMP_Settings.defaultFontAsset != null) tmp.font = TMP_Settings.defaultFontAsset;
            ((RectTransform)tmp.transform).sizeDelta = new Vector2(400f, fontSize + 8f);
            return tmp;
        }

        private static Button CreateButton(Transform parent, string label, Color bg, Color fg, Vector2 size)
        {
            var go = new GameObject($"Button_{label}", typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            ((RectTransform)go.transform).sizeDelta = size;
            go.GetComponent<Image>().color = bg;

            var labelGo = new GameObject("Label", typeof(RectTransform));
            labelGo.transform.SetParent(go.transform, false);
            var tmp = labelGo.AddComponent<TextMeshProUGUI>();
            tmp.text = label;
            tmp.fontSize = 20f;
            tmp.color = fg;
            tmp.alignment = TextAlignmentOptions.Center;
            if (TMP_Settings.defaultFontAsset != null) tmp.font = TMP_Settings.defaultFontAsset;
            var lrt = tmp.rectTransform;
            lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one; lrt.offsetMin = Vector2.zero; lrt.offsetMax = Vector2.zero;
            return go.GetComponent<Button>();
        }
    }
}
#endif
