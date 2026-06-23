#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Wavekeep.Data;
using Wavekeep.UI;

namespace Wavekeep.EditorTools
{
    /// <summary>
    /// Authors the Task 23 consumable types (Crit Chance, Crit Damage, Frost, Lightning, Ultimate
    /// Duration, Basic Attack Damage — each across T1/T2/T3) as <see cref="ConsumableDefinitionSO"/>
    /// assets and APPENDS them to the existing shop's draw pool, leaving the Task 06/09 items in place.
    /// All effects ride the existing <c>ShopController.TryPurchase</c> → <c>ConsumableInventory</c> →
    /// <c>AbilityRuntime</c> pipeline — no new purchase code.
    ///
    /// Built in code like the other task setups. Run "Wavekeep/Setup Task 23 (Shop: Crit &amp; New
    /// Potions)" AFTER the Task 06/09 shop setups, then save the scene. Editor-only.
    ///
    /// Documented tuning (per-run permanent effects, all stackable, prices T1/T2/T3 = 18/30/45):
    /// Crit Chance +5/10/15%; Crit Damage +25/40/60%; Frost per-stack slow +2/4/6%; Lightning flat
    /// damage +4/8/12 (placeholder, applies to all abilities); Ultimate duration +2/4/6s; Basic damage +5/10/15.
    /// </summary>
    public static class Task23SceneSetup
    {
        private const string ConsumableFolder = "Assets/Data/Consumables";

        [MenuItem("Wavekeep/Setup Task 23 (Shop: Crit & New Potions)")]
        public static void SetupScene()
        {
            var controller = UnityEngine.Object.FindFirstObjectByType<ShopScreenController>();
            if (controller == null)
            {
                Debug.LogError("[Task23SceneSetup] No ShopScreenController in scene. Run 'Wavekeep/Setup Task 06 (Shop)' (and Task 09) first.");
                return;
            }

            var newItems = new List<ConsumableDefinitionSO>();

            CreateFamily(newItems, "CritChancePotion", "Crit Chance Potion", ConsumableEffectType.CritChanceBoost,
                0.05f, 0.10f, 0.15f, v => $"+{v * 100f:0}% crit chance (permanent).");
            CreateFamily(newItems, "CritDamagePotion", "Crit Damage Potion", ConsumableEffectType.CritDamageBoost,
                0.25f, 0.40f, 0.60f, v => $"+{v * 100f:0}% crit damage (permanent).");
            CreateFamily(newItems, "FrostPotion", "Frost Potion", ConsumableEffectType.FrostPotency,
                0.02f, 0.04f, 0.06f, v => $"+{v * 100f:0}% frost slow per stack. No effect for non-frost heroes.");
            CreateFamily(newItems, "LightningPotion", "Lightning Potion", ConsumableEffectType.ElementalLightning,
                4f, 8f, 12f, v => $"+{v:0} ability damage (Lightning — placeholder until a Lightning hero exists).");
            CreateFamily(newItems, "UltimateDurationPotion", "Ultimate Duration Potion", ConsumableEffectType.UltimateDurationBoost,
                2f, 4f, 6f, v => $"+{v:0}s ultimate zone duration (permanent).");
            CreateFamily(newItems, "BasicDamagePotion", "Basic Attack Damage Potion", ConsumableEffectType.BasicDamageBoost,
                5f, 10f, 15f, v => $"+{v:0} basic attack damage (permanent).");

            AssetDatabase.SaveAssets();

            // Merge into the existing pool (keep Task 06/09 items; add ours; dedup → idempotent re-run).
            var cso = new SerializedObject(controller);
            var listProp = cso.FindProperty("_availableConsumables");
            var pool = new List<ConsumableDefinitionSO>();
            for (int i = 0; i < listProp.arraySize; i++)
            {
                if (listProp.GetArrayElementAtIndex(i).objectReferenceValue is ConsumableDefinitionSO existing
                    && !pool.Contains(existing))
                {
                    pool.Add(existing);
                }
            }
            foreach (var item in newItems)
            {
                if (item != null && !pool.Contains(item)) pool.Add(item);
            }

            listProp.arraySize = pool.Count;
            for (int i = 0; i < pool.Count; i++)
            {
                listProp.GetArrayElementAtIndex(i).objectReferenceValue = pool[i];
            }
            cso.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log($"[Task23SceneSetup] Added {newItems.Count} new consumables to the shop pool " +
                      $"(now {pool.Count} items). Play → reach the shop → buy the new potions. Save the scene (Ctrl+S).");
        }

        private static void CreateFamily(List<ConsumableDefinitionSO> sink, string baseFile, string baseName,
            ConsumableEffectType type, float v1, float v2, float v3, Func<float, string> describe)
        {
            sink.Add(CreateConsumable(baseFile + "T1", baseName + " I", describe(v1), 18, ConsumableTier.Tier1, type, v1));
            sink.Add(CreateConsumable(baseFile + "T2", baseName + " II", describe(v2), 30, ConsumableTier.Tier2, type, v2));
            sink.Add(CreateConsumable(baseFile + "T3", baseName + " III", describe(v3), 45, ConsumableTier.Tier3, type, v3));
        }

        private static ConsumableDefinitionSO CreateConsumable(string fileName, string displayName,
            string description, int price, ConsumableTier tier, ConsumableEffectType effectType, float effectValue)
        {
            string path = $"{ConsumableFolder}/{fileName}.asset";
            var asset = AbilityAssetUtil.LoadOrCreate<ConsumableDefinitionSO>(path);
            var so = new SerializedObject(asset);
            so.FindProperty("_displayName").stringValue = displayName;
            so.FindProperty("_description").stringValue = description;
            so.FindProperty("_price").intValue = price;
            so.FindProperty("_stackable").boolValue = true;
            so.FindProperty("_tier").enumValueIndex = (int)tier;
            so.FindProperty("_effectType").enumValueIndex = (int)effectType;
            so.FindProperty("_effectValue").floatValue = effectValue;
            so.FindProperty("_duration").floatValue = 0f; // permanent for the run (clear to observe in a playtest)
            so.ApplyModifiedPropertiesWithoutUndo();
            return asset;
        }
    }
}
#endif
