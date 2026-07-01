#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Wavekeep.Core;
using Wavekeep.Data;

namespace Wavekeep.EditorTools
{
    /// <summary>
    /// Task 71 — gear economy: salvage core + Artifact Forge data wiring.
    /// <list type="bullet">
    /// <item>Authors a <see cref="GearEconomyConfigSO"/> (inventory cap, salvage Dust yields, forge costs).</item>
    /// <item>Strips the Artifact slot from every loot table's <c>_slotEntries</c> (Artifacts are craft-only now).
    /// Rarity weights are left untouched, so e.g. Unique still drops — just never on the Artifact slot.</item>
    /// <item>Wires the open scene's <c>GameSessionBootstrap._gearEconomyConfig</c> (and re-ensures the Task 68
    /// affix config is wired, which the forge also needs).</item>
    /// </list>
    /// The hard runtime guarantee that Artifacts never drop lives in <c>GearGenerator.PickBase</c> (it skips the
    /// Artifact slot); this setup additionally cleans the authored data. Run "Wavekeep/Setup Task 71 (Gear Economy)"
    /// from the gameplay scene after the Task 67/68 setups, then save the scene. Idempotent. Editor-only.
    /// </summary>
    public static class Task71GearEconomySetup
    {
        private const string GearFolder = "Assets/Data/Gear";
        private const string LootFolder = "Assets/Data/Loot";
        private const string EconomyPath = GearFolder + "/GearEconomyConfig.asset";
        private const string AffixConfigPath = GearFolder + "/GearAffixCountConfig.asset";

        private static readonly string[] TablePaths =
        {
            LootFolder + "/LootTable_Regular.asset",
            LootFolder + "/LootTable_BossEarly.asset",
            LootFolder + "/LootTable_BossLate.asset",
        };

        // Placeholder economy tuning (flagged for later balance):
        // - Capacity 40: room for a couple of runs' drops before salvage pressure kicks in.
        // - Salvage Dust doubles per tier (Common 1 … Unique 32).
        // - Forge cost steepens per tier; Unique (700) is the premium sink.
        private const int Capacity = 40;
        private static readonly int[] SalvageDust = { 1, 2, 4, 8, 16, 32 };       // Common..Unique
        private static readonly int[] ForgeCost = { 10, 25, 60, 140, 320, 700 };  // Common..Unique

        [MenuItem("Wavekeep/Setup Task 71 (Gear Economy)")]
        public static void Setup()
        {
            var economy = AuthorEconomyConfig();
            int stripped = StripArtifactFromTables();
            WireBootstrap(economy);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

            Debug.Log($"[Task71] Gear economy authored (cap {Capacity}, salvage {Format(SalvageDust)}, forge " +
                      $"{Format(ForgeCost)}). Removed {stripped} Artifact slot entr(y/ies) from loot tables — Artifacts " +
                      "are now Forge-only. Bootstrap wired. Save the scene (Ctrl+S).");
        }

        private static GearEconomyConfigSO AuthorEconomyConfig()
        {
            var asset = AbilityAssetUtil.LoadOrCreate<GearEconomyConfigSO>(EconomyPath);
            var so = new SerializedObject(asset);
            so.FindProperty("_inventoryCapacity").intValue = Capacity;
            SetIntArray(so.FindProperty("_salvageDustByRarity"), SalvageDust);
            SetIntArray(so.FindProperty("_forgeCostByRarity"), ForgeCost);
            so.ApplyModifiedPropertiesWithoutUndo();
            return asset;
        }

        // Remove any _slotEntries element whose base is the Artifact slot. Leaves _rarityWeights untouched.
        private static int StripArtifactFromTables()
        {
            int removed = 0;
            foreach (var path in TablePaths)
            {
                var table = AssetDatabase.LoadAssetAtPath<LootTableSO>(path);
                if (table == null) { Debug.LogWarning("[Task71] Missing loot table: " + path); continue; }

                var so = new SerializedObject(table);
                var slots = so.FindProperty("_slotEntries");
                for (int i = slots.arraySize - 1; i >= 0; i--)
                {
                    var baseRef = slots.GetArrayElementAtIndex(i).FindPropertyRelative("_base").objectReferenceValue as GearBaseSO;
                    if (baseRef != null && baseRef.Slot == GearSlot.Artifact)
                    {
                        slots.DeleteArrayElementAtIndex(i);
                        removed++;
                    }
                }
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(table);
            }
            return removed;
        }

        private static void WireBootstrap(GearEconomyConfigSO economy)
        {
            var bootstrap = Object.FindFirstObjectByType<GameSessionBootstrap>();
            if (bootstrap == null)
            {
                Debug.Log("[Task71] No GameSessionBootstrap in the open scene — skipped wiring (open the gameplay " +
                          "scene and re-run to enable the inventory cap + forge).");
                return;
            }

            var affixConfig = AssetDatabase.LoadAssetAtPath<GearAffixCountConfigSO>(AffixConfigPath);
            var so = new SerializedObject(bootstrap);
            so.FindProperty("_gearEconomyConfig").objectReferenceValue = economy;
            if (affixConfig != null) so.FindProperty("_gearAffixConfig").objectReferenceValue = affixConfig; // forge needs it
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(bootstrap.gameObject.scene);
        }

        private static void SetIntArray(SerializedProperty prop, int[] values)
        {
            prop.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) prop.GetArrayElementAtIndex(i).intValue = values[i];
        }

        private static string Format(int[] a) => "[" + string.Join("/", a) + "]";
    }
}
#endif
