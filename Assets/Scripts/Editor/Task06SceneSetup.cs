#if UNITY_EDITOR
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Wavekeep.Core;
using Wavekeep.Data;
using Wavekeep.Runtime;
using Wavekeep.UI;
using Wavekeep.Waves;

namespace Wavekeep.EditorTools
{
    /// <summary>
    /// Builds the Task 06 shop on top of Tasks 01–05: authors a fixed assortment of
    /// <see cref="ConsumableDefinitionSO"/> assets (covering every effect type plus a non-stackable
    /// item), and adds a between-wave shop Canvas screen wired to a <see cref="ShopScreenController"/>
    /// (item rows are generated at runtime by the controller, so the screen built here is just the
    /// frame: panel, title, currency readout, item container, Continue button).
    ///
    /// Built in code for the same reason as Tasks 01–05 (scenes/assets can't be reliably hand-authored
    /// as YAML outside the editor). Run "Wavekeep/Setup Task 06 (Shop)" after the Task 01–05 setups,
    /// then save the scene. Editor-only; not part of the runtime build.
    /// </summary>
    public static class Task06SceneSetup
    {
        private const string ConsumableFolder = "Assets/Data/Consumables";

        [MenuItem("Wavekeep/Setup Task 06 (Shop)")]
        public static void SetupScene()
        {
            var bootstrap = Object.FindFirstObjectByType<GameSessionBootstrap>();
            var waveSpawner = Object.FindFirstObjectByType<WaveSpawner>();
            var wall = Object.FindFirstObjectByType<WallRuntime>();
            var canvas = Object.FindFirstObjectByType<Canvas>();
            if (bootstrap == null || waveSpawner == null || wall == null || canvas == null)
            {
                Debug.LogError("[Task06SceneSetup] Missing GameSessionBootstrap / WaveSpawner / WallRuntime / Canvas. Run the Task 01–05 setups first.");
                return;
            }

            // --- Author the fixed shop assortment (prices tuned to be reachable from early kills). ---
            EnsureFolder("Assets/Data", "Consumables");
            var consumables = new List<ConsumableDefinitionSO>
            {
                CreateConsumable("SharpElixir", "Sharp Elixir", "Permanent +10 ability damage.",
                    price: 10, stackable: true, ConsumableEffectType.FlatDamageBoost, value: 10f, duration: 0f),
                CreateConsumable("SwiftTonic", "Swift Tonic", "Permanent ability cooldown ×0.7.",
                    price: 20, stackable: true, ConsumableEffectType.CooldownReduction, value: 0.7f, duration: 0f),
                CreateConsumable("WallRepairKit", "Wall Repair Kit", "Instantly restore 100 wall HP.",
                    price: 15, stackable: true, ConsumableEffectType.HealWall, value: 100f, duration: 0f),
                CreateConsumable("GreaterWhetstone", "Greater Whetstone", "Permanent +30 ability damage. One per run.",
                    price: 40, stackable: false, ConsumableEffectType.FlatDamageBoost, value: 30f, duration: 0f),
            };
            AssetDatabase.SaveAssets();

            // --- Scene cleanup (idempotent). ---
            DestroyIfExists("ShopPanel");
            DestroyIfExists("Shop");

            // --- Shop UI frame. ---
            BuildShopScreen(canvas, bootstrap, waveSpawner, wall, consumables);

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log("[Task06SceneSetup] Shop built. Play: clear wave 1, the shop opens between waves — buy an item, then Continue to start wave 2. Save the scene (Ctrl+S).");
        }

        private static ConsumableDefinitionSO CreateConsumable(
            string fileName, string displayName, string description,
            int price, bool stackable, ConsumableEffectType effectType, float value, float duration)
        {
            string path = $"{ConsumableFolder}/{fileName}.asset";
            var asset = AbilityAssetUtil.LoadOrCreate<ConsumableDefinitionSO>(path);
            var so = new SerializedObject(asset);
            so.FindProperty("_displayName").stringValue = displayName;
            so.FindProperty("_description").stringValue = description;
            so.FindProperty("_price").intValue = price;
            so.FindProperty("_stackable").boolValue = stackable;
            so.FindProperty("_effectType").enumValueIndex = (int)effectType;
            so.FindProperty("_effectValue").floatValue = value;
            so.FindProperty("_duration").floatValue = duration;
            so.ApplyModifiedPropertiesWithoutUndo();
            return asset;
        }

        private static void BuildShopScreen(
            Canvas canvas, GameSessionBootstrap bootstrap, WaveSpawner waveSpawner, WallRuntime wall,
            List<ConsumableDefinitionSO> consumables)
        {
            if (TMP_Settings.defaultFontAsset == null)
            {
                Debug.LogWarning("[Task06SceneSetup] TMP has no default font asset. If shop text doesn't render, " +
                                 "import via Window > TextMeshPro > Import TMP Essential Resources, then re-run.");
            }

            // Full-screen dimmed panel (controller hides it on Start; shown only between waves).
            var panel = new GameObject("ShopPanel", typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(canvas.transform, false);
            var prt = (RectTransform)panel.transform;
            prt.anchorMin = Vector2.zero;
            prt.anchorMax = Vector2.one;
            prt.offsetMin = Vector2.zero;
            prt.offsetMax = Vector2.zero;
            panel.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.75f);

            // Title.
            CreateText(panel.transform, "Title", "Shop", 48f, TextAlignmentOptions.Center,
                new Vector2(0.5f, 1f), new Vector2(0f, -40f), new Vector2(600f, 70f));

            // Currency readout (top-left of the panel).
            var currencyText = CreateText(panel.transform, "ShopCurrencyText", "Currency: 0", 28f,
                TextAlignmentOptions.Left, new Vector2(0f, 1f), new Vector2(40f, -120f), new Vector2(400f, 40f));

            // Item container with a vertical layout — the controller fills it at runtime.
            var container = new GameObject("ItemContainer", typeof(RectTransform), typeof(VerticalLayoutGroup));
            container.transform.SetParent(panel.transform, false);
            var crt = (RectTransform)container.transform;
            crt.anchorMin = new Vector2(0.5f, 0.5f);
            crt.anchorMax = new Vector2(0.5f, 0.5f);
            crt.pivot = new Vector2(0.5f, 0.5f);
            crt.anchoredPosition = new Vector2(0f, 0f);
            crt.sizeDelta = new Vector2(600f, 360f);
            var vlg = container.GetComponent<VerticalLayoutGroup>();
            vlg.spacing = 10f;
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.childControlWidth = false;
            vlg.childControlHeight = false;
            vlg.childForceExpandWidth = false;
            vlg.childForceExpandHeight = false;

            // Continue button (bottom-centre).
            var continueButton = CreateButton(panel.transform, "ContinueButton", "Continue",
                new Vector2(0.5f, 0f), new Vector2(0f, 60f), new Vector2(220f, 56f));

            // Controller on a separate root object so hiding the panel doesn't disable it.
            var shopGo = new GameObject("Shop", typeof(ShopScreenController));
            var controller = shopGo.GetComponent<ShopScreenController>();
            var so = new SerializedObject(controller);
            so.FindProperty("_bootstrap").objectReferenceValue = bootstrap;
            so.FindProperty("_waveSpawner").objectReferenceValue = waveSpawner;
            so.FindProperty("_wall").objectReferenceValue = wall;
            so.FindProperty("_shopPanel").objectReferenceValue = panel;
            so.FindProperty("_itemContainer").objectReferenceValue = crt;
            so.FindProperty("_currencyText").objectReferenceValue = currencyText;
            so.FindProperty("_continueButton").objectReferenceValue = continueButton;

            var listProp = so.FindProperty("_availableConsumables");
            listProp.arraySize = consumables.Count;
            for (int i = 0; i < consumables.Count; i++)
            {
                listProp.GetArrayElementAtIndex(i).objectReferenceValue = consumables[i];
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static TextMeshProUGUI CreateText(
            Transform parent, string name, string text, float fontSize, TextAlignmentOptions alignment,
            Vector2 anchor, Vector2 anchoredPosition, Vector2 size)
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
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = anchor;
            rt.anchoredPosition = anchoredPosition;
            rt.sizeDelta = size;
            return tmp;
        }

        private static Button CreateButton(
            Transform parent, string name, string label, Vector2 anchor, Vector2 anchoredPosition, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = anchor;
            rt.anchoredPosition = anchoredPosition;
            rt.sizeDelta = size;

            var labelGo = new GameObject("Label", typeof(RectTransform));
            labelGo.transform.SetParent(go.transform, false);
            var tmp = labelGo.AddComponent<TextMeshProUGUI>();
            tmp.text = label;
            tmp.fontSize = 24f;
            tmp.color = Color.black;
            tmp.alignment = TextAlignmentOptions.Center;
            if (TMP_Settings.defaultFontAsset != null) tmp.font = TMP_Settings.defaultFontAsset;
            var lrt = tmp.rectTransform;
            lrt.anchorMin = Vector2.zero;
            lrt.anchorMax = Vector2.one;
            lrt.offsetMin = Vector2.zero;
            lrt.offsetMax = Vector2.zero;

            return go.GetComponent<Button>();
        }

        private static void EnsureFolder(string parent, string child)
        {
            if (!AssetDatabase.IsValidFolder($"{parent}/{child}"))
            {
                AssetDatabase.CreateFolder(parent, child);
            }
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
