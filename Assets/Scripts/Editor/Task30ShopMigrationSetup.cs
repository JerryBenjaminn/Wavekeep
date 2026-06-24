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
    /// Task 30 — migrates the old §3.8 shared generic level-up pool (retired in Task 29) into shop
    /// consumables. The old pool (see Task07SceneSetup) offered three distinct generic STAT effects:
    /// flat damage, AoE radius, and cooldown reduction. This authors them as <see cref="ConsumableDefinitionSO"/>
    /// across T1/T2/T3, REUSING the equivalent potions that already exist (no duplicates) and creating only
    /// the missing tiers, then appends the new items to the open scene's shop pool.
    ///
    /// Coverage (reused → created):
    /// <list type="bullet">
    /// <item>Flat damage (FlatDamageBoost): T1 Sharp Elixir + T3 Greater Whetstone already exist → create T2.</item>
    /// <item>Cooldown (CooldownReduction): T2 Swift Tonic already exists → create T1 + T3.</item>
    /// <item>AoE radius (AoeRadiusBoost, new effect type this task): create T1/T2/T3 (none existed).</item>
    /// </list>
    /// All effects flow through the existing AbilityRuntime ComputeStats pipeline and the normal
    /// <c>ShopController.TryPurchase</c> path — no parallel calculation/purchase path.
    ///
    /// Run "Wavekeep/Setup Task 30 (Shop Migration)" from the gameplay scene (so the shop pool is wired),
    /// then save the scene. Idempotent. Editor-only.
    /// </summary>
    public static class Task30ShopMigrationSetup
    {
        private const string ConsumableFolder = "Assets/Data/Consumables";

        [MenuItem("Wavekeep/Setup Task 30 (Shop Migration)")]
        public static void SetupScene()
        {
            EnsureFolder("Assets/Data", "Consumables");

            // Gap-filling tiers for the two already-partly-covered effects + the full new AoE-radius set.
            var created = new List<ConsumableDefinitionSO>
            {
                // Flat damage — fills the missing Tier 2 between Sharp Elixir (T1, +10) and Greater Whetstone (T3, +30).
                Create("DamageElixirT2", "Honed Elixir", "Permanent +20 ability damage.",
                    ConsumableEffectType.FlatDamageBoost, 20f, price: 25, ConsumableTier.Tier2),

                // Cooldown — fills the missing T1 + T3 around Swift Tonic (T2, ×0.7). Lower multiplier = faster.
                Create("HasteTonicT1", "Brisk Tonic", "Permanent ability cooldown ×0.85.",
                    ConsumableEffectType.CooldownReduction, 0.85f, price: 12, ConsumableTier.Tier1),
                Create("HasteTonicT3", "Alacrity Tonic", "Permanent ability cooldown ×0.55.",
                    ConsumableEffectType.CooldownReduction, 0.55f, price: 45, ConsumableTier.Tier3),

                // AoE radius — the migrated generic burst-size upgrade (new effect type), full T1/T2/T3 set.
                Create("BlastRadiusPotionT1", "Blast Radius Potion I", "+1m ability AoE/blast radius (permanent).",
                    ConsumableEffectType.AoeRadiusBoost, 1f, price: 15, ConsumableTier.Tier1),
                Create("BlastRadiusPotionT2", "Blast Radius Potion II", "+2m ability AoE/blast radius (permanent).",
                    ConsumableEffectType.AoeRadiusBoost, 2f, price: 28, ConsumableTier.Tier2),
                Create("BlastRadiusPotionT3", "Blast Radius Potion III", "+3m ability AoE/blast radius (permanent).",
                    ConsumableEffectType.AoeRadiusBoost, 3f, price: 45, ConsumableTier.Tier3),
            };

            AssetDatabase.SaveAssets();

            // Reused existing tiers that complete each migrated effect's T1/T2/T3 set — added to the pool too
            // (if not already present) so the full set rotates through the shop.
            var reused = new List<ConsumableDefinitionSO>();
            AddIfFound(reused, $"{ConsumableFolder}/SharpElixir.asset");      // FlatDamageBoost T1
            AddIfFound(reused, $"{ConsumableFolder}/GreaterWhetstone.asset"); // FlatDamageBoost T3
            AddIfFound(reused, $"{ConsumableFolder}/SwiftTonic.asset");       // CooldownReduction T2

            var all = new List<ConsumableDefinitionSO>(created);
            all.AddRange(reused);
            AppendToShopPool(all);

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log($"[Task30] Generic-pool migration complete: {created.Count} new consumables created " +
                      "(Honed Elixir; Brisk/Alacrity Tonic; Blast Radius Potion I/II/III) + 3 existing tiers " +
                      "reused (Sharp Elixir, Greater Whetstone, Swift Tonic). Added to the shop pool. Save the " +
                      "scene (Ctrl+S). Note: flat-damage/cooldown were already partly covered; only gap tiers added.");
        }

        private static ConsumableDefinitionSO Create(
            string fileName, string displayName, string description,
            ConsumableEffectType effect, float value, int price, ConsumableTier tier)
        {
            var asset = AbilityAssetUtil.LoadOrCreate<ConsumableDefinitionSO>($"{ConsumableFolder}/{fileName}.asset");
            var so = new SerializedObject(asset);
            so.FindProperty("_displayName").stringValue = displayName;
            so.FindProperty("_description").stringValue = description;
            so.FindProperty("_price").intValue = price;
            so.FindProperty("_stackable").boolValue = true; // generic stat upgrades stacked in the old pool
            so.FindProperty("_tier").enumValueIndex = (int)tier;
            so.FindProperty("_effectType").enumValueIndex = (int)effect;
            so.FindProperty("_effectValue").floatValue = value;
            so.FindProperty("_duration").floatValue = 0f; // permanent run-bonus
            so.ApplyModifiedPropertiesWithoutUndo();
            return asset;
        }

        private static void AddIfFound(List<ConsumableDefinitionSO> list, string path)
        {
            var asset = AssetDatabase.LoadAssetAtPath<ConsumableDefinitionSO>(path);
            if (asset != null) list.Add(asset);
            else Debug.LogWarning($"[Task30] Existing consumable not found at {path}; its tier won't be re-added " +
                                  "to the pool (it may already be wired, or run the Task 06 setup first).");
        }

        private static void AppendToShopPool(List<ConsumableDefinitionSO> items)
        {
            var shop = Object.FindFirstObjectByType<ShopScreenController>();
            if (shop == null)
            {
                Debug.LogWarning("[Task30] No ShopScreenController in the open scene — created the assets but " +
                                 "couldn't add them to a shop pool. Open the gameplay scene and re-run.");
                return;
            }

            var so = new SerializedObject(shop);
            var listProp = so.FindProperty("_availableConsumables");

            var present = new HashSet<Object>();
            for (int i = 0; i < listProp.arraySize; i++)
            {
                var obj = listProp.GetArrayElementAtIndex(i).objectReferenceValue;
                if (obj != null) present.Add(obj);
            }
            foreach (var item in items)
            {
                if (item == null || present.Contains(item)) continue;
                listProp.arraySize++;
                listProp.GetArrayElementAtIndex(listProp.arraySize - 1).objectReferenceValue = item;
                present.Add(item);
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void EnsureFolder(string parent, string child)
        {
            if (!AssetDatabase.IsValidFolder($"{parent}/{child}"))
                AssetDatabase.CreateFolder(parent, child);
        }
    }
}
#endif
