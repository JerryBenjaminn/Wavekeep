#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Wavekeep.Core;
using Wavekeep.Data;

namespace Wavekeep.EditorTools
{
    /// <summary>
    /// Authors the Task 13 loot content: three <see cref="LootTableSO"/>s (regular = Common/Uncommon/
    /// Rare only; boss-early = up to Rare; boss-late = up to Unique), wires the regular table onto the
    /// grunt enemy, extends the Task 10 test tier to 20 waves so there are TWO boss waves (10 = early
    /// tier, 20 = late tier) with different tables, and adds a placeholder drop-notification HUD.
    ///
    /// Wave-to-loot mapping (documented): the boss loot table lives on each WaveConfigSO's BossLootTable
    /// field (Task 13), NOT on the shared boss definition — so adding a higher tier later (e.g. wave 50)
    /// is just a new WaveConfigSO with its own table dropped into the tier's wave list. No code changes.
    ///
    /// Regular-enemy rarity restriction is enforced purely by table CONTENTS: the regular table simply
    /// has no Epic/Legendary/Unique entries (no code-level rarity filter exists).
    ///
    /// Run "Wavekeep/Setup Task 13 (Loot Drops)" after the Task 02/10/12 setups, then save the scene.
    /// Editor-only.
    /// </summary>
    public static class Task13SceneSetup
    {
        private const string LootFolder = "Assets/Data/Loot";
        private const string GearFolder = "Assets/Data/Gear";

        private const string GruntDefPath = "Assets/Data/Enemies/PlaceholderGrunt.asset";
        private const string TierPath = "Assets/Data/DifficultyTiers/TestTier.asset";

        private const int WaveCount = 20; // extends Task 10's 12 → 20 so wave 10 AND wave 20 are boss waves

        [MenuItem("Wavekeep/Setup Task 13 (Loot Drops)")]
        public static void SetupScene()
        {
            var bootstrap = Object.FindFirstObjectByType<GameSessionBootstrap>();
            var canvas = Object.FindFirstObjectByType<Canvas>();
            var grunt = AssetDatabase.LoadAssetAtPath<EnemyDefinitionSO>(GruntDefPath);
            var tier = AssetDatabase.LoadAssetAtPath<DifficultyTierSO>(TierPath);
            if (bootstrap == null || canvas == null || grunt == null || tier == null)
            {
                Debug.LogError("[Task13SceneSetup] Missing bootstrap/canvas/grunt/tier. Run the Task 01/02/10 setups first.");
                return;
            }

            // --- Resolve Task 12 gear/artifact items by rarity. ---
            var common = LoadItem("Gear_CommonHelm");
            var uncommon = LoadItem("Gear_UncommonBody");
            var rare = LoadItem("Gear_RareHands");
            var epic = LoadItem("Gear_EpicLegs");
            var legendary = LoadItem("Gear_LegendaryFeet");
            var unique = LoadItem("Artifact_UniqueCore");
            if (common == null || uncommon == null || rare == null || epic == null || legendary == null || unique == null)
            {
                Debug.LogError("[Task13SceneSetup] Missing Task 12 gear assets. Run 'Wavekeep/Setup Task 12 (Gear Core)' first.");
                return;
            }

            // --- Loot tables. ---
            EnsureFolder("Assets/Data", "Loot");
            var regularTable = CreateLootTable("LootTable_Regular", 0.10f,   // ~10% on regular kills
                (common, 5), (uncommon, 3), (rare, 1));                       // C/U/R ONLY — no high tiers
            var bossEarlyTable = CreateLootTable("LootTable_BossEarly", 1.0f, // guaranteed
                (common, 2), (uncommon, 3), (rare, 2));                       // max Rare
            var bossLateTable = CreateLootTable("LootTable_BossLate", 1.0f,   // guaranteed
                (rare, 3), (epic, 3), (legendary, 2), (unique, 1));           // full high range
            AssetDatabase.SaveAssets();

            // --- Regular enemy drops its table. ---
            SetObjectField(grunt, "_lootTable", regularTable);

            // --- Extend the tier to 20 waves (idempotent re-author of Task 10's pattern). ---
            var waves = new List<WaveConfigSO>(WaveCount);
            for (int n = 1; n <= WaveCount; n++)
            {
                float perWave = 1.0f + 0.05f * (n - 1);
                int count = 4 + n;
                waves.Add(CreateWave($"Assets/Data/Waves/Wave_{n:00}.asset", n, grunt, count, 0.5f, perWave));
            }
            SetTierWaves(tier, waves);

            // --- Two boss tiers: wave 10 = early, wave 20 = late. ---
            SetObjectField(waves[9], "_bossLootTable", bossEarlyTable);   // wave 10
            SetObjectField(waves[19], "_bossLootTable", bossLateTable);   // wave 20
            AssetDatabase.SaveAssets();

            // --- Drop notification. ---
            // Task 69: the text-toast LootDropHud is GONE — visual arena drops + an end-of-run summary replace it
            // (see Task69SceneSetup / LootDropVfxController / RunLootSummary). Clear any leftover toast objects so
            // re-running this older setup can't resurrect them.
            DestroyIfExists("LootDropText");
            DestroyIfExists("LootDropHud");

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log("[Task13SceneSetup] Loot tables wired. Play: kill grunts for ~10% drops; wave 10 boss → up to Rare, " +
                      "wave 20 boss → up to Unique. Drops persist via GearManager. For the visual drop layer run " +
                      "'Wavekeep/Setup Task 69 (Visual Loot Drops)'. Save the scene (Ctrl+S).");
        }

        private static LootItemSO LoadItem(string fileName) =>
            AssetDatabase.LoadAssetAtPath<LootItemSO>($"{GearFolder}/{fileName}.asset");

        private static LootTableSO CreateLootTable(string fileName, float dropChance,
            params (LootItemSO item, int weight)[] entries)
        {
            var table = AbilityAssetUtil.LoadOrCreate<LootTableSO>($"{LootFolder}/{fileName}.asset");
            var so = new SerializedObject(table);
            so.FindProperty("_dropChance").floatValue = dropChance;

            var list = so.FindProperty("_entries");
            list.arraySize = entries.Length;
            for (int i = 0; i < entries.Length; i++)
            {
                var element = list.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("_item").objectReferenceValue = entries[i].item;
                element.FindPropertyRelative("_weight").intValue = entries[i].weight;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            return table;
        }

        private static WaveConfigSO CreateWave(
            string path, int waveNumber, EnemyDefinitionSO enemyType, int count, float interval, float perWaveMultiplier)
        {
            var wave = AbilityAssetUtil.LoadOrCreate<WaveConfigSO>(path);
            var so = new SerializedObject(wave);
            so.FindProperty("_waveNumber").intValue = waveNumber;
            so.FindProperty("_statMultiplier").floatValue = perWaveMultiplier;

            var entries = so.FindProperty("_spawnEntries");
            entries.arraySize = 1;
            var entry = entries.GetArrayElementAtIndex(0);
            entry.FindPropertyRelative("_enemyType").objectReferenceValue = enemyType;
            entry.FindPropertyRelative("_count").intValue = count;
            entry.FindPropertyRelative("_spawnInterval").floatValue = interval;
            so.ApplyModifiedPropertiesWithoutUndo();
            return wave;
        }

        private static void SetTierWaves(DifficultyTierSO tier, List<WaveConfigSO> waves)
        {
            var so = new SerializedObject(tier);
            var list = so.FindProperty("_waves");
            list.arraySize = waves.Count;
            for (int i = 0; i < waves.Count; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = waves[i];
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

        private static void DestroyIfExists(string objectName)
        {
            var existing = GameObject.Find(objectName);
            if (existing != null) Object.DestroyImmediate(existing);
        }
    }
}
#endif
