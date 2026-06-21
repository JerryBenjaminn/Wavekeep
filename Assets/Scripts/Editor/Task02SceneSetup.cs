#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Wavekeep.Core;
using Wavekeep.Data;
using Wavekeep.Pooling;
using Wavekeep.Runtime;
using Wavekeep.Waves;

namespace Wavekeep.EditorTools
{
    /// <summary>
    /// One-shot editor utility that wires Task 02 on top of the Task 01 scene: authors the test
    /// difficulty tier + waves + enemy definition assets, lays out a single far-side line of spawn
    /// markers (single approach direction, CLAUDE.md §2), adds the defended <see cref="WallRuntime"/>
    /// between the spawn side and the player, adds and wires the <see cref="WaveSpawner"/>, makes the
    /// placeholder enemy kinematic (so direct transform movement isn't fought by gravity), and
    /// removes the Task 01 pooling smoke test.
    ///
    /// Built in code for the same reason as Task 01: scenes/assets can't be reliably hand-authored
    /// as YAML outside the editor. Run "Wavekeep/Setup Task 02 (Waves)" after the Task 01 setup,
    /// then save the scene. Editor-only; not part of the runtime build.
    ///
    /// Layout note: the camera (Task 01) sits at -Z looking toward +Z, so +Z is the "far" side.
    /// Enemies spawn at +Z and advance toward the wall at the centre, keeping their lateral X lane.
    /// </summary>
    public static class Task02SceneSetup
    {
        private const string PlaceholderPrefabPath = "Assets/Prefabs/Enemies/PlaceholderEnemy.prefab";
        private const string EnemyDefPath = "Assets/Data/Enemies/PlaceholderGrunt.asset";
        private const string TierPath = "Assets/Data/DifficultyTiers/TestTier.asset";
        private static readonly string[] WavePaths =
        {
            "Assets/Data/Waves/Wave_01.asset",
            "Assets/Data/Waves/Wave_02.asset",
            "Assets/Data/Waves/Wave_03.asset",
        };

        private const int SpawnMarkerCount = 6;
        private const float SpawnLineZ = 20f;          // far side, opposite the player
        private const float SpawnLineHalfWidth = 12f;  // markers spread across X
        private const float EnemyGroundHeight = 1f;    // capsule half-height so it rests on the ground

        private const float WallZ = 0f;                // wall between enemies (+Z) and player (-Z)
        private static readonly Vector3 WallSize = new Vector3(26f, 3f, 1f);

        [MenuItem("Wavekeep/Setup Task 02 (Waves)")]
        public static void SetupScene()
        {
            var bootstrap = Object.FindFirstObjectByType<GameSessionBootstrap>();
            if (bootstrap == null)
            {
                Debug.LogError("[Task02SceneSetup] No GameSessionBootstrap in scene. Run 'Wavekeep/Setup Task 01 Scene' first.");
                return;
            }

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlaceholderPrefabPath);
            if (prefab == null)
            {
                Debug.LogError($"[Task02SceneSetup] Placeholder prefab missing at {PlaceholderPrefabPath}. Run Task 01 setup first.");
                return;
            }

            MakePlaceholderKinematic();

            // --- Author the test data assets. ---
            var enemyDef = CreateEnemyDefinition(prefab);
            var waves = new[]
            {
                CreateWave(WavePaths[0], 1, enemyDef, count: 5, interval: 0.6f, statMultiplier: 1.0f),
                CreateWave(WavePaths[1], 2, enemyDef, count: 8, interval: 0.5f, statMultiplier: 1.5f),
                CreateWave(WavePaths[2], 3, enemyDef, count: 10, interval: 0.4f, statMultiplier: 2.0f),
            };
            var tier = CreateTier(waves);

            AssetDatabase.SaveAssets();

            // --- Replace any previous Task 02 scene objects so re-running is idempotent. ---
            DestroyIfExists("DefendedPoint"); // legacy object from the pre-correction layout
            DestroyIfExists("Wall");
            DestroyIfExists("SpawnMarkers");
            DestroyIfExists("WaveSpawner");

            // --- Defended wall (spans the arena width, between enemies and the player). ---
            var wallGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wallGo.name = "Wall";
            wallGo.transform.position = new Vector3(0f, WallSize.y * 0.5f, WallZ);
            wallGo.transform.localScale = WallSize;
            var wall = wallGo.AddComponent<WallRuntime>();

            // --- Far-side spawn markers: a single line across X (NOT a perimeter ring). ---
            var markersParent = new GameObject("SpawnMarkers");
            var markers = new Transform[SpawnMarkerCount];
            for (int i = 0; i < SpawnMarkerCount; i++)
            {
                float t = SpawnMarkerCount == 1 ? 0.5f : i / (float)(SpawnMarkerCount - 1);
                float x = Mathf.Lerp(-SpawnLineHalfWidth, SpawnLineHalfWidth, t);
                var marker = new GameObject($"Spawn_{i}");
                marker.transform.SetParent(markersParent.transform, false);
                marker.transform.position = new Vector3(x, EnemyGroundHeight, SpawnLineZ);
                markers[i] = marker.transform;
            }

            // --- Wave spawner, wired to dependencies + arena. ---
            var spawnerGo = new GameObject("WaveSpawner", typeof(WaveSpawner));
            var spawner = spawnerGo.GetComponent<WaveSpawner>();
            var so = new SerializedObject(spawner);
            so.FindProperty("_bootstrap").objectReferenceValue = bootstrap;
            so.FindProperty("_difficultyTier").objectReferenceValue = tier;
            so.FindProperty("_wall").objectReferenceValue = wall;

            var markersProp = so.FindProperty("_spawnMarkers");
            markersProp.arraySize = markers.Length;
            for (int i = 0; i < markers.Length; i++)
            {
                markersProp.GetArrayElementAtIndex(i).objectReferenceValue = markers[i];
            }
            so.ApplyModifiedPropertiesWithoutUndo();

            // --- Remove the Task 01 smoke test so it doesn't also spawn capsules. ---
            var smokeTest = Object.FindFirstObjectByType<EnemyPoolSmokeTest>();
            if (smokeTest != null)
            {
                Object.DestroyImmediate(smokeTest);
                Debug.Log("[Task02SceneSetup] Removed Task 01 EnemyPoolSmokeTest component.");
            }

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log("[Task02SceneSetup] Task 02 scaffold created. Press Play: enemies spawn from the far side and attack the wall until it falls (defeat). Press K to debug-kill an enemy. Save the scene (Ctrl+S) to persist it.");
        }

        private static void MakePlaceholderKinematic()
        {
            var contents = PrefabUtility.LoadPrefabContents(PlaceholderPrefabPath);
            if (contents.TryGetComponent<Rigidbody>(out var rb))
            {
                // Movement is driven directly on the transform; keep physics from fighting it.
                rb.isKinematic = true;
                rb.useGravity = false;
                PrefabUtility.SaveAsPrefabAsset(contents, PlaceholderPrefabPath);
            }
            PrefabUtility.UnloadPrefabContents(contents);
        }

        private static EnemyDefinitionSO CreateEnemyDefinition(GameObject prefab)
        {
            var def = LoadOrCreate<EnemyDefinitionSO>(EnemyDefPath);
            var so = new SerializedObject(def);
            so.FindProperty("_enemyName").stringValue = "Placeholder Grunt";
            so.FindProperty("_prefab").objectReferenceValue = prefab;
            so.FindProperty("_maxHealth").floatValue = 10f;
            so.FindProperty("_moveSpeed").floatValue = 3f;
            so.FindProperty("_contactDamage").floatValue = 5f;
            so.FindProperty("_currencyReward").intValue = 5;
            so.FindProperty("_xpReward").intValue = 5;
            so.ApplyModifiedPropertiesWithoutUndo();
            return def;
        }

        private static WaveConfigSO CreateWave(
            string path, int waveNumber, EnemyDefinitionSO enemyType, int count, float interval, float statMultiplier)
        {
            var wave = LoadOrCreate<WaveConfigSO>(path);
            var so = new SerializedObject(wave);
            so.FindProperty("_waveNumber").intValue = waveNumber;
            so.FindProperty("_statMultiplier").floatValue = statMultiplier;

            var entries = so.FindProperty("_spawnEntries");
            entries.arraySize = 1;
            var entry = entries.GetArrayElementAtIndex(0);
            entry.FindPropertyRelative("_enemyType").objectReferenceValue = enemyType;
            entry.FindPropertyRelative("_count").intValue = count;
            entry.FindPropertyRelative("_spawnInterval").floatValue = interval;
            so.ApplyModifiedPropertiesWithoutUndo();
            return wave;
        }

        private static DifficultyTierSO CreateTier(WaveConfigSO[] waves)
        {
            var tier = LoadOrCreate<DifficultyTierSO>(TierPath);
            var so = new SerializedObject(tier);
            so.FindProperty("_tierName").stringValue = "Test";
            so.FindProperty("_globalStatMultiplier").floatValue = 1.2f;

            var waveList = so.FindProperty("_waves");
            waveList.arraySize = waves.Length;
            for (int i = 0; i < waves.Length; i++)
            {
                waveList.GetArrayElementAtIndex(i).objectReferenceValue = waves[i];
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            return tier;
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

        private static void DestroyIfExists(string objectName)
        {
            var existing = GameObject.Find(objectName);
            if (existing != null)
            {
                Object.DestroyImmediate(existing);
            }
        }
    }
}
#endif
