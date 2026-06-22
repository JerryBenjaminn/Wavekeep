#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Wavekeep.Data;

namespace Wavekeep.EditorTools
{
    /// <summary>
    /// Extends the Task 02 test content for Task 10: authors a placeholder Boss (a bigger, red,
    /// tougher <see cref="EnemyDefinitionSO"/> + scaled capsule prefab), grows the existing TestTier to
    /// 12 waves, and configures the tier's milestone-scaling + boss-wave parameters (milestone every 5,
    /// boss every 10). It reuses the Task 02 grunt enemy/prefab and the SAME TestTier asset rather than
    /// creating a disconnected new tier.
    ///
    /// Boss representation (documented decision): NO BossDefinitionSO and NO isBoss flag — the boss is
    /// an ordinary <see cref="EnemyDefinitionSO"/> with high stats + a distinct prefab. This structurally
    /// guarantees "no special-cased boss behaviour" (it is the same type the spawner/EnemyRuntime always
    /// handle). The tier references it via <c>DifficultyTierSO.BossDefinition</c>.
    ///
    /// Run "Wavekeep/Setup Task 10 (Wave Scaling &amp; Bosses)" after the Task 02 setup, then save the
    /// scene. Editor-only; not part of the runtime build.
    /// </summary>
    public static class Task10SceneSetup
    {
        private const string GruntPrefabPath = "Assets/Prefabs/Enemies/PlaceholderEnemy.prefab";
        private const string GruntDefPath = "Assets/Data/Enemies/PlaceholderGrunt.asset";
        private const string TierPath = "Assets/Data/DifficultyTiers/TestTier.asset";
        private const string BossDefPath = "Assets/Data/Enemies/BossGrunt.asset";
        private const string BossPrefabPath = "Assets/Prefabs/Enemies/PlaceholderBoss.prefab";
        private const string BossMaterialPath = "Assets/Materials/BossMaterial.mat";

        private const int WaveCount = 12;
        private const float BossScale = 2.2f;

        [MenuItem("Wavekeep/Setup Task 10 (Wave Scaling & Bosses)")]
        public static void SetupScene()
        {
            var grunt = AssetDatabase.LoadAssetAtPath<EnemyDefinitionSO>(GruntDefPath);
            if (grunt == null)
            {
                Debug.LogError($"[Task10SceneSetup] Grunt enemy not found at {GruntDefPath}. Run 'Wavekeep/Setup Task 02 (Waves)' first.");
                return;
            }

            // --- Boss content: material → prefab → definition. ---
            var bossMaterial = CreateBossMaterial();
            var bossPrefab = CreateBossPrefab(bossMaterial);
            var bossDef = CreateBossDefinition(bossPrefab);

            // --- Author 12 waves (gentle per-wave ramp so the milestone jumps stay visible). ---
            EnsureFolder("Assets/Data", "Waves");
            var waves = new List<WaveConfigSO>(WaveCount);
            for (int n = 1; n <= WaveCount; n++)
            {
                float perWave = 1.0f + 0.05f * (n - 1);
                int count = 4 + n; // wave 1 → 5 grunts, scaling up
                waves.Add(CreateWave($"Assets/Data/Waves/Wave_{n:00}.asset", n, grunt, count, 0.5f, perWave));
            }

            ConfigureTier(waves, bossDef);
            AssetDatabase.SaveAssets();

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log("[Task10SceneSetup] 12 waves + milestone scaling (every 5) + boss waves (every 10) configured. " +
                      "Play: watch the scaling log; wave 5 = stat jump, wave 10 = boss alongside. Save the scene (Ctrl+S).");
        }

        private static Material CreateBossMaterial()
        {
            EnsureFolder("Assets", "Materials");
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");

            var mat = AssetDatabase.LoadAssetAtPath<Material>(BossMaterialPath);
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, BossMaterialPath);
            }
            mat.shader = shader;
            mat.color = new Color(0.65f, 0.06f, 0.06f); // distinct dark red (maps to URP Lit _BaseColor)
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static GameObject CreateBossPrefab(Material bossMaterial)
        {
            EnsureFolder("Assets/Prefabs", "Enemies");

            // Built fresh each run for idempotency: a scaled-up capsule, kinematic (movement is driven
            // on the transform, like the grunt — pooling skips physics reset on kinematic bodies), with
            // the distinct red material. Same component shape as the grunt, so EnemyRuntime drives it
            // identically (no boss-specific code).
            var temp = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            temp.name = "PlaceholderBoss";
            temp.transform.localScale = Vector3.one * BossScale;

            var rb = temp.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;

            if (temp.TryGetComponent<MeshRenderer>(out var renderer))
            {
                renderer.sharedMaterial = bossMaterial;
            }

            var prefab = PrefabUtility.SaveAsPrefabAsset(temp, BossPrefabPath);
            Object.DestroyImmediate(temp);
            return prefab;
        }

        private static EnemyDefinitionSO CreateBossDefinition(GameObject bossPrefab)
        {
            EnsureFolder("Assets/Data", "Enemies");
            var def = LoadOrCreate<EnemyDefinitionSO>(BossDefPath);
            var so = new SerializedObject(def);
            so.FindProperty("_enemyName").stringValue = "Boss Grunt";
            so.FindProperty("_prefab").objectReferenceValue = bossPrefab;
            so.FindProperty("_maxHealth").floatValue = 400f;   // notably higher than the grunt's 10
            so.FindProperty("_moveSpeed").floatValue = 1.5f;   // lumbering vs grunt's 3
            so.FindProperty("_contactDamage").floatValue = 30f; // vs grunt's 5
            so.FindProperty("_currencyReward").intValue = 50;
            so.FindProperty("_xpReward").intValue = 50;
            so.ApplyModifiedPropertiesWithoutUndo();
            return def;
        }

        private static WaveConfigSO CreateWave(
            string path, int waveNumber, EnemyDefinitionSO enemyType, int count, float interval, float perWaveMultiplier)
        {
            var wave = LoadOrCreate<WaveConfigSO>(path);
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

        private static void ConfigureTier(List<WaveConfigSO> waves, EnemyDefinitionSO bossDef)
        {
            EnsureFolder("Assets/Data", "DifficultyTiers");
            var tier = LoadOrCreate<DifficultyTierSO>(TierPath);
            var so = new SerializedObject(tier);
            so.FindProperty("_tierName").stringValue = "Test";
            so.FindProperty("_globalStatMultiplier").floatValue = 1.2f;

            var waveList = so.FindProperty("_waves");
            waveList.arraySize = waves.Count;
            for (int i = 0; i < waves.Count; i++)
            {
                waveList.GetArrayElementAtIndex(i).objectReferenceValue = waves[i];
            }

            so.FindProperty("_milestoneWaveInterval").intValue = 5;
            so.FindProperty("_milestoneStep").floatValue = 0.5f;
            so.FindProperty("_bossWaveInterval").intValue = 10;
            so.FindProperty("_bossDefinition").objectReferenceValue = bossDef;
            so.FindProperty("_bossCount").intValue = 1;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<T>();
                AssetDatabase.CreateAsset(asset, path);
            }
            return asset;
        }

        private static void EnsureFolder(string parent, string child)
        {
            if (!AssetDatabase.IsValidFolder($"{parent}/{child}"))
            {
                AssetDatabase.CreateFolder(parent, child);
            }
        }
    }
}
#endif
