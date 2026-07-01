#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Wavekeep.Core;
using Wavekeep.Data;

namespace Wavekeep.EditorTools
{
    /// <summary>
    /// Task 68 — migrates the loot tables to the new drop-generation shape (gear redesign part 2) and wires the
    /// affix config so kills generate rolled <c>GearInstance</c>s:
    /// <list type="bullet">
    /// <item>For each loot table, DERIVES the new <c>_slotEntries</c> (slot/base + weight) and <c>_rarityWeights</c>
    /// (rarity + weight) from the table's existing, balance-tuned legacy <c>_entries</c> — by aggregating the
    /// marginal per-slot and per-rarity weights. This preserves the Task 61–64 tuned drop-rate / rarity feel
    /// automatically (re-run after any balance change to re-sync), at the cost of the joint slot↔rarity
    /// correlation (see the flag logged at the end).</item>
    /// <item>Authors a hand-authored Unique fixed-affix set on each <see cref="GearBaseSO"/> (Unique never rolls).</item>
    /// <item>Wires the open scene's <c>GameSessionBootstrap._gearAffixConfig</c> to the Task 67 affix config.</item>
    /// </list>
    /// Run "Wavekeep/Setup Task 68 (Drop Generation)" from the gameplay scene, then save the scene. Idempotent.
    /// Editor-only. Requires the Task 67 setup to have run first (bases/affixes/config + catalog must exist).
    /// </summary>
    public static class Task68DropGenerationSetup
    {
        private const string LootFolder = "Assets/Data/Loot";
        private const string GearFolder = "Assets/Data/Gear";
        private const string CatalogPath = GearFolder + "/GearCatalog.asset";
        private const string ConfigPath = GearFolder + "/GearAffixCountConfig.asset";

        private static readonly string[] TablePaths =
        {
            LootFolder + "/LootTable_Regular.asset",
            LootFolder + "/LootTable_BossEarly.asset",
            LootFolder + "/LootTable_BossLate.asset",
        };

        [MenuItem("Wavekeep/Setup Task 68 (Drop Generation)")]
        public static void Setup()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<GearCatalogSO>(CatalogPath);
            if (catalog == null)
            {
                Debug.LogError("[Task68] No GearCatalog at " + CatalogPath + ". Run 'Setup Task 67' first.");
                return;
            }

            var log = new StringBuilder();
            foreach (var path in TablePaths)
            {
                var table = AssetDatabase.LoadAssetAtPath<LootTableSO>(path);
                if (table == null) { Debug.LogWarning("[Task68] Missing loot table: " + path); continue; }
                MigrateTable(table, catalog, log);
            }

            AuthorUniqueAffixSets(catalog, log);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            WireBootstrap();

            Debug.Log("[Task68] Drop-generation migration complete:\n" + log +
                      "\nFLAG: slot×rarity JOINT correlation in the boss tables is not preserved — the new shape " +
                      "rolls slot and rarity independently, so the marginal slot and rarity distributions match the " +
                      "old tables exactly, but specific old pairings (e.g. BossLate's Unique-only-on-Artifact) can " +
                      "now cross (e.g. a Unique on another slot). Regular preserves exactly (it was a clean grid). " +
                      "Save the scene (Ctrl+S) to persist the bootstrap wiring.");
        }

        // --- Table migration: aggregate legacy entries into marginal slot + rarity weights ----------

        private static void MigrateTable(LootTableSO table, GearCatalogSO catalog, StringBuilder log)
        {
            var so = new SerializedObject(table);
            var entries = so.FindProperty("_entries");

            // Aggregate marginal weights from the tuned legacy entries (rarity stored on the item itself).
            var slotWeights = new Dictionary<GearSlot, int>();
            var rarityWeights = new Dictionary<Rarity, int>();
            for (int i = 0; i < entries.arraySize; i++)
            {
                var el = entries.GetArrayElementAtIndex(i);
                var item = el.FindPropertyRelative("_item").objectReferenceValue as LootItemSO;
                int w = el.FindPropertyRelative("_weight").intValue;
                if (item == null || w <= 0) continue;
                // Rarity weight is always aggregated (rarity distribution must be preserved). Task 71: the Artifact
                // slot is craft-only (Forge), so it is EXCLUDED from the slot pool — but its rarity contribution is
                // kept, so e.g. Unique still rolls (now landing on a non-Artifact slot).
                rarityWeights.TryGetValue(item.Rarity, out int rw); rarityWeights[item.Rarity] = rw + w;
                if (item.Slot == GearSlot.Artifact) continue;
                slotWeights.TryGetValue(item.Slot, out int sw); slotWeights[item.Slot] = sw + w;
            }

            // Write slot entries (enum order for stable diffs), resolving each slot to its base.
            var slotProp = so.FindProperty("_slotEntries");
            slotProp.ClearArray();
            foreach (GearSlot slot in System.Enum.GetValues(typeof(GearSlot)))
            {
                if (!slotWeights.TryGetValue(slot, out int w)) continue;
                var baseTemplate = catalog.FindBaseForSlot(slot);
                if (baseTemplate == null)
                {
                    Debug.LogWarning($"[Task68] {table.name}: no GearBase registered for slot {slot}; skipped.");
                    continue;
                }
                int idx = slotProp.arraySize;
                slotProp.InsertArrayElementAtIndex(idx);
                var el = slotProp.GetArrayElementAtIndex(idx);
                el.FindPropertyRelative("_base").objectReferenceValue = baseTemplate;
                el.FindPropertyRelative("_weight").intValue = w;
            }

            // Write rarity weights (enum order).
            var rarProp = so.FindProperty("_rarityWeights");
            rarProp.ClearArray();
            foreach (Rarity rarity in System.Enum.GetValues(typeof(Rarity)))
            {
                if (!rarityWeights.TryGetValue(rarity, out int w)) continue;
                int idx = rarProp.arraySize;
                rarProp.InsertArrayElementAtIndex(idx);
                var el = rarProp.GetArrayElementAtIndex(idx);
                el.FindPropertyRelative("_rarity").enumValueIndex = (int)rarity;
                el.FindPropertyRelative("_weight").intValue = w;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(table);

            log.Append($"  {table.name}: slots [");
            foreach (var kv in slotWeights) log.Append($"{kv.Key}:{kv.Value} ");
            log.Append("] rarities [");
            foreach (var kv in rarityWeights) log.Append($"{kv.Key}:{kv.Value} ");
            log.Append("]\n");
        }

        // --- Unique fixed-affix sets (Unique never rolls; hand-authored per base) --------------------

        private static void AuthorUniqueAffixSets(GearCatalogSO catalog, StringBuilder log)
        {
            // Strong, fixed values (deliberately at/above the normal roll ceiling, befitting Unique).
            var empowered = catalog.FindAffix("affix_empowered"); // DamageMultiplier (roll 1.05–1.25)
            var sharpened = catalog.FindAffix("affix_sharpened"); // DamageFlatBonus  (roll 3–12)
            var lucky = catalog.FindAffix("affix_lucky");         // Luck            (roll 1–5)

            int authored = 0;
            foreach (var baseTemplate in catalog.Bases)
            {
                if (baseTemplate == null) continue;

                // Artifact (Luck implicit) gets a Luck-leaning fixed set; gear slots get an offensive set.
                var set = baseTemplate.Slot == GearSlot.Artifact
                    ? new List<(AffixDefinitionSO affix, float value)> { (lucky, 6f), (empowered, 1.30f) }
                    : new List<(AffixDefinitionSO affix, float value)> { (empowered, 1.30f), (sharpened, 15f) };

                var so = new SerializedObject(baseTemplate);
                var prop = so.FindProperty("_uniqueAffixes");
                prop.ClearArray();
                for (int i = 0; i < set.Count; i++)
                {
                    if (set[i].affix == null) continue;
                    int idx = prop.arraySize;
                    prop.InsertArrayElementAtIndex(idx);
                    var el = prop.GetArrayElementAtIndex(idx);
                    el.FindPropertyRelative("_affix").objectReferenceValue = set[i].affix;
                    el.FindPropertyRelative("_value").floatValue = set[i].value;
                }
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(baseTemplate);
                authored++;
            }
            log.Append($"  Authored Unique fixed-affix sets on {authored} base(s).\n");
        }

        // --- Bootstrap wiring -----------------------------------------------------------------------

        private static void WireBootstrap()
        {
            var config = AssetDatabase.LoadAssetAtPath<GearAffixCountConfigSO>(ConfigPath);
            if (config == null)
            {
                Debug.LogWarning("[Task68] No GearAffixCountConfig at " + ConfigPath + "; bootstrap not wired.");
                return;
            }

            var bootstrap = Object.FindFirstObjectByType<GameSessionBootstrap>();
            if (bootstrap == null)
            {
                Debug.Log("[Task68] No GameSessionBootstrap in the open scene — skipped affix-config wiring (open " +
                          "the gameplay scene and re-run to enable affix rolling on drops).");
                return;
            }

            var so = new SerializedObject(bootstrap);
            so.FindProperty("_gearAffixConfig").objectReferenceValue = config;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(bootstrap.gameObject.scene);
            Debug.Log("[Task68] Wired GearAffixCountConfig into the scene's GameSessionBootstrap. Save the scene.");
        }
    }
}
#endif
