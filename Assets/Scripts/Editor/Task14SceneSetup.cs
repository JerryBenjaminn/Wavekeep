#if UNITY_EDITOR
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using Wavekeep.Core;
using Wavekeep.Data;
using Wavekeep.UI;

namespace Wavekeep.EditorTools
{
    /// <summary>
    /// Builds the Task 14 Hub scene from scratch (a brand-new scene, not an edit of the gameplay scene):
    /// camera, EventSystem, landscape Canvas, a <see cref="GameSessionBootstrap"/> wired to the SAME
    /// <see cref="GearCatalogSO"/> the gameplay scene uses (so it loads the identical on-disk gear save),
    /// and a fully-wired <see cref="HubController"/> with hero column, 6-slot loadout view, inventory
    /// list, equip-picker modal, and a Start Run button.
    ///
    /// It then sets the build-settings scene order so the GAME LAUNCHES INTO THE HUB (index 0) and the
    /// existing gameplay scene (SampleScene) follows (index 1). HubController loads "SampleScene" on
    /// Start Run and carries the chosen hero via <c>RunLaunchContext</c>; the gameplay scene's
    /// HeroSelectController auto-starts that hero, so the player never picks twice.
    ///
    /// Run "Wavekeep/Setup Task 14 (Hub Scene)" once. It creates/overwrites Assets/Scenes/Hub.unity and
    /// updates build settings. Editor-only. Requires the Task 05 hero assets and the Task 12 gear catalog.
    /// </summary>
    public static class Task14SceneSetup
    {
        private const string HubScenePath = "Assets/Scenes/Hub.unity";
        private const string GameplayScenePath = "Assets/Scenes/SampleScene.unity";
        private const string CatalogPath = "Assets/Data/Gear/GearCatalog.asset";

        [MenuItem("Wavekeep/Setup Task 14 (Hub Scene)")]
        public static void SetupScene()
        {
            // --- Pre-check that the required assets EXIST before we wipe the current scene. We do NOT
            //     keep these references: creating a single-mode new scene below invalidates objects
            //     loaded beforehand (they serialize as null when wired afterward — the original bug).
            //     We reload them fresh AFTER NewScene instead. ---
            if (AssetDatabase.LoadAssetAtPath<GearCatalogSO>(CatalogPath) == null)
            {
                Debug.LogError("[Task14SceneSetup] GearCatalog not found at " + CatalogPath +
                               ". Run 'Wavekeep/Setup Task 12 (Gear Core)' first.");
                return;
            }
            // Task 37: discover ALL hero assets in the project rather than a hardcoded path list, so a newly
            // authored hero is picked up by both this setup and the runtime team-select screen with no edits.
            if (AssetDatabase.FindAssets("t:HeroDefinitionSO").Length == 0)
            {
                Debug.LogError("[Task14SceneSetup] No hero assets found. Run 'Wavekeep/Setup Task 05 (Heroes)' first.");
                return;
            }

            if (TMP_Settings.defaultFontAsset == null)
            {
                Debug.LogWarning("[Task14SceneSetup] TMP has no default font asset. If Hub text doesn't render, " +
                                 "import via Window > TextMeshPro > Import TMP Essential Resources, then re-run.");
            }

            // --- New, empty Hub scene (offer to save any unsaved current-scene edits first). ---
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // --- Reload the asset references NOW (post-NewScene) so they wire as live, non-null refs. ---
            var catalog = AssetDatabase.LoadAssetAtPath<GearCatalogSO>(CatalogPath);
            var roster = LoadAllHeroes();

            // Camera + AudioListener (ScreenSpaceOverlay UI renders without one, but a scene wants a camera).
            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            camGo.AddComponent<Camera>().clearFlags = CameraClearFlags.SolidColor;
            camGo.GetComponent<Camera>().backgroundColor = new Color(0.08f, 0.09f, 0.12f);
            camGo.AddComponent<AudioListener>();

            // EventSystem (new Input System UI module — matches the rest of the project).
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));

            // Landscape-tuned Canvas.
            var canvasGo = new GameObject("UI Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            // GameSessionBootstrap wired to the gear catalog (so it loads the persistent save).
            var bootstrapGo = new GameObject("GameSession", typeof(GameSessionBootstrap));
            var bootstrap = bootstrapGo.GetComponent<GameSessionBootstrap>();
            var bso = new SerializedObject(bootstrap);
            bso.FindProperty("_gearCatalog").objectReferenceValue = catalog;
            bso.ApplyModifiedPropertiesWithoutUndo();

            var hub = BuildHubUi(canvas, bootstrap, roster);

            // Task 39 (regression fix): re-apply the Task 25 gear detail panel as part of the SAME build.
            // The panel is an additive setup; building the Hub from an empty scene here (e.g. when the Task 37
            // team selector was added) used to drop it, leaving HubController's _detail* refs null so the panel
            // never opened. Chaining it in means every Hub rebuild restores the panel + its wiring in one pass.
            Task25SceneSetup.BuildAndWire(hub, canvas);

            // Task 43: same chaining lesson — re-apply the Codex panel + OPEN CODEX button so a Hub rebuild
            // never silently drops it. No-ops if the Task 43 catalog asset doesn't exist yet.
            Task43CodexSetup.BuildAndWireForHubRebuild(canvas, bootstrap);

            // Task 73: re-apply the gear-economy UI (Dust/Salvage/Forge/Overflow) + fix the Hub bootstrap's gear
            // config wiring (Task 71 flag #3). Runs AFTER Task 25 so the detail panel exists for the Salvage button.
            Task73HubEconomySetup.BuildAndWire(hub, canvas, bootstrap);

            // Task 74: re-apply the mass-salvage bar (rarity quick-filters + selection summary + Mass Salvage/Clear)
            // to the inventory column. Runs AFTER Task 73 so it reflows the same inventory scroll consistently.
            Task74MassSalvageSetup.BuildAndWire(hub, canvas);

            // Task 75: re-apply the reroll/upgrade sinks — authors the new economy costs, adds the detail-panel
            // Modify button + the Modify modal. Runs AFTER Task 73/25 so the detail panel + economy config exist.
            Task75GearMutationSetup.BuildAndWire(hub, canvas, bootstrap);

            // --- Save the scene + register build order (Hub first). ---
            EnsureFolder("Assets", "Scenes");
            EditorSceneManager.SaveScene(scene, HubScenePath);
            SetBuildScenes();

            // --- Verify the asset references actually wired (the bug we just fixed). ---
            VerifyWiring(bootstrap, hub, roster.Count);

            Debug.Log("[Task14SceneSetup] Hub scene created at " + HubScenePath +
                      " and set as the startup scene (build index 0). Press Play from the Hub to test: " +
                      "browse gear, equip on a hero, Start Run. Loadout persists via the existing GearManager save.");
        }

        // --- UI construction -----------------------------------------------------------------------

        private static HubController BuildHubUi(Canvas canvas, GameSessionBootstrap bootstrap, List<HeroDefinitionSO> roster)
        {
            // Full-screen background.
            var root = NewRect("HubRoot", canvas.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            root.gameObject.AddComponent<Image>().color = new Color(0.10f, 0.11f, 0.14f, 1f);

            // Header title.
            var title = CreateText(root, "WAVEKEEP — HUB", 44f, TextAlignmentOptions.Center);
            var trt = title.rectTransform;
            trt.anchorMin = new Vector2(0f, 1f); trt.anchorMax = new Vector2(1f, 1f); trt.pivot = new Vector2(0.5f, 1f);
            trt.anchoredPosition = new Vector2(0f, -24f); trt.sizeDelta = new Vector2(0f, 60f);

            // === LEFT COLUMN: heroes + selected-hero loadout ===
            var left = NewRect("LeftColumn", root, new Vector2(0f, 0f), new Vector2(0.5f, 1f), Vector2.zero, Vector2.zero);
            ((RectTransform)left).offsetMin = new Vector2(40f, 110f);
            ((RectTransform)left).offsetMax = new Vector2(-20f, -110f);

            CreateColumnHeader(left, "TEAM  (✓ = in run · name = edit gear)", 1f);

            // Task 40 (overlap fix): the team list is now a FIXED-HEIGHT scroll band rather than a free-growing
            // container. It previously grew downward with the roster and overran the "Editing:" label below it
            // (with two heroes the second row rendered on top of the label). Bounding the list keeps every
            // section beneath it at a fixed, non-overlapping position for ANY hero count — the list scrolls if
            // it is taller than the band. Sections use a consistent 16px gap so the column reads evenly.
            var heroContainer = CreateScrollView("HeroScroll", left,
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -236f), new Vector2(0f, -48f));

            var selectedLabel = CreateText(left, "Editing: —", 24f, TextAlignmentOptions.Left);
            var slrt = selectedLabel.rectTransform;
            slrt.anchorMin = new Vector2(0f, 1f); slrt.anchorMax = new Vector2(1f, 1f); slrt.pivot = new Vector2(0f, 1f);
            slrt.anchoredPosition = new Vector2(0f, -252f); slrt.sizeDelta = new Vector2(0f, 32f);

            var slotContainer = CreateVerticalContainer("SlotContainer", left,
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -300f), new Vector2(0f, 320f), 6f);

            // === RIGHT COLUMN: scrollable inventory list ===
            var right = NewRect("RightColumn", root, new Vector2(0.5f, 0f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);
            ((RectTransform)right).offsetMin = new Vector2(20f, 110f);
            ((RectTransform)right).offsetMax = new Vector2(-40f, -110f);

            CreateColumnHeader(right, "INVENTORY", 1f);
            var inventoryContainer = CreateScrollView("InventoryScroll", right,
                new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0f, 0f), new Vector2(0f, -44f));

            // === Start Run button (bottom center) ===
            var startBtn = CreateButton(root, "START RUN", new Color(0.2f, 0.55f, 0.25f), Color.white, new Vector2(260f, 64f));
            var srt = (RectTransform)startBtn.transform;
            srt.anchorMin = new Vector2(0.5f, 0f); srt.anchorMax = new Vector2(0.5f, 0f); srt.pivot = new Vector2(0.5f, 0f);
            srt.anchoredPosition = new Vector2(0f, 28f);

            // Task 37: team-status / "select at least one hero" feedback, just above Start Run.
            var feedback = CreateText(root, "", 22f, TextAlignmentOptions.Center);
            var frt = feedback.rectTransform;
            frt.anchorMin = new Vector2(0.5f, 0f); frt.anchorMax = new Vector2(0.5f, 0f); frt.pivot = new Vector2(0.5f, 0f);
            frt.anchoredPosition = new Vector2(0f, 100f); frt.sizeDelta = new Vector2(560f, 32f);

            // === Equip picker modal (hidden by HubController on Start) ===
            var pickerPanel = NewRect("EquipPickerPanel", canvas.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            pickerPanel.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.7f);

            var pickerBox = NewRect("PickerBox", pickerPanel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            ((RectTransform)pickerBox).sizeDelta = new Vector2(560f, 560f);
            pickerBox.gameObject.AddComponent<Image>().color = new Color(0.14f, 0.15f, 0.19f, 1f);

            var pickerTitle = CreateText(pickerBox, "Equip —", 28f, TextAlignmentOptions.Center);
            var ptrt = pickerTitle.rectTransform;
            ptrt.anchorMin = new Vector2(0f, 1f); ptrt.anchorMax = new Vector2(1f, 1f); ptrt.pivot = new Vector2(0.5f, 1f);
            ptrt.anchoredPosition = new Vector2(0f, -16f); ptrt.sizeDelta = new Vector2(0f, 40f);

            var pickerContainer = CreateScrollView("PickerScroll", pickerBox,
                new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0f, 56f), new Vector2(0f, -64f));

            var pickerClose = CreateButton(pickerBox, "Close", new Color(0.4f, 0.25f, 0.25f), Color.white, new Vector2(160f, 40f));
            var pcrt = (RectTransform)pickerClose.transform;
            pcrt.anchorMin = new Vector2(0.5f, 0f); pcrt.anchorMax = new Vector2(0.5f, 0f); pcrt.pivot = new Vector2(0.5f, 0f);
            pcrt.anchoredPosition = new Vector2(0f, 10f);

            // === Wire the controller ===
            var hubGo = new GameObject("HubController", typeof(HubController));
            var hub = hubGo.GetComponent<HubController>();
            var so = new SerializedObject(hub);
            so.FindProperty("_bootstrap").objectReferenceValue = bootstrap;
            so.FindProperty("_gameplaySceneName").stringValue = "SampleScene";
            so.FindProperty("_heroButtonContainer").objectReferenceValue = heroContainer;
            so.FindProperty("_selectedHeroLabel").objectReferenceValue = selectedLabel;
            so.FindProperty("_slotContainer").objectReferenceValue = slotContainer;
            so.FindProperty("_inventoryContainer").objectReferenceValue = inventoryContainer;
            so.FindProperty("_startRunButton").objectReferenceValue = startBtn;
            so.FindProperty("_startFeedbackLabel").objectReferenceValue = feedback;
            so.FindProperty("_equipPickerPanel").objectReferenceValue = pickerPanel.gameObject;
            so.FindProperty("_equipPickerTitle").objectReferenceValue = pickerTitle;
            so.FindProperty("_equipPickerContainer").objectReferenceValue = pickerContainer;
            so.FindProperty("_equipPickerCloseButton").objectReferenceValue = pickerClose;

            var rosterProp = so.FindProperty("_heroRoster");
            rosterProp.arraySize = roster.Count;
            for (int i = 0; i < roster.Count; i++)
                rosterProp.GetArrayElementAtIndex(i).objectReferenceValue = roster[i];
            so.ApplyModifiedPropertiesWithoutUndo();

            return hub;
        }

        /// <summary>Reads the saved scene's serialized refs back to confirm the catalog and every hero
        /// entry are non-null — catches a regression of the "loaded-before-NewScene → null" bug.</summary>
        private static void VerifyWiring(GameSessionBootstrap bootstrap, HubController hub, int expectedHeroes)
        {
            var bso = new SerializedObject(bootstrap);
            if (bso.FindProperty("_gearCatalog").objectReferenceValue == null)
                Debug.LogError("[Task14SceneSetup] _gearCatalog wired NULL — Hub will load empty gear. Re-run the setup.");

            var hso = new SerializedObject(hub);
            var rosterProp = hso.FindProperty("_heroRoster");
            int wired = 0;
            for (int i = 0; i < rosterProp.arraySize; i++)
                if (rosterProp.GetArrayElementAtIndex(i).objectReferenceValue != null) wired++;
            if (wired != expectedHeroes)
                Debug.LogError($"[Task14SceneSetup] Hero roster wired {wired}/{expectedHeroes} non-null — " +
                               "hero buttons will be missing. Re-run the setup.");
            else
                Debug.Log($"[Task14SceneSetup] Wiring verified: gear catalog + {wired} hero(es) referenced.");
        }

        // --- UI helpers -----------------------------------------------------------------------------

        private static void CreateColumnHeader(RectTransform parent, string text, float topInset)
        {
            var t = CreateText(parent, text, 28f, TextAlignmentOptions.Left);
            var rt = t.rectTransform;
            rt.anchorMin = new Vector2(0f, 1f); rt.anchorMax = new Vector2(1f, 1f); rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(0f, 0f); rt.sizeDelta = new Vector2(0f, 40f);
        }

        /// <summary>Container with a VerticalLayoutGroup; HubController dumps generated rows/buttons here.</summary>
        private static RectTransform CreateVerticalContainer(string name, RectTransform parent,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPos, Vector2 size, float spacing)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(VerticalLayoutGroup));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = anchorMin; rt.anchorMax = anchorMax; rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = anchoredPos; rt.sizeDelta = size;
            ConfigureLayout(go.GetComponent<VerticalLayoutGroup>(), spacing);
            return rt;
        }

        /// <summary>Scrollable list: viewport (masked) + content (vertical layout + size fitter). Returns
        /// the CONTENT RectTransform, which HubController populates with text/buttons.</summary>
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
            crt.anchoredPosition = Vector2.zero; crt.sizeDelta = new Vector2(0f, 0f);
            ConfigureLayout(content.GetComponent<VerticalLayoutGroup>(), 4f);
            content.GetComponent<VerticalLayoutGroup>().padding = new RectOffset(8, 8, 8, 8);
            content.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var scroll = scrollGo.GetComponent<ScrollRect>();
            scroll.content = crt;
            scroll.viewport = srt;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            return crt;
        }

        private static void ConfigureLayout(VerticalLayoutGroup vlg, float spacing)
        {
            vlg.spacing = spacing;
            vlg.childAlignment = TextAnchor.UpperLeft;
            vlg.childControlWidth = false;
            vlg.childControlHeight = false;
            vlg.childForceExpandWidth = false;
            vlg.childForceExpandHeight = false;
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
            tmp.fontSize = 22f;
            tmp.color = fg;
            tmp.alignment = TextAlignmentOptions.Center;
            if (TMP_Settings.defaultFontAsset != null) tmp.font = TMP_Settings.defaultFontAsset;
            var lrt = tmp.rectTransform;
            lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one; lrt.offsetMin = Vector2.zero; lrt.offsetMax = Vector2.zero;
            return go.GetComponent<Button>();
        }

        /// <summary>Task 37: every <see cref="HeroDefinitionSO"/> in the project, name-sorted for a stable
        /// roster order. Adding a hero asset grows the Hub roster (and team-select list) automatically.</summary>
        private static List<HeroDefinitionSO> LoadAllHeroes()
        {
            var list = new List<HeroDefinitionSO>();
            foreach (var guid in AssetDatabase.FindAssets("t:HeroDefinitionSO"))
            {
                var hero = AssetDatabase.LoadAssetAtPath<HeroDefinitionSO>(AssetDatabase.GUIDToAssetPath(guid));
                if (hero != null) list.Add(hero);
            }
            list.Sort((a, b) => string.CompareOrdinal(a.HeroName, b.HeroName));
            return list;
        }

        private static void SetBuildScenes()
        {
            var scenes = new List<EditorBuildSettingsScene>
            {
                new EditorBuildSettingsScene(HubScenePath, true),
            };
            if (System.IO.File.Exists(GameplayScenePath))
                scenes.Add(new EditorBuildSettingsScene(GameplayScenePath, true));
            else
                Debug.LogWarning("[Task14SceneSetup] Gameplay scene not found at " + GameplayScenePath +
                                 "; add it to Build Settings manually so 'Start Run' can load it.");
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        private static void EnsureFolder(string parent, string child)
        {
            if (!AssetDatabase.IsValidFolder($"{parent}/{child}"))
                AssetDatabase.CreateFolder(parent, child);
        }
    }
}
#endif
