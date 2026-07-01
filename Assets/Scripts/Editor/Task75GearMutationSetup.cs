#if UNITY_EDITOR
using TMPro;
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
    /// Task 75 — gear redesign part 6: the reroll-affix + upgrade-rarity Dust sinks. Authors the two new cost
    /// arrays on the existing <see cref="GearEconomyConfigSO"/> (Task 71 asset), adds a "Modify" button to the
    /// Task 25 detail panel, and builds the Modify modal (per-affix Reroll rows + a single Upgrade-Rarity action).
    /// Wires the new <see cref="HubController"/> fields and re-ensures the Hub bootstrap's economy config is wired.
    ///
    /// Additive + idempotent, like Tasks 73/74: re-running rebuilds the elements in place, and it's chained into
    /// <see cref="Task14SceneSetup"/> (after Task 73/74) so a base-Hub rebuild never drops it. No backend logic is
    /// touched — everything routes through the new <c>GearManager.RerollAffix</c>/<c>UpgradeRarity</c> APIs and the
    /// shared Task 73 confirm modal.
    ///
    /// Run "Wavekeep/Setup Task 75 (Reroll + Upgrade Rarity)" AFTER Task 14/25/71/73. Editor-only.
    /// </summary>
    public static class Task75GearMutationSetup
    {
        private const string HubScenePath = "Assets/Scenes/Hub.unity";
        private const string EconomyPath = "Assets/Data/Gear/GearEconomyConfig.asset";

        // Placeholder tuning (flagged for balance): reroll is cheap per current rarity; upgrade is cheaper than
        // forging fresh at the resulting tier. Both indexed by the item's CURRENT rarity (Common..Unique).
        // Unique = 0 (not rerollable / Forge-only); Legendary upgrade = 0 (cap).
        private static readonly int[] RerollCost = { 3, 6, 15, 35, 80, 0 };
        private static readonly int[] UpgradeCost = { 15, 40, 90, 200, 0, 0 };

        [MenuItem("Wavekeep/Setup Task 75 (Reroll + Upgrade Rarity)")]
        public static void SetupScene()
        {
            if (!System.IO.File.Exists(HubScenePath))
            {
                Debug.LogError("[Task75] Hub scene not found at " + HubScenePath + ". Run 'Setup Task 14 (Hub Scene)' first.");
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(HubScenePath, OpenSceneMode.Single);

            var hub = Object.FindFirstObjectByType<HubController>();
            var canvas = Object.FindFirstObjectByType<Canvas>();
            var bootstrap = Object.FindFirstObjectByType<GameSessionBootstrap>();
            if (!BuildAndWire(hub, canvas, bootstrap)) return;

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();
            Debug.Log("[Task75] Reroll/Upgrade sinks wired (costs authored on GearEconomyConfig). Press Play from the " +
                      "Hub: inspect an item, click Modify, reroll an affix or upgrade its rarity (max Legendary).");
        }

        /// <summary>Build/rebuild the Task 75 UI on the CURRENTLY-OPEN Hub scene and wire it (idempotent). Shared by
        /// the menu and the Task 14 rebuild chain. Returns false (logged) if the Hub layout isn't present.</summary>
        public static bool BuildAndWire(HubController hub, Canvas canvas, GameSessionBootstrap bootstrap)
        {
            if (hub == null || canvas == null)
            {
                Debug.LogError("[Task75] HubController or Canvas missing. Re-run 'Setup Task 14 (Hub Scene)'.");
                return false;
            }
            var hubRoot = canvas.transform.Find("HubRoot") as RectTransform;
            if (hubRoot == null)
            {
                Debug.LogError("[Task75] 'HubRoot' not found under the Canvas; Hub layout changed.");
                return false;
            }
            var detailPanel = hubRoot.Find("DetailPanel") as RectTransform;
            if (detailPanel == null)
            {
                Debug.LogError("[Task75] 'DetailPanel' not found — run 'Setup Task 25 (Gear Detail Panel)' first.");
                return false;
            }

            // 1) Author the two new cost arrays on the shared economy config (tunable without code changes).
            var economy = AssetDatabase.LoadAssetAtPath<GearEconomyConfigSO>(EconomyPath);
            if (economy != null)
            {
                var eso = new SerializedObject(economy);
                SetIntArray(eso.FindProperty("_rerollAffixCostByRarity"), RerollCost);
                SetIntArray(eso.FindProperty("_upgradeRarityCostByRarity"), UpgradeCost);
                eso.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(economy);
            }
            else
            {
                Debug.LogWarning("[Task75] GearEconomyConfig not found at " + EconomyPath +
                                 " — run 'Setup Task 71 (Gear Economy)' first, or reroll/upgrade will be disabled.");
            }

            // 2) Re-ensure the Hub bootstrap's economy config is wired (Task 73 already does this; harmless to repeat).
            if (bootstrap != null && economy != null)
            {
                var bso = new SerializedObject(bootstrap);
                bso.FindProperty("_gearEconomyConfig").objectReferenceValue = economy;
                bso.ApplyModifiedPropertiesWithoutUndo();
            }

            // 3) Make room on the detail panel for a "Modify" button (raise the stat scroll's bottom — idempotent).
            var statScroll = detailPanel.Find("StatScroll") as RectTransform;
            if (statScroll != null) statScroll.offsetMin = new Vector2(16f, 116f);

            DestroyChild(detailPanel, "Button_Modify");
            var modify = CreateButton(detailPanel, "Modify", new Color(0.32f, 0.4f, 0.55f), Color.white, new Vector2(220f, 40f));
            modify.gameObject.name = "Button_Modify";
            var mrt = (RectTransform)modify.transform;
            mrt.anchorMin = new Vector2(0.5f, 0f); mrt.anchorMax = new Vector2(0.5f, 0f); mrt.pivot = new Vector2(0.5f, 0f);
            mrt.anchoredPosition = new Vector2(0f, 64f);

            // 4) Build the Modify modal.
            DestroyChild(canvas.transform, "ModifyPanel");
            BuildModifyPanel(canvas, out var panel, out var title, out var dust, out var upgrade,
                out var affixContainer, out var close);

            // 5) Wire HubController.
            var so = new SerializedObject(hub);
            so.FindProperty("_detailModifyButton").objectReferenceValue = modify;
            so.FindProperty("_modifyPanel").objectReferenceValue = panel;
            so.FindProperty("_modifyTitle").objectReferenceValue = title;
            so.FindProperty("_modifyDustLabel").objectReferenceValue = dust;
            so.FindProperty("_modifyUpgradeButton").objectReferenceValue = upgrade;
            so.FindProperty("_modifyAffixContainer").objectReferenceValue = affixContainer;
            so.FindProperty("_modifyCloseButton").objectReferenceValue = close;
            so.ApplyModifiedPropertiesWithoutUndo();

            return true;
        }

        private static void BuildModifyPanel(Canvas canvas, out GameObject panel, out TextMeshProUGUI title,
            out TextMeshProUGUI dust, out Button upgrade, out RectTransform affixContainer, out Button close)
        {
            var box = BuildModal(canvas, "ModifyPanel", new Vector2(640f, 580f), out panel);

            title = CreateText(box, "MODIFY ITEM", 28f, TextAlignmentOptions.Center);
            AnchorTop(title.rectTransform, -16f, 40f);

            dust = CreateText(box, "Dust: 0", 22f, TextAlignmentOptions.Center);
            dust.color = new Color(0.85f, 0.78f, 0.5f);
            AnchorTop(dust.rectTransform, -56f, 30f);

            upgrade = CreateButton(box, "Upgrade → [Rarity]", new Color(0.3f, 0.5f, 0.7f), Color.white, new Vector2(380f, 46f));
            var urt = (RectTransform)upgrade.transform;
            urt.anchorMin = new Vector2(0.5f, 1f); urt.anchorMax = new Vector2(0.5f, 1f); urt.pivot = new Vector2(0.5f, 1f);
            urt.anchoredPosition = new Vector2(0f, -98f);

            var hint = CreateText(box, "Reroll an affix's value (type stays the same):", 16f, TextAlignmentOptions.Center);
            hint.color = new Color(0.7f, 0.7f, 0.75f);
            AnchorTop(hint.rectTransform, -156f, 24f);

            affixContainer = CreateScrollView("ModifyAffixScroll", box, new Vector2(0f, 0f), new Vector2(1f, 1f),
                new Vector2(20f, 70f), new Vector2(-20f, -188f));

            close = CreateButton(box, "Close", new Color(0.4f, 0.25f, 0.25f), Color.white, new Vector2(160f, 44f));
            AnchorBottomCenter((RectTransform)close.transform, 14f);
        }

        // --- UI helpers (local copies, matching Task 73 style) -------------------------------------

        private static RectTransform BuildModal(Canvas canvas, string name, Vector2 boxSize, out GameObject panel)
        {
            var panelRect = NewRect(name, canvas.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            panelRect.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.72f);
            panel = panelRect.gameObject;

            var box = NewRect("Box", panelRect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            box.sizeDelta = boxSize;
            box.gameObject.AddComponent<Image>().color = new Color(0.14f, 0.15f, 0.19f, 1f);
            return box;
        }

        private static void AnchorTop(RectTransform rt, float anchoredY, float height)
        {
            rt.anchorMin = new Vector2(0f, 1f); rt.anchorMax = new Vector2(1f, 1f); rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, anchoredY); rt.sizeDelta = new Vector2(0f, height);
        }

        private static void AnchorBottomCenter(RectTransform rt, float anchoredY)
        {
            rt.anchorMin = new Vector2(0.5f, 0f); rt.anchorMax = new Vector2(0.5f, 0f); rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = new Vector2(0f, anchoredY);
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
            vlg.spacing = 4f; vlg.childAlignment = TextAnchor.UpperLeft;
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

        private static void SetIntArray(SerializedProperty prop, int[] values)
        {
            if (prop == null) return;
            prop.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) prop.GetArrayElementAtIndex(i).intValue = values[i];
        }

        private static void DestroyChild(Transform parent, string name)
        {
            var existing = parent.Find(name);
            if (existing != null) Object.DestroyImmediate(existing.gameObject);
        }
    }
}
#endif
