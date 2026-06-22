#if UNITY_EDITOR
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Wavekeep.Data;
using Wavekeep.UI;

namespace Wavekeep.EditorTools
{
    /// <summary>
    /// Extends the Task 06 shop with Task 09 tiers + reroll: assigns tiers to the existing consumable
    /// assets, authors the three Reroll Potion variants (T1/T2/T3 → +1/+2/+3 reroll points), and
    /// augments the existing shop Canvas with a Reroll button, re-wiring the
    /// <see cref="ShopScreenController"/> to the full 7-item pool + reroll controls.
    ///
    /// Built in code like Tasks 01–08. Run "Wavekeep/Setup Task 09 (Shop Tiers &amp; Reroll)" AFTER the
    /// Task 06 shop setup (it augments that shop in place), then save the scene. Editor-only.
    ///
    /// Tier assignments (documented): Sharp Elixir = T1, Wall Repair Kit = T1, Swift Tonic = T2,
    /// Greater Whetstone = T3. Reroll Potions: T1=+1, T2=+2, T3=+3.
    /// </summary>
    public static class Task09SceneSetup
    {
        private const string ConsumableFolder = "Assets/Data/Consumables";

        // Existing Task 06 assets (load by path to set tiers).
        private const string SharpElixirPath = ConsumableFolder + "/SharpElixir.asset";
        private const string SwiftTonicPath = ConsumableFolder + "/SwiftTonic.asset";
        private const string WallRepairKitPath = ConsumableFolder + "/WallRepairKit.asset";
        private const string GreaterWhetstonePath = ConsumableFolder + "/GreaterWhetstone.asset";

        private const int OfferSize = 4; // = Task 06's per-visit display count (unchanged this task)

        [MenuItem("Wavekeep/Setup Task 09 (Shop Tiers & Reroll)")]
        public static void SetupScene()
        {
            var controller = Object.FindFirstObjectByType<ShopScreenController>();
            if (controller == null)
            {
                Debug.LogError("[Task09SceneSetup] No ShopScreenController in scene. Run 'Wavekeep/Setup Task 06 (Shop)' first.");
                return;
            }

            // --- Tier the existing Task 06 consumables. ---
            SetTier(SharpElixirPath, ConsumableTier.Tier1);
            SetTier(WallRepairKitPath, ConsumableTier.Tier1);
            SetTier(SwiftTonicPath, ConsumableTier.Tier2);
            SetTier(GreaterWhetstonePath, ConsumableTier.Tier3);

            // --- Author the three Reroll Potion variants (the only new item content this task). ---
            var rerollT1 = CreateRerollPotion("RerollPotionT1", "Reroll Potion I", "+1 reroll point.", 10, ConsumableTier.Tier1, 1);
            var rerollT2 = CreateRerollPotion("RerollPotionT2", "Reroll Potion II", "+2 reroll points.", 20, ConsumableTier.Tier2, 2);
            var rerollT3 = CreateRerollPotion("RerollPotionT3", "Reroll Potion III", "+3 reroll points.", 35, ConsumableTier.Tier3, 3);
            AssetDatabase.SaveAssets();

            // --- Build the full draw pool: existing 4 + 3 reroll potions. ---
            var pool = new List<ConsumableDefinitionSO>();
            AddIfExists(pool, SharpElixirPath);
            AddIfExists(pool, SwiftTonicPath);
            AddIfExists(pool, WallRepairKitPath);
            AddIfExists(pool, GreaterWhetstonePath);
            pool.Add(rerollT1);
            pool.Add(rerollT2);
            pool.Add(rerollT3);

            // --- Augment the existing shop panel with a Reroll button + re-wire the controller. ---
            var cso = new SerializedObject(controller);
            var panel = cso.FindProperty("_shopPanel").objectReferenceValue as GameObject;
            if (panel == null)
            {
                Debug.LogError("[Task09SceneSetup] ShopScreenController has no _shopPanel wired. Re-run Task 06 setup.");
                return;
            }

            DestroyChildIfExists(panel.transform, "RerollButton");
            var rerollButton = CreateButton(panel.transform, "RerollButton", "Reroll (3)",
                new Vector2(0.5f, 0f), new Vector2(-280f, 60f), new Vector2(220f, 56f), out var rerollLabel);

            // Re-wire pool, offer size, and the reroll controls onto the existing controller.
            var listProp = cso.FindProperty("_availableConsumables");
            listProp.arraySize = pool.Count;
            for (int i = 0; i < pool.Count; i++)
            {
                listProp.GetArrayElementAtIndex(i).objectReferenceValue = pool[i];
            }
            cso.FindProperty("_offerSize").intValue = OfferSize;
            cso.FindProperty("_rerollButton").objectReferenceValue = rerollButton;
            cso.FindProperty("_rerollCountLabel").objectReferenceValue = rerollLabel;
            cso.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log("[Task09SceneSetup] Tiers + reroll wired. Play: reach the shop → see Reroll (3) → reroll to swap items / buy a Reroll Potion to gain points. Play Again resets to 3. Save the scene (Ctrl+S).");
        }

        private static void SetTier(string path, ConsumableTier tier)
        {
            var asset = AssetDatabase.LoadAssetAtPath<ConsumableDefinitionSO>(path);
            if (asset == null)
            {
                Debug.LogWarning($"[Task09SceneSetup] Consumable not found at {path}; skipping tier assignment. Run Task 06 setup first.");
                return;
            }
            var so = new SerializedObject(asset);
            so.FindProperty("_tier").enumValueIndex = (int)tier;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static ConsumableDefinitionSO CreateRerollPotion(
            string fileName, string displayName, string description, int price, ConsumableTier tier, int rerollAmount)
        {
            string path = $"{ConsumableFolder}/{fileName}.asset";
            var asset = AbilityAssetUtil.LoadOrCreate<ConsumableDefinitionSO>(path);
            var so = new SerializedObject(asset);
            so.FindProperty("_displayName").stringValue = displayName;
            so.FindProperty("_description").stringValue = description;
            so.FindProperty("_price").intValue = price;
            so.FindProperty("_stackable").boolValue = true;
            so.FindProperty("_tier").enumValueIndex = (int)tier;
            so.FindProperty("_effectType").enumValueIndex = (int)ConsumableEffectType.GainRerollPoints;
            so.FindProperty("_effectValue").floatValue = rerollAmount;
            so.FindProperty("_duration").floatValue = 0f;
            so.ApplyModifiedPropertiesWithoutUndo();
            return asset;
        }

        private static Button CreateButton(
            Transform parent, string name, string label, Vector2 anchor, Vector2 anchoredPosition, Vector2 size,
            out TextMeshProUGUI labelText)
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
            labelText = labelGo.AddComponent<TextMeshProUGUI>();
            labelText.text = label;
            labelText.fontSize = 24f;
            labelText.color = Color.black;
            labelText.alignment = TextAlignmentOptions.Center;
            if (TMP_Settings.defaultFontAsset != null) labelText.font = TMP_Settings.defaultFontAsset;
            var lrt = labelText.rectTransform;
            lrt.anchorMin = Vector2.zero;
            lrt.anchorMax = Vector2.one;
            lrt.offsetMin = Vector2.zero;
            lrt.offsetMax = Vector2.zero;

            return go.GetComponent<Button>();
        }

        private static void AddIfExists(List<ConsumableDefinitionSO> list, string path)
        {
            var asset = AssetDatabase.LoadAssetAtPath<ConsumableDefinitionSO>(path);
            if (asset != null) list.Add(asset);
            else Debug.LogWarning($"[Task09SceneSetup] Consumable not found at {path}; omitted from pool. Run Task 06 setup first.");
        }

        private static void DestroyChildIfExists(Transform parent, string childName)
        {
            var existing = parent.Find(childName);
            if (existing != null) Object.DestroyImmediate(existing.gameObject);
        }
    }
}
#endif
