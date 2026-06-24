#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Wavekeep.Data;
using Wavekeep.Runtime;

namespace Wavekeep.EditorTools
{
    /// <summary>
    /// Task 27 — test-data only. Fills out the gear/artifact catalog so EVERY (slot × rarity) combination
    /// exists (6 slots × 6 tiers = 36 items), which Task 26's stat-comparison needs in order to compare
    /// items of different tiers in the same slot. It is purely additive: it scans the existing assets
    /// (the Task 12 diagonal — Common Helm, Uncommon Body, … Unique Core), SKIPS any combination already
    /// authored, and creates only the missing ones — so existing assets are never duplicated or overwritten.
    ///
    /// Reuses Task 12's exact authoring pattern (SerializedObject writes via <see cref="AbilityAssetUtil"/>),
    /// adds no runtime code path, and touches NO loot-table / drop-rate / boss-exclusivity logic — the
    /// boss-exclusive lock governs how items DROP, not whether test assets may exist. New items are named
    /// to match the existing convention (Gear_{Tier}{SlotNoun} / Artifact_{Tier}Core) and given placeholder
    /// stats that scale with tier. Every item (existing + new) is registered in the <see cref="GearCatalogSO"/>
    /// so saved ids resolve and they can be granted via the debug tools.
    ///
    /// Run "Wavekeep/Setup Task 27 (Test Gear Population)" once (idempotent). Editor-only.
    /// </summary>
    public static class Task27GearPopulation
    {
        private const string GearFolder = "Assets/Data/Gear";
        private const string CatalogPath = GearFolder + "/GearCatalog.asset";

        // Slots in their armour order; Artifact is handled as ArtifactItemSO. Tiers follow the Rarity enum.
        private static readonly GearSlot[] Slots =
        {
            GearSlot.Helmet, GearSlot.Body, GearSlot.Hands, GearSlot.Legs, GearSlot.Feet, GearSlot.Artifact
        };

        private static readonly Rarity[] Tiers =
        {
            Rarity.Common, Rarity.Uncommon, Rarity.Rare, Rarity.Epic, Rarity.Legendary, Rarity.Unique
        };

        [MenuItem("Wavekeep/Setup Task 27 (Test Gear Population)")]
        public static void Populate()
        {
            if (!AssetDatabase.IsValidFolder(GearFolder))
            {
                Debug.LogError("[Task27] " + GearFolder + " does not exist. Run 'Wavekeep/Setup Task 12 (Gear Core)' first.");
                return;
            }

            // Map existing (slot, rarity) → asset, so we skip combos already authored (don't duplicate).
            var existing = ScanExisting(out var allItems);

            int created = 0;
            foreach (var slot in Slots)
            {
                foreach (var rarity in Tiers)
                {
                    var key = (slot, rarity);
                    if (existing.ContainsKey(key)) continue; // already covered by an existing asset

                    var item = CreateItem(slot, rarity);
                    existing[key] = item;
                    allItems.Add(item);
                    created++;
                }
            }

            RegisterInCatalog(allItems);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // Optional convenience: if a GearDebugController is in the OPEN scene, let its G key grant from
            // the full set so all tiers are reachable in a playtest. Pure data wiring — no code change.
            WireDebugController(allItems);

            Debug.Log($"[Task27] Test gear population complete: {created} new asset(s) created, " +
                      $"{allItems.Count}/36 (slot × tier) items now in the catalog. Existing assets untouched. " +
                      "Play → grant gear (debug G key) or open the Hub → inspect items of different tiers in the " +
                      "same slot to exercise the Task 26 comparison.");
        }

        // --- Scanning ------------------------------------------------------------------------------

        private static Dictionary<(GearSlot, Rarity), LootItemSO> ScanExisting(out List<LootItemSO> all)
        {
            var map = new Dictionary<(GearSlot, Rarity), LootItemSO>();
            all = new List<LootItemSO>();

            // OR'd type filter matches both concrete LootItemSO subclasses.
            var guids = AssetDatabase.FindAssets("t:GearItemSO t:ArtifactItemSO", new[] { GearFolder });
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var item = AssetDatabase.LoadAssetAtPath<LootItemSO>(path);
                if (item == null) continue;

                all.Add(item);
                var key = (item.Slot, item.Rarity);
                if (!map.ContainsKey(key)) map[key] = item; // first wins; duplicates are a pre-existing authoring choice
            }
            return map;
        }

        // --- Asset creation ------------------------------------------------------------------------

        private static LootItemSO CreateItem(GearSlot slot, Rarity rarity)
        {
            int tier = (int)rarity;                 // 0 = Common … 5 = Unique
            string noun = SlotNoun(slot);
            string fileName = slot == GearSlot.Artifact ? $"Artifact_{rarity}Core" : $"Gear_{rarity}{noun}";
            string itemId = slot == GearSlot.Artifact
                ? $"artifact_{rarity.ToString().ToLowerInvariant()}_core"
                : $"gear_{rarity.ToString().ToLowerInvariant()}_{noun.ToLowerInvariant()}";
            string itemName = slot == GearSlot.Artifact ? $"{rarity} Artifact" : $"{rarity} {slot}";

            var (modType, modValue) = StatFor(slot, tier);
            float luck = 1f + tier; // 1 → 6, scaling with tier

            if (slot == GearSlot.Artifact)
            {
                var asset = AbilityAssetUtil.LoadOrCreate<ArtifactItemSO>($"{GearFolder}/{fileName}.asset");
                var so = new SerializedObject(asset);
                WriteBaseFields(so, itemId, itemName, rarity, modType, modValue, luck);
                so.ApplyModifiedPropertiesWithoutUndo();
                return asset;
            }
            else
            {
                var asset = AbilityAssetUtil.LoadOrCreate<GearItemSO>($"{GearFolder}/{fileName}.asset");
                var so = new SerializedObject(asset);
                WriteBaseFields(so, itemId, itemName, rarity, modType, modValue, luck);
                so.FindProperty("_slot").enumValueIndex = (int)slot;
                so.ApplyModifiedPropertiesWithoutUndo();
                return asset;
            }
        }

        private static void WriteBaseFields(
            SerializedObject so, string itemId, string itemName, Rarity rarity,
            AbilityModifierType modType, float modValue, float luck)
        {
            so.FindProperty("_itemId").stringValue = itemId;
            so.FindProperty("_itemName").stringValue = itemName;
            so.FindProperty("_rarity").enumValueIndex = (int)rarity;
            so.FindProperty("_luckBonus").floatValue = luck;

            var mods = so.FindProperty("_statModifiers");
            mods.arraySize = 1;
            var element = mods.GetArrayElementAtIndex(0);
            element.FindPropertyRelative("_modifierType").enumValueIndex = (int)modType;
            element.FindPropertyRelative("_value").floatValue = modValue;
        }

        // One representative modifier per slot (matching the Task 12 diagonal's themes), scaled by tier so
        // values clearly differ across tiers — the point of this test data. Lower Cooldown = stronger.
        private static (AbilityModifierType type, float value) StatFor(GearSlot slot, int tier)
        {
            switch (slot)
            {
                case GearSlot.Helmet:   return (AbilityModifierType.DamageFlatBonus, 4f + 4f * tier);          // 4 … 24
                case GearSlot.Body:     return (AbilityModifierType.DamageMultiplier, Round(1.10f + 0.10f * tier)); // 1.10 … 1.60
                case GearSlot.Hands:    return (AbilityModifierType.CooldownMultiplier, Round(0.95f - 0.05f * tier)); // 0.95 … 0.70
                case GearSlot.Legs:     return (AbilityModifierType.RangeMultiplier, Round(1.10f + 0.10f * tier));    // 1.10 … 1.60
                case GearSlot.Feet:     return (AbilityModifierType.DamageFlatBonus, 6f + 4f * tier);          // 6 … 26
                case GearSlot.Artifact: return (AbilityModifierType.DamageMultiplier, Round(1.15f + 0.10f * tier)); // 1.15 … 1.65
                default:                return (AbilityModifierType.DamageFlatBonus, 5f + 5f * tier);
            }
        }

        private static float Round(float v) => Mathf.Round(v * 100f) / 100f;

        private static string SlotNoun(GearSlot slot)
        {
            // Matches the abbreviations the existing Task 12 assets use (Gear_CommonHelm, …).
            switch (slot)
            {
                case GearSlot.Helmet: return "Helm";
                case GearSlot.Body: return "Body";
                case GearSlot.Hands: return "Hands";
                case GearSlot.Legs: return "Legs";
                case GearSlot.Feet: return "Feet";
                case GearSlot.Artifact: return "Core";
                default: return slot.ToString();
            }
        }

        // --- Catalog + debug wiring ----------------------------------------------------------------

        private static void RegisterInCatalog(List<LootItemSO> allItems)
        {
            var catalog = AbilityAssetUtil.LoadOrCreate<GearCatalogSO>(CatalogPath);
            var so = new SerializedObject(catalog);
            var list = so.FindProperty("_items");

            // Preserve whatever the catalog already references, then append any of our items not yet present.
            var present = new HashSet<Object>();
            for (int i = 0; i < list.arraySize; i++)
            {
                var obj = list.GetArrayElementAtIndex(i).objectReferenceValue;
                if (obj != null) present.Add(obj);
            }
            foreach (var item in allItems)
            {
                if (item == null || present.Contains(item)) continue;
                list.arraySize++;
                list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = item;
                present.Add(item);
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void WireDebugController(List<LootItemSO> allItems)
        {
            var debug = Object.FindFirstObjectByType<GearDebugController>();
            if (debug == null)
            {
                Debug.Log("[Task27] No GearDebugController in the open scene — skipped grant-list wiring " +
                          "(open the gameplay scene and re-run if you want the G key to grant all tiers).");
                return;
            }

            var so = new SerializedObject(debug);
            var list = so.FindProperty("_sampleItems");
            list.arraySize = allItems.Count;
            for (int i = 0; i < allItems.Count; i++)
                list.GetArrayElementAtIndex(i).objectReferenceValue = allItems[i];
            so.ApplyModifiedPropertiesWithoutUndo();
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(debug.gameObject.scene);
            Debug.Log($"[Task27] Wired all {allItems.Count} items into the scene's GearDebugController grant list. " +
                      "Save the scene (Ctrl+S) to keep it.");
        }
    }
}
#endif
