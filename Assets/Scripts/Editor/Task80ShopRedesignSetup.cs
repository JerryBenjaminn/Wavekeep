#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Wavekeep.Data;
using Wavekeep.UI;

namespace Wavekeep.EditorTools
{
    /// <summary>
    /// Task 80 — utility-only shop redesign. Authors the 7-item utility roster, rewires the SampleScene shop pool
    /// to it, and DELETES the 33 removed stat/luck/reroll consumable assets from Task 79's removal list. The shop
    /// itself becomes a boss-death "pick one free" reward (logic in WaveSpawner/ShopController/ShopScreenController);
    /// this setup only handles the DATA (assets + scene wiring).
    ///
    /// Idempotent: re-running re-authors the 7 assets, rewires the pool, and re-deletes any stray old consumable.
    /// Run "Wavekeep/Setup Task 80 (Utility Shop)" once. Editor-only.
    ///
    /// NOTE: the historical shop setups (Task 06/09/23/24/30) authored the OLD stat potions and are now superseded
    /// — do not re-run them, or they'll recreate removed assets and repopulate the pool.
    /// </summary>
    public static class Task80ShopRedesignSetup
    {
        private const string ConsumablesFolder = "Assets/Data/Consumables";
        private const string SampleScenePath = "Assets/Scenes/SampleScene.unity";
        private const int OfferSize = 4; // boss reward offers 4; player picks exactly one

        // The 7 retained/new utility items. Values are placeholders — FLAGGED for tuning (see summary).
        private struct Item
        {
            public string AssetName, DisplayName, Description;
            public ConsumableEffectType Effect;
            public ConsumableTier Tier;
            public float Value, Duration, AreaExtent;
        }

        private static readonly Item[] Roster =
        {
            // --- Wall utility ---
            new Item { AssetName = "WallRepairKit", DisplayName = "Wall Repair Kit", Tier = ConsumableTier.Tier1,
                Effect = ConsumableEffectType.HealWall, Value = 150f, Duration = 0f,
                Description = "Instantly restore 150 wall HP." },
            new Item { AssetName = "ReinforcedRepair", DisplayName = "Reinforced Repair", Tier = ConsumableTier.Tier2,
                Effect = ConsumableEffectType.HealWall, Value = 400f, Duration = 0f,
                Description = "Instantly restore a large amount of wall HP (400)." },
            new Item { AssetName = "ReinforcedBarricade", DisplayName = "Reinforced Barricade", Tier = ConsumableTier.Tier2,
                Effect = ConsumableEffectType.WallDamageReduction, Value = 0.4f, Duration = 30f,
                Description = "The wall takes 40% less damage for the next wave." },
            new Item { AssetName = "AegisShield", DisplayName = "Aegis Shield", Tier = ConsumableTier.Tier3,
                Effect = ConsumableEffectType.WallShield, Value = 250f, Duration = 30f,
                Description = "The wall gains a 250 HP shield that absorbs damage during the next wave." },

            // --- Arena control ---
            new Item { AssetName = "TarField", DisplayName = "Tar Field", Tier = ConsumableTier.Tier1,
                Effect = ConsumableEffectType.ArenaSlowZone, Value = 0.4f, Duration = 25f, AreaExtent = 16f,
                Description = "Slows enemies crossing the lane by 40% for the next wave." },
            new Item { AssetName = "GlacialChoke", DisplayName = "Glacial Choke", Tier = ConsumableTier.Tier2,
                Effect = ConsumableEffectType.ArenaFreezeZone, Value = 0f, Duration = 10f, AreaExtent = 4f,
                Description = "Freezes enemies in a band at the wall for the next wave (10s)." },
            new Item { AssetName = "FlashFreeze", DisplayName = "Flash Freeze", Tier = ConsumableTier.Tier3,
                Effect = ConsumableEffectType.FlashFreeze, Value = 0f, Duration = 3.5f,
                Description = "Freezes every enemy for ~3.5s at the start of the next wave." },
        };

        [MenuItem("Wavekeep/Setup Task 80 (Utility Shop)")]
        public static void Setup()
        {
            var keep = new HashSet<string>();
            var pool = new List<ConsumableDefinitionSO>();

            // 1) Author (create/update) the 7 utility items.
            foreach (var item in Roster)
            {
                var asset = AuthorItem(item);
                pool.Add(asset);
                keep.Add($"{ConsumablesFolder}/{item.AssetName}.asset");
            }

            // 2) Delete every OTHER consumable asset (the 33 removed by Task 79). Done before scene save so no
            //    dangling references remain once the pool is rewired to the kept set.
            int deleted = DeleteRemovedConsumables(keep);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // 3) Rewire the SampleScene shop pool to the 7 utility items.
            bool wired = RewireScenePool(pool);

            Debug.Log($"[Task80] Utility shop authored: {pool.Count} items kept, {deleted} old consumables deleted. " +
                      $"Scene pool rewired: {wired}. Boss-death 'pick one of {OfferSize}, free' shop is live " +
                      "(trigger + pick logic in WaveSpawner/ShopController). All values flagged for tuning.");
        }

        private static ConsumableDefinitionSO AuthorItem(Item item)
        {
            string path = $"{ConsumablesFolder}/{item.AssetName}.asset";
            var asset = AbilityAssetUtil.LoadOrCreate<ConsumableDefinitionSO>(path);
            var so = new SerializedObject(asset);
            so.FindProperty("_displayName").stringValue = item.DisplayName;
            so.FindProperty("_description").stringValue = item.Description;
            so.FindProperty("_price").intValue = 0;          // free pick — no currency cost (Task 80)
            so.FindProperty("_stackable").boolValue = false; // single pick per boss reward; irrelevant but tidy
            so.FindProperty("_tier").enumValueIndex = (int)item.Tier;
            so.FindProperty("_effectType").enumValueIndex = (int)item.Effect;
            so.FindProperty("_effectValue").floatValue = item.Value;
            so.FindProperty("_duration").floatValue = item.Duration;
            so.FindProperty("_areaExtent").floatValue = item.AreaExtent;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(asset);
            return asset;
        }

        private static int DeleteRemovedConsumables(HashSet<string> keepPaths)
        {
            int deleted = 0;
            var guids = AssetDatabase.FindAssets("t:ConsumableDefinitionSO", new[] { ConsumablesFolder });
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (keepPaths.Contains(path)) continue;
                if (AssetDatabase.DeleteAsset(path)) deleted++;
                else Debug.LogWarning($"[Task80] Could not delete '{path}'.");
            }
            return deleted;
        }

        private static bool RewireScenePool(List<ConsumableDefinitionSO> pool)
        {
            if (!System.IO.File.Exists(SampleScenePath))
            {
                Debug.LogWarning("[Task80] SampleScene not found; scene pool NOT rewired. Wire ShopScreenController manually.");
                return false;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return false;
            EditorSceneManager.OpenScene(SampleScenePath, OpenSceneMode.Single);

            var shop = Object.FindFirstObjectByType<ShopScreenController>();
            if (shop == null)
            {
                Debug.LogWarning("[Task80] No ShopScreenController in SampleScene; pool NOT rewired.");
                return false;
            }

            var so = new SerializedObject(shop);
            var listProp = so.FindProperty("_availableConsumables");
            listProp.arraySize = pool.Count;
            for (int i = 0; i < pool.Count; i++)
                listProp.GetArrayElementAtIndex(i).objectReferenceValue = pool[i];
            so.FindProperty("_offerSize").intValue = OfferSize;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(shop);

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            return true;
        }
    }
}
#endif
