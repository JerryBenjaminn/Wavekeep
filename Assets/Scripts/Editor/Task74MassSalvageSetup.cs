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
    /// Task 74 — adds the mass-salvage UI (rarity quick-filter row, live selection summary, "Mass Salvage" +
    /// "Clear" buttons) to the bottom of the Hub inventory column, and wires the matching <see cref="HubController"/>
    /// fields. The inventory list rows already gain a selection checkbox in code (HubController.BuildInventoryRow);
    /// this only builds the surrounding chrome + reflows the inventory scroll to make room.
    ///
    /// Additive + idempotent (like Task 73): re-running rebuilds the bar in place, and it's chained into
    /// <see cref="Task14SceneSetup"/> (after the Task 73 economy UI) so a base-Hub rebuild never drops it. No
    /// backend logic touched — mass salvage loops the existing <c>GearManager.Salvage(instanceId)</c> per item and
    /// reuses the Task 73 confirm modal.
    ///
    /// Run "Wavekeep/Setup Task 74 (Mass Salvage)" AFTER Task 14/73. Editor-only.
    /// </summary>
    public static class Task74MassSalvageSetup
    {
        private const string HubScenePath = "Assets/Scenes/Hub.unity";

        [MenuItem("Wavekeep/Setup Task 74 (Mass Salvage)")]
        public static void SetupScene()
        {
            if (!System.IO.File.Exists(HubScenePath))
            {
                Debug.LogError("[Task74] Hub scene not found at " + HubScenePath + ". Run 'Setup Task 14 (Hub Scene)' first.");
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(HubScenePath, OpenSceneMode.Single);

            var hub = Object.FindFirstObjectByType<HubController>();
            var canvas = Object.FindFirstObjectByType<Canvas>();
            if (!BuildAndWire(hub, canvas)) return;

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            Debug.Log("[Task74] Mass-salvage UI added + wired. Press Play from the Hub: tick inventory items or a " +
                      "rarity quick-filter, then Mass Salvage to confirm the batch.");
        }

        /// <summary>Build/rebuild the Task 74 mass-salvage bar on the CURRENTLY-OPEN Hub scene and wire it
        /// (idempotent). Shared by the menu and the Task 14 rebuild chain. Returns false (logged) if the Hub layout
        /// isn't present.</summary>
        public static bool BuildAndWire(HubController hub, Canvas canvas)
        {
            if (hub == null || canvas == null)
            {
                Debug.LogError("[Task74] HubController or Canvas missing. Re-run 'Setup Task 14 (Hub Scene)'.");
                return false;
            }
            var hubRoot = canvas.transform.Find("HubRoot") as RectTransform;
            var right = hubRoot != null ? hubRoot.Find("RightColumn") as RectTransform : null;
            if (right == null)
            {
                Debug.LogError("[Task74] 'HubRoot/RightColumn' not found; Hub layout changed. Re-run 'Setup Task 14'.");
                return false;
            }

            // Make room at the bottom of the inventory column for the mass-salvage bar (idempotent: a fixed offset,
            // not a relative shrink, so re-running doesn't keep eating into the list).
            var invScroll = right.Find("InventoryScroll") as RectTransform;
            if (invScroll != null) invScroll.offsetMin = new Vector2(0f, 150f);

            // Idempotency: remove anything from a previous run.
            DestroyChild(right, "RarityFilterBar");
            DestroyChild(right, "SelectionLabel");
            DestroyChild(right, "Button_MassSalvage");
            DestroyChild(right, "Button_ClearSelection");

            // Rarity quick-filter row (HubController fills it with one button per tier at runtime).
            var filterBar = new GameObject("RarityFilterBar", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            filterBar.transform.SetParent(right, false);
            var fbr = (RectTransform)filterBar.transform;
            fbr.anchorMin = new Vector2(0f, 0f); fbr.anchorMax = new Vector2(1f, 0f); fbr.pivot = new Vector2(0.5f, 0f);
            fbr.anchoredPosition = new Vector2(0f, 104f); fbr.sizeDelta = new Vector2(0f, 40f);
            var hlg = filterBar.GetComponent<HorizontalLayoutGroup>();
            hlg.spacing = 6f; hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.childControlWidth = false; hlg.childControlHeight = false;
            hlg.childForceExpandWidth = false; hlg.childForceExpandHeight = false;

            // Live selection summary.
            var selLabel = CreateText(right, "No items selected for salvage", 18f, TextAlignmentOptions.Center);
            selLabel.gameObject.name = "SelectionLabel";
            var slr = selLabel.rectTransform;
            slr.anchorMin = new Vector2(0f, 0f); slr.anchorMax = new Vector2(1f, 0f); slr.pivot = new Vector2(0.5f, 0f);
            slr.anchoredPosition = new Vector2(0f, 64f); slr.sizeDelta = new Vector2(0f, 28f);

            // Mass Salvage (left) + Clear (right) buttons.
            var massBtn = CreateButton(right, "Mass Salvage", new Color(0.5f, 0.4f, 0.25f), Color.white, new Vector2(220f, 48f));
            massBtn.gameObject.name = "Button_MassSalvage";
            var mbr = (RectTransform)massBtn.transform;
            mbr.anchorMin = new Vector2(0f, 0f); mbr.anchorMax = new Vector2(0f, 0f); mbr.pivot = new Vector2(0f, 0f);
            mbr.anchoredPosition = new Vector2(0f, 8f);

            var clearBtn = CreateButton(right, "Clear", new Color(0.4f, 0.3f, 0.3f), Color.white, new Vector2(140f, 48f));
            clearBtn.gameObject.name = "Button_ClearSelection";
            var cbr = (RectTransform)clearBtn.transform;
            cbr.anchorMin = new Vector2(1f, 0f); cbr.anchorMax = new Vector2(1f, 0f); cbr.pivot = new Vector2(1f, 0f);
            cbr.anchoredPosition = new Vector2(0f, 8f);

            // Wire HubController.
            var so = new SerializedObject(hub);
            so.FindProperty("_rarityFilterContainer").objectReferenceValue = fbr;
            so.FindProperty("_selectionLabel").objectReferenceValue = selLabel;
            so.FindProperty("_massSalvageButton").objectReferenceValue = massBtn;
            so.FindProperty("_clearSelectionButton").objectReferenceValue = clearBtn;
            so.ApplyModifiedPropertiesWithoutUndo();

            return true;
        }

        // --- UI helpers (local copies, matching Task 14/25/73 style) -------------------------------

        private static TextMeshProUGUI CreateText(Transform parent, string text, float fontSize, TextAlignmentOptions alignment)
        {
            var go = new GameObject("Text", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text; tmp.fontSize = fontSize; tmp.color = Color.white; tmp.alignment = alignment; tmp.richText = true;
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
            tmp.text = label; tmp.fontSize = 20f; tmp.color = fg; tmp.alignment = TextAlignmentOptions.Center; tmp.richText = true;
            if (TMP_Settings.defaultFontAsset != null) tmp.font = TMP_Settings.defaultFontAsset;
            var lrt = tmp.rectTransform;
            lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one; lrt.offsetMin = Vector2.zero; lrt.offsetMax = Vector2.zero;
            return go.GetComponent<Button>();
        }

        private static void DestroyChild(Transform parent, string name)
        {
            var existing = parent.Find(name);
            if (existing != null) Object.DestroyImmediate(existing.gameObject);
        }
    }
}
#endif
