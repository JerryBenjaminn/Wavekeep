#if UNITY_EDITOR
using System.Collections.Generic;
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
    /// Task 43 — Apex/Combo-Apex Discovery Codex. One menu item that does three idempotent things based on
    /// what exists in the open scene:
    /// <list type="number">
    /// <item>(Always) Ensures <c>Assets/Data/Codex/TalentCatalog.asset</c> exists and re-scans the whole
    ///   project for every <see cref="ApexTalentDefinitionSO"/> + <see cref="ComboApexTalentDefinitionSO"/>,
    ///   so the runtime Codex iterates "all talents that exist" without a hardcoded list. Also back-fills any
    ///   empty talent <c>Description</c> with a sensible placeholder so the Codex always has flavour text.</item>
    /// <item>(Hub scene — has a <see cref="HubController"/>) Builds the Codex panel + an OPEN CODEX button and
    ///   wires a <see cref="CodexController"/>.</item>
    /// <item>(Gameplay scene — has a <see cref="WaveSpawner"/>) Adds the centred
    ///   <see cref="FirstDiscoveryNotificationDisplay"/> banner.</item>
    /// </list>
    /// Run "Wavekeep/Setup Task 43 (Codex)" in BOTH scenes (hub + gameplay), saving each. The Codex panel is
    /// also re-applied automatically whenever the Hub is rebuilt by Task 14 (chained, like the Task 25 panel),
    /// so a Hub rebuild never silently drops it. Editor-only.
    /// </summary>
    public static class Task43CodexSetup
    {
        private const string CodexFolder = "Codex";
        private const string CatalogPath = "Assets/Data/Codex/TalentCatalog.asset";
        private const string CodexRootName = "CodexPanel";
        private const string CodexOpenButtonName = "CodexOpenButton";
        private const string NotificationRootName = "FirstDiscoveryNotification";

        [MenuItem("Wavekeep/Setup Task 43 (Codex)")]
        public static void SetupScene()
        {
            var catalog = EnsureCatalog();

            var hub = Object.FindFirstObjectByType<HubController>();
            var spawner = Object.FindFirstObjectByType<WaveSpawner>();
            var canvas = Object.FindFirstObjectByType<Canvas>();
            var bootstrap = Object.FindFirstObjectByType<GameSessionBootstrap>();

            if (hub != null && canvas != null && bootstrap != null)
            {
                BuildCodexInHub(canvas, bootstrap, catalog);
                Debug.Log("[Task43] Codex panel + OPEN CODEX button added to the Hub and wired. Save the scene (Ctrl+S).");
            }
            else if (spawner != null && canvas != null && bootstrap != null)
            {
                BuildNotification(canvas, bootstrap);
                Debug.Log("[Task43] First-discovery notification banner added to the gameplay scene. Save the scene (Ctrl+S).");
            }
            else
            {
                Debug.LogWarning("[Task43] Catalog ensured, but no Hub (HubController) or gameplay (WaveSpawner) " +
                                 "scene piece found to wire. Open the Hub or gameplay scene and re-run.");
            }

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        }

        // --- Catalog -------------------------------------------------------------------------------

        /// <summary>Create (if missing) and fully repopulate the talent catalog by scanning the project.
        /// Returns the catalog asset.</summary>
        public static TalentCatalogSO EnsureCatalog()
        {
            EnsureFolder("Assets/Data", CodexFolder);

            var catalog = AssetDatabase.LoadAssetAtPath<TalentCatalogSO>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<TalentCatalogSO>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }

            var apexes = LoadAll<ApexTalentDefinitionSO>("t:ApexTalentDefinitionSO");
            var combos = LoadAll<ComboApexTalentDefinitionSO>("t:ComboApexTalentDefinitionSO");

            BackfillDescriptions(apexes, combos);

            var so = new SerializedObject(catalog);
            WriteObjectList(so.FindProperty("_apexTalents"), apexes);
            WriteObjectList(so.FindProperty("_comboApexes"), combos);
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[Task43] TalentCatalog populated: {apexes.Count} apex + {combos.Count} combo apex talent(s).");
            return catalog;
        }

        // Back-fill empty descriptions so the Codex always shows flavour text. Only writes when empty, so any
        // designer-authored description is preserved. SOs are content assets here (edit-time), not runtime state.
        private static void BackfillDescriptions(
            List<ApexTalentDefinitionSO> apexes, List<ComboApexTalentDefinitionSO> combos)
        {
            for (int i = 0; i < apexes.Count; i++)
            {
                var apex = apexes[i];
                if (apex == null || !string.IsNullOrEmpty(apex.Description)) continue;
                var so = new SerializedObject(apex);
                so.FindProperty("_description").stringValue =
                    "An automatically-firing apex ability, unlocked by maxing all of its required upgrade lines.";
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(apex);
            }

            for (int i = 0; i < combos.Count; i++)
            {
                var combo = combos[i];
                if (combo == null || !string.IsNullOrEmpty(combo.Description)) continue;
                string primer = combo.PrimingApex != null ? combo.PrimingApex.ApexName : "the first apex";
                string consumer = combo.ConsumingApex != null ? combo.ConsumingApex.ApexName : "the second apex";
                var so = new SerializedObject(combo);
                so.FindProperty("_description").stringValue =
                    $"Cross-hero synergy: {primer} primes struck enemies; {consumer} consumes the prime for " +
                    $"×{combo.ConsumeDamageMultiplier:0.0} amplified damage.";
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(combo);
            }
        }

        // --- Hub Codex panel -----------------------------------------------------------------------

        /// <summary>Build (idempotently) the Codex panel + open button under <paramref name="canvas"/> and wire
        /// a <see cref="CodexController"/>. Public so Task 14's Hub rebuild can re-apply it (Task 25 pattern).
        /// No-op-safe if <paramref name="catalog"/> is null (the controller just shows a "run setup" notice).</summary>
        public static void BuildCodexInHub(Canvas canvas, GameSessionBootstrap bootstrap, TalentCatalogSO catalog)
        {
            DestroyIfExists(CodexRootName);
            DestroyIfExists(CodexOpenButtonName);

            // OPEN CODEX button — top-right, clear of the hub header/columns.
            var openBtn = CreateButton(canvas.transform, "CODEX", new Color(0.30f, 0.28f, 0.55f), Color.white,
                new Vector2(160f, 52f), CodexOpenButtonName);
            var obrt = (RectTransform)openBtn.transform;
            obrt.anchorMin = new Vector2(1f, 1f); obrt.anchorMax = new Vector2(1f, 1f); obrt.pivot = new Vector2(1f, 1f);
            obrt.anchoredPosition = new Vector2(-30f, -26f);

            // Full-screen dim + centred panel box (hidden by the controller on Start).
            var panel = NewRect(CodexRootName, canvas.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            panel.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.75f);

            var box = NewRect("CodexBox", panel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            box.sizeDelta = new Vector2(820f, 760f);
            box.gameObject.AddComponent<Image>().color = new Color(0.12f, 0.13f, 0.17f, 1f);

            var title = CreateText(box, "CODEX — Apex & Combo Discoveries", 30f, TextAlignmentOptions.Center);
            var trt = title.rectTransform;
            trt.anchorMin = new Vector2(0f, 1f); trt.anchorMax = new Vector2(1f, 1f); trt.pivot = new Vector2(0.5f, 1f);
            trt.anchoredPosition = new Vector2(0f, -16f); trt.sizeDelta = new Vector2(0f, 42f);

            var content = CreateScrollView("CodexScroll", box,
                new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(16f, 70f), new Vector2(-16f, -68f));

            var closeBtn = CreateButton(box, "Close", new Color(0.4f, 0.25f, 0.25f), Color.white,
                new Vector2(160f, 44f), "CodexCloseButton");
            var crt = (RectTransform)closeBtn.transform;
            crt.anchorMin = new Vector2(0.5f, 0f); crt.anchorMax = new Vector2(0.5f, 0f); crt.pivot = new Vector2(0.5f, 0f);
            crt.anchoredPosition = new Vector2(0f, 14f);

            var controller = panel.gameObject.AddComponent<CodexController>();
            var so = new SerializedObject(controller);
            so.FindProperty("_bootstrap").objectReferenceValue = bootstrap;
            so.FindProperty("_catalog").objectReferenceValue = catalog;
            so.FindProperty("_panel").objectReferenceValue = panel.gameObject;
            so.FindProperty("_entryContainer").objectReferenceValue = content;
            so.FindProperty("_openButton").objectReferenceValue = openBtn;
            so.FindProperty("_closeButton").objectReferenceValue = closeBtn;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>Task 14 chain hook: re-apply the Codex panel during a Hub rebuild if the catalog exists.</summary>
        public static void BuildAndWireForHubRebuild(Canvas canvas, GameSessionBootstrap bootstrap)
        {
            var catalog = AssetDatabase.LoadAssetAtPath<TalentCatalogSO>(CatalogPath);
            if (catalog == null) return; // Task 43 not set up yet; nothing to restore.
            BuildCodexInHub(canvas, bootstrap, catalog);
        }

        // --- Gameplay notification -----------------------------------------------------------------

        private static void BuildNotification(Canvas canvas, GameSessionBootstrap bootstrap)
        {
            DestroyIfExists(NotificationRootName);

            var uiSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");

            // Top-centre band so the banner sits clear of the bottom HUD (apex bars / ultimate bar).
            var rootGo = new GameObject(NotificationRootName, typeof(RectTransform));
            rootGo.transform.SetParent(canvas.transform, false);
            var rt = (RectTransform)rootGo.transform;
            rt.anchorMin = new Vector2(0.5f, 1f);
            rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, -120f);
            rt.sizeDelta = new Vector2(600f, 120f);

            var display = rootGo.AddComponent<FirstDiscoveryNotificationDisplay>();
            var so = new SerializedObject(display);
            so.FindProperty("_container").objectReferenceValue = rt;
            so.FindProperty("_bannerSprite").objectReferenceValue = uiSprite;
            so.FindProperty("_bootstrap").objectReferenceValue = bootstrap;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // --- Asset helpers -------------------------------------------------------------------------

        private static List<T> LoadAll<T>(string filter) where T : Object
        {
            var list = new List<T>();
            foreach (var guid in AssetDatabase.FindAssets(filter))
            {
                var asset = AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid));
                if (asset != null) list.Add(asset);
            }
            list.Sort((a, b) => string.CompareOrdinal(a.name, b.name)); // stable Codex order
            return list;
        }

        private static void WriteObjectList<T>(SerializedProperty listProp, List<T> items) where T : Object
        {
            listProp.arraySize = items.Count;
            for (int i = 0; i < items.Count; i++)
                listProp.GetArrayElementAtIndex(i).objectReferenceValue = items[i];
        }

        private static void EnsureFolder(string parent, string child)
        {
            if (!AssetDatabase.IsValidFolder($"{parent}/{child}"))
                AssetDatabase.CreateFolder(parent, child);
        }

        private static void DestroyIfExists(string objectName)
        {
            var existing = GameObject.Find(objectName);
            if (existing != null) Object.DestroyImmediate(existing);
        }

        // --- UI builders (mirror the other Task setups' placeholder style) -------------------------

        private static RectTransform NewRect(string name, Transform parent,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = anchorMin; rt.anchorMax = anchorMax; rt.offsetMin = offsetMin; rt.offsetMax = offsetMax;
            return rt;
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
            vlg.spacing = 8f;
            vlg.padding = new RectOffset(10, 10, 10, 10);
            vlg.childAlignment = TextAnchor.UpperLeft;
            vlg.childControlWidth = false; vlg.childControlHeight = false;
            vlg.childForceExpandWidth = false; vlg.childForceExpandHeight = false;
            content.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var scroll = scrollGo.GetComponent<ScrollRect>();
            scroll.content = crt; scroll.viewport = srt;
            scroll.horizontal = false; scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            return crt;
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

        private static Button CreateButton(Transform parent, string label, Color bg, Color fg, Vector2 size, string name)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            ((RectTransform)go.transform).sizeDelta = size;
            go.GetComponent<Image>().color = bg;

            var labelGo = new GameObject("Label", typeof(RectTransform));
            labelGo.transform.SetParent(go.transform, false);
            var tmp = labelGo.AddComponent<TextMeshProUGUI>();
            tmp.text = label;
            tmp.fontSize = 22f;
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
