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
    /// Task 73 — adds the gear-economy UI (Dust counter, Salvage button, Artifact Forge screen, overflow
    /// resolution, shared confirm modal) to the Task 14 Hub scene, and fixes the Task 71 flag #3 gap by wiring
    /// the Hub bootstrap's <c>_gearEconomyConfig</c> + <c>_gearAffixConfig</c> (without which salvage/forge no-op).
    ///
    /// Additive + idempotent, like the Task 25 detail panel: re-running rebuilds the elements in place, and the
    /// build is also chained into <see cref="Task14SceneSetup"/> (after the detail/codex panels) so a base-Hub
    /// rebuild never drops it. No backend logic is touched — everything routes through existing
    /// <c>GearManager</c>/<c>GearGenerator</c> APIs; the economy config is read by the UI only for displaying
    /// pre-confirm numbers (the SAME asset the bootstrap/GearManager use).
    ///
    /// Run "Wavekeep/Setup Task 73 (Hub Gear Economy)" AFTER Task 14/25 and Task 71. Editor-only.
    /// </summary>
    public static class Task73HubEconomySetup
    {
        private const string HubScenePath = "Assets/Scenes/Hub.unity";
        private const string EconomyPath = "Assets/Data/Gear/GearEconomyConfig.asset";
        private const string AffixConfigPath = "Assets/Data/Gear/GearAffixCountConfig.asset";

        [MenuItem("Wavekeep/Setup Task 73 (Hub Gear Economy)")]
        public static void SetupScene()
        {
            if (!System.IO.File.Exists(HubScenePath))
            {
                Debug.LogError("[Task73] Hub scene not found at " + HubScenePath + ". Run 'Setup Task 14 (Hub Scene)' first.");
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
            Debug.Log("[Task73] Hub gear-economy UI added + wired (Dust, Salvage, Forge, Overflow) and Hub bootstrap " +
                      "configs fixed. Press Play from the Hub: salvage an inventory item, open the Artifact Forge, " +
                      "and resolve overflow.");
        }

        /// <summary>Build/rebuild the Task 73 UI on the CURRENTLY-OPEN Hub scene and wire it (idempotent). Shared by
        /// the menu and the Task 14 rebuild chain. Returns false (logged) if the Hub layout isn't present.</summary>
        public static bool BuildAndWire(HubController hub, Canvas canvas, GameSessionBootstrap bootstrap)
        {
            if (hub == null || canvas == null)
            {
                Debug.LogError("[Task73] HubController or Canvas missing. Re-run 'Setup Task 14 (Hub Scene)'.");
                return false;
            }
            var hubRoot = canvas.transform.Find("HubRoot") as RectTransform;
            if (hubRoot == null)
            {
                Debug.LogError("[Task73] 'HubRoot' not found under the Canvas; Hub layout changed.");
                return false;
            }

            var economy = AssetDatabase.LoadAssetAtPath<GearEconomyConfigSO>(EconomyPath);
            var affix = AssetDatabase.LoadAssetAtPath<GearAffixCountConfigSO>(AffixConfigPath);
            if (economy == null)
                Debug.LogWarning("[Task73] GearEconomyConfig not found at " + EconomyPath +
                                 " — run 'Setup Task 71 (Gear Economy)' first, or salvage/forge will show 0 / be disabled.");

            // --- Task 71 flag #3 fix: wire the Hub bootstrap's gear configs so salvage/forge actually work here. ---
            if (bootstrap != null)
            {
                var bso = new SerializedObject(bootstrap);
                if (economy != null) bso.FindProperty("_gearEconomyConfig").objectReferenceValue = economy;
                if (affix != null) bso.FindProperty("_gearAffixConfig").objectReferenceValue = affix;
                bso.ApplyModifiedPropertiesWithoutUndo();
            }
            else
            {
                Debug.LogWarning("[Task73] No GameSessionBootstrap found — Hub bootstrap config NOT wired (Task 71 flag #3).");
            }

            // --- Idempotency: remove anything from a previous run. ---
            DestroyChild(hubRoot, "DustLabel");
            DestroyChild(hubRoot, "Button_OpenForge");
            DestroyChild(hubRoot, "Button_OpenOverflow");
            DestroyChild(canvas.transform, "ForgePanel");
            DestroyChild(canvas.transform, "OverflowPanel");
            DestroyChild(canvas.transform, "ConfirmPanel");

            // --- Build pieces. ---
            var dust = BuildDustLabel(hubRoot);
            var openForge = BuildCornerButton(hubRoot, "Button_OpenForge", "ARTIFACT FORGE",
                new Color(0.28f, 0.34f, 0.5f), new Vector2(40f, 92f));
            var openOverflow = BuildCornerButton(hubRoot, "Button_OpenOverflow", "Overflow (0)",
                new Color(0.5f, 0.4f, 0.25f), new Vector2(40f, 28f));

            var salvage = BuildSalvageButton(hub); // also repositions Equip/Unequip into a 3-button row

            BuildForgePanel(canvas, out var forgePanel, out var forgeContainer, out var forgeDust, out var forgeClose);
            BuildOverflowPanel(canvas, out var ovPanel, out var ovContainer, out var ovFeedback, out var ovClose);
            BuildConfirmPanel(canvas, out var confirmPanel, out var confirmText, out var confirmYes, out var confirmNo);

            // --- Wire HubController. ---
            var so = new SerializedObject(hub);
            so.FindProperty("_economyConfig").objectReferenceValue = economy;
            so.FindProperty("_dustLabel").objectReferenceValue = dust;
            so.FindProperty("_detailSalvageButton").objectReferenceValue = salvage;
            so.FindProperty("_openForgeButton").objectReferenceValue = openForge;
            so.FindProperty("_forgePanel").objectReferenceValue = forgePanel;
            so.FindProperty("_forgeContainer").objectReferenceValue = forgeContainer;
            so.FindProperty("_forgeDustLabel").objectReferenceValue = forgeDust;
            so.FindProperty("_forgeCloseButton").objectReferenceValue = forgeClose;
            so.FindProperty("_openOverflowButton").objectReferenceValue = openOverflow;
            so.FindProperty("_overflowPanel").objectReferenceValue = ovPanel;
            so.FindProperty("_overflowContainer").objectReferenceValue = ovContainer;
            so.FindProperty("_overflowFeedbackLabel").objectReferenceValue = ovFeedback;
            so.FindProperty("_overflowCloseButton").objectReferenceValue = ovClose;
            so.FindProperty("_confirmPanel").objectReferenceValue = confirmPanel;
            so.FindProperty("_confirmText").objectReferenceValue = confirmText;
            so.FindProperty("_confirmYesButton").objectReferenceValue = confirmYes;
            so.FindProperty("_confirmNoButton").objectReferenceValue = confirmNo;
            so.ApplyModifiedPropertiesWithoutUndo();

            return true;
        }

        // --- HubRoot pieces -------------------------------------------------------------------------

        private static TextMeshProUGUI BuildDustLabel(RectTransform hubRoot)
        {
            var t = CreateText(hubRoot, "Dust: 0", 26f, TextAlignmentOptions.Right);
            t.gameObject.name = "DustLabel";
            t.color = new Color(0.85f, 0.78f, 0.5f);
            var rt = t.rectTransform;
            rt.anchorMin = new Vector2(1f, 1f); rt.anchorMax = new Vector2(1f, 1f); rt.pivot = new Vector2(1f, 1f);
            rt.anchoredPosition = new Vector2(-44f, -30f); rt.sizeDelta = new Vector2(300f, 36f);
            return t;
        }

        private static Button BuildCornerButton(RectTransform hubRoot, string name, string label, Color bg, Vector2 pos)
        {
            var b = CreateButton(hubRoot, label, bg, Color.white, new Vector2(240f, 52f));
            b.gameObject.name = name;
            var rt = (RectTransform)b.transform;
            rt.anchorMin = new Vector2(0f, 0f); rt.anchorMax = new Vector2(0f, 0f); rt.pivot = new Vector2(0f, 0f);
            rt.anchoredPosition = pos;
            return b;
        }

        // Adds a Salvage button to the detail panel and reflows Equip/Unequip into a centered 3-button bottom row.
        private static Button BuildSalvageButton(HubController hub)
        {
            var so = new SerializedObject(hub);
            var panel = so.FindProperty("_detailPanel").objectReferenceValue as GameObject;
            var equip = so.FindProperty("_detailEquipButton").objectReferenceValue as Button;
            var unequip = so.FindProperty("_detailUnequipButton").objectReferenceValue as Button;
            if (panel == null)
            {
                Debug.LogWarning("[Task73] Detail panel not found — Salvage button skipped. Run 'Setup Task 25' first.");
                return null;
            }

            DestroyChild(panel.transform, "Button_Salvage");

            // Reflow Equip (left) / Unequip (right) to make room for Salvage (center), all on the y=14 bottom row.
            if (equip != null) PlaceBottom((RectTransform)equip.transform, new Vector2(0f, 0f), new Vector2(16f, 14f));
            if (unequip != null) PlaceBottom((RectTransform)unequip.transform, new Vector2(1f, 0f), new Vector2(-16f, 14f));

            var salvage = CreateButton(panel.transform, "Salvage", new Color(0.5f, 0.4f, 0.25f), Color.white,
                new Vector2(140f, 44f));
            salvage.gameObject.name = "Button_Salvage";
            PlaceBottom((RectTransform)salvage.transform, new Vector2(0.5f, 0f), new Vector2(0f, 14f));
            return salvage;
        }

        private static void PlaceBottom(RectTransform rt, Vector2 anchor, Vector2 pos)
        {
            rt.sizeDelta = new Vector2(140f, 44f);
            rt.anchorMin = anchor; rt.anchorMax = anchor; rt.pivot = anchor;
            rt.anchoredPosition = pos;
        }

        // --- Modals ---------------------------------------------------------------------------------

        private static void BuildForgePanel(Canvas canvas, out GameObject panel, out RectTransform container,
            out TextMeshProUGUI dust, out Button close)
        {
            var box = BuildModal(canvas, "ForgePanel", new Vector2(620f, 560f), out panel);

            var title = CreateText(box, "ARTIFACT FORGE", 30f, TextAlignmentOptions.Center);
            AnchorTop(title.rectTransform, -16f, 40f);

            dust = CreateText(box, "Dust: 0", 22f, TextAlignmentOptions.Center);
            dust.color = new Color(0.85f, 0.78f, 0.5f);
            AnchorTop(dust.rectTransform, -58f, 30f);

            var hint = CreateText(box, "Choose a rarity to craft (Dust only — deterministic):", 16f, TextAlignmentOptions.Center);
            hint.color = new Color(0.7f, 0.7f, 0.75f);
            AnchorTop(hint.rectTransform, -92f, 24f);

            container = CreateScrollView("ForgeScroll", box, new Vector2(0f, 0f), new Vector2(1f, 1f),
                new Vector2(20f, 70f), new Vector2(-20f, -124f));

            close = CreateButton(box, "Close", new Color(0.4f, 0.25f, 0.25f), Color.white, new Vector2(160f, 44f));
            AnchorBottomCenter((RectTransform)close.transform, 14f);
        }

        private static void BuildOverflowPanel(Canvas canvas, out GameObject panel, out RectTransform container,
            out TextMeshProUGUI feedback, out Button close)
        {
            var box = BuildModal(canvas, "OverflowPanel", new Vector2(680f, 580f), out panel);

            var title = CreateText(box, "OVERFLOW — PENDING ITEMS", 28f, TextAlignmentOptions.Center);
            AnchorTop(title.rectTransform, -16f, 40f);

            var hint = CreateText(box, "Drops that arrived while inventory was full. Claim or salvage each:", 16f, TextAlignmentOptions.Center);
            hint.color = new Color(0.7f, 0.7f, 0.75f);
            AnchorTop(hint.rectTransform, -56f, 24f);

            container = CreateScrollView("OverflowScroll", box, new Vector2(0f, 0f), new Vector2(1f, 1f),
                new Vector2(20f, 96f), new Vector2(-20f, -88f));

            feedback = CreateText(box, "", 18f, TextAlignmentOptions.Center);
            AnchorBottomCenter((RectTransform)feedback.rectTransform, 64f);
            feedback.rectTransform.sizeDelta = new Vector2(620f, 28f);

            close = CreateButton(box, "Close", new Color(0.4f, 0.25f, 0.25f), Color.white, new Vector2(160f, 44f));
            AnchorBottomCenter((RectTransform)close.transform, 14f);
        }

        private static void BuildConfirmPanel(Canvas canvas, out GameObject panel, out TextMeshProUGUI text,
            out Button yes, out Button no)
        {
            var box = BuildModal(canvas, "ConfirmPanel", new Vector2(600f, 240f), out panel);

            text = CreateText(box, "Are you sure?", 22f, TextAlignmentOptions.Center);
            var trt = text.rectTransform;
            trt.anchorMin = new Vector2(0f, 0f); trt.anchorMax = new Vector2(1f, 1f);
            trt.offsetMin = new Vector2(24f, 76f); trt.offsetMax = new Vector2(-24f, -16f);

            yes = CreateButton(box, "Confirm", new Color(0.2f, 0.55f, 0.3f), Color.white, new Vector2(200f, 48f));
            var yrt = (RectTransform)yes.transform;
            yrt.anchorMin = new Vector2(0f, 0f); yrt.anchorMax = new Vector2(0f, 0f); yrt.pivot = new Vector2(0f, 0f);
            yrt.anchoredPosition = new Vector2(40f, 16f);

            no = CreateButton(box, "Cancel", new Color(0.45f, 0.3f, 0.3f), Color.white, new Vector2(200f, 48f));
            var nrt = (RectTransform)no.transform;
            nrt.anchorMin = new Vector2(1f, 0f); nrt.anchorMax = new Vector2(1f, 0f); nrt.pivot = new Vector2(1f, 0f);
            nrt.anchoredPosition = new Vector2(-40f, 16f);
        }

        // Full-screen dim + centered box. Returns the box; the panel root is the full-screen dim (toggled by Hub).
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

        // --- UI helpers (local copies, matching Task 14/25 style) ----------------------------------

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

        private static void DestroyChild(Transform parent, string name)
        {
            var existing = parent.Find(name);
            if (existing != null) Object.DestroyImmediate(existing.gameObject);
        }
    }
}
#endif
