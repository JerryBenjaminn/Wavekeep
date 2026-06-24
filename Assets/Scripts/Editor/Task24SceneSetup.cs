#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Wavekeep.Core;
using Wavekeep.Data;
using Wavekeep.UI;

namespace Wavekeep.EditorTools
{
    /// <summary>
    /// Authors the Task 24 Luck content and wires it into the scene (scenes aren't hand-authored as YAML):
    /// <list type="number">
    /// <item>Creates a <see cref="TierWeightingConfigSO"/> with placeholder tuning and assigns it to BOTH
    ///   the <see cref="GameSessionBootstrap"/> (drives loot weighting + the per-run LuckState) and the
    ///   <see cref="ShopScreenController"/> (drives the offer-tier draw).</item>
    /// <item>Creates three Luck Potion <see cref="ConsumableDefinitionSO"/> assets (I/II/III, +5/10/15
    ///   Luck, placeholder prices) and APPENDS them to the shop's existing draw pool — same authoring
    ///   pattern as Task 23, same <c>ShopController.TryPurchase</c> path.</item>
    /// <item>Seeds a small <c>luckBonus</c> on the Task 12 sample gear/artifact so the gear-derived Luck
    ///   source is observable in a playtest.</item>
    /// </list>
    ///
    /// Run "Wavekeep/Setup Task 24 (Luck System)" AFTER the Task 06/09/13/22/23 setups, then save the
    /// scene. Idempotent — safe to re-run. Editor-only.
    ///
    /// Documented placeholder tuning (all to be balanced later): Luck Potions +5/10/15 (prices 20/35/50);
    /// config maxLuck 100, luckWeight 0.75 &gt; waveWeight 0.25, waveProgressMaxWave 20, shopStrength 4,
    /// shop base tier odds 6/3/1 (T1/T2/T3), lootStrengthMultiplier 0.25 (loot ≈ a quarter as strong).
    /// </summary>
    public static class Task24SceneSetup
    {
        private const string ConfigFolder = "Assets/Data/Config";
        private const string ConsumableFolder = "Assets/Data/Consumables";
        private const string GearFolder = "Assets/Data/Gear";
        private const string ConfigPath = ConfigFolder + "/TierWeightingConfig.asset";

        [MenuItem("Wavekeep/Setup Task 24 (Luck System)")]
        public static void SetupScene()
        {
            var bootstrap = Object.FindFirstObjectByType<GameSessionBootstrap>();
            var shop = Object.FindFirstObjectByType<ShopScreenController>();
            if (bootstrap == null || shop == null)
            {
                Debug.LogError("[Task24SceneSetup] Missing GameSessionBootstrap and/or ShopScreenController. " +
                               "Run the Task 01/02 and Task 06/09 setups first.");
                return;
            }

            var config = CreateConfig();

            // Wire the config into both consumers (one shared asset = consistent odds across shop + loot).
            SetObjectField(bootstrap, "_tierWeightingConfig", config);
            SetObjectField(shop, "_tierWeightingConfig", config);

            // Three Luck Potions on the existing TryPurchase path; appended to the shop's pool.
            var potions = new List<ConsumableDefinitionSO>
            {
                CreatePotion("LuckPotionT1", "Luck Potion I", 5f, 20, ConsumableTier.Tier1),
                CreatePotion("LuckPotionT2", "Luck Potion II", 10f, 35, ConsumableTier.Tier2),
                CreatePotion("LuckPotionT3", "Luck Potion III", 15f, 50, ConsumableTier.Tier3),
            };
            AppendToShopPool(shop, potions);

            // Seed gear Luck so the equipped-gear source is observable (placeholder magnitudes).
            SeedGearLuck("Gear_CommonHelm", 2f);
            SeedGearLuck("Gear_UncommonBody", 3f);
            SeedGearLuck("Gear_RareHands", 4f);
            SeedGearLuck("Gear_EpicLegs", 5f);
            SeedGearLuck("Gear_LegendaryFeet", 7f);
            SeedGearLuck("Artifact_UniqueCore", 10f);

            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log("[Task24SceneSetup] Luck system wired: TierWeightingConfig created + assigned, 3 Luck " +
                      "Potions added to the shop pool, sample gear seeded with luckBonus. Play → open the stat " +
                      "panel (Tab) to see Luck; buy Luck Potions / equip gear and reroll the shop to see tier " +
                      "odds shift. Save the scene (Ctrl+S).");
        }

        private static TierWeightingConfigSO CreateConfig()
        {
            EnsureFolder("Assets/Data", "Config");
            var config = AbilityAssetUtil.LoadOrCreate<TierWeightingConfigSO>(ConfigPath);

            // Author placeholder tuning. (LoadOrCreate keeps existing edits on re-run? No — we re-set the
            // defaults each run so the asset reflects this task's documented baseline; edit afterwards to tune.)
            var so = new SerializedObject(config);
            so.FindProperty("_maxLuck").floatValue = 100f;
            so.FindProperty("_luckWeight").floatValue = 0.75f;
            so.FindProperty("_waveWeight").floatValue = 0.25f;
            so.FindProperty("_waveProgressMaxWave").intValue = 20;
            so.FindProperty("_shopStrength").floatValue = 4f;
            so.FindProperty("_lootStrengthMultiplier").floatValue = 0.25f;

            var weights = so.FindProperty("_shopBaseTierWeights");
            weights.arraySize = 3;
            weights.GetArrayElementAtIndex(0).floatValue = 6f; // Tier1
            weights.GetArrayElementAtIndex(1).floatValue = 3f; // Tier2
            weights.GetArrayElementAtIndex(2).floatValue = 1f; // Tier3
            so.ApplyModifiedPropertiesWithoutUndo();
            return config;
        }

        private static ConsumableDefinitionSO CreatePotion(
            string fileName, string displayName, float luck, int price, ConsumableTier tier)
        {
            string path = $"{ConsumableFolder}/{fileName}.asset";
            var asset = AbilityAssetUtil.LoadOrCreate<ConsumableDefinitionSO>(path);
            var so = new SerializedObject(asset);
            so.FindProperty("_displayName").stringValue = displayName;
            so.FindProperty("_description").stringValue =
                $"+{luck:0} Luck for the rest of the run. Improves shop offer tiers (and, weakly, loot tiers).";
            so.FindProperty("_price").intValue = price;
            so.FindProperty("_stackable").boolValue = true;
            so.FindProperty("_tier").enumValueIndex = (int)tier;
            so.FindProperty("_effectType").enumValueIndex = (int)ConsumableEffectType.LuckBoost;
            so.FindProperty("_effectValue").floatValue = luck;
            so.FindProperty("_duration").floatValue = 0f; // run-long; LuckState resets the potion portion at run end
            so.ApplyModifiedPropertiesWithoutUndo();
            return asset;
        }

        private static void AppendToShopPool(ShopScreenController shop, List<ConsumableDefinitionSO> newItems)
        {
            var cso = new SerializedObject(shop);
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
        }

        private static void SeedGearLuck(string fileName, float luck)
        {
            var item = AssetDatabase.LoadAssetAtPath<LootItemSO>($"{GearFolder}/{fileName}.asset");
            if (item == null)
            {
                Debug.LogWarning($"[Task24SceneSetup] Gear asset '{fileName}' not found; skipping luck seed " +
                                 "(run 'Wavekeep/Setup Task 12 (Gear Core)' first to author it).");
                return;
            }
            var so = new SerializedObject(item);
            so.FindProperty("_luckBonus").floatValue = luck;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetObjectField(Object target, string propertyName, Object value)
        {
            var so = new SerializedObject(target);
            so.FindProperty(propertyName).objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void EnsureFolder(string parent, string child)
        {
            if (!AssetDatabase.IsValidFolder($"{parent}/{child}")) AssetDatabase.CreateFolder(parent, child);
        }
    }
}
#endif
