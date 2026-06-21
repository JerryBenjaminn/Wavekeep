#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using Wavekeep.Core;
using Wavekeep.Pooling;

namespace Wavekeep.EditorTools
{
    /// <summary>
    /// One-shot editor utility that builds the Task 01 scene scaffold programmatically
    /// (fixed 3/4 top-down camera, ground plane on the Ground layer, landscape-tuned Canvas,
    /// GameSessionBootstrap, and a placeholder enemy prefab + pooling smoke test).
    ///
    /// This exists because the scene cannot be reliably hand-authored as raw YAML outside the
    /// editor; building it in code keeps the result deterministic and reviewable. Run it once
    /// via the menu, then save the scene. It is editor-only and not part of the runtime build.
    /// </summary>
    public static class Task01SceneSetup
    {
        private const string GroundLayerName = "Ground";
        private const string PlaceholderPrefabPath = "Assets/Prefabs/Enemies/PlaceholderEnemy.prefab";

        [MenuItem("Wavekeep/Setup Task 01 Scene")]
        public static void SetupScene()
        {
            int groundLayer = LayerMask.NameToLayer(GroundLayerName);
            if (groundLayer == -1)
            {
                Debug.LogError($"[Task01SceneSetup] Layer '{GroundLayerName}' does not exist. " +
                               "Add it under Project Settings > Tags and Layers, then re-run.");
                return;
            }

            // --- Camera: fixed 3/4 top-down, no orbit. Placeholder framing of world origin. ---
            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            var camera = camGo.AddComponent<Camera>();
            camGo.AddComponent<AudioListener>();
            camGo.transform.SetPositionAndRotation(new Vector3(0f, 18f, -15f), Quaternion.Euler(50f, 0f, 0f));

            // --- Ground plane on the Ground layer (raycast target for IInteractionInput). ---
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.layer = groundLayer;
            ground.transform.localScale = new Vector3(5f, 1f, 5f); // 50 x 50 units

            // --- Directional light (only if the scene has none). ---
            if (Object.FindFirstObjectByType<Light>() == null)
            {
                var lightGo = new GameObject("Directional Light");
                var light = lightGo.AddComponent<Light>();
                light.type = LightType.Directional;
                lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            }

            // --- Landscape-tuned UI Canvas. ---
            var canvasGo = new GameObject("UI Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            // --- EventSystem using the new Input System UI module. ---
            if (Object.FindFirstObjectByType<EventSystem>() == null)
            {
                new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            }

            // --- Placeholder pooled enemy prefab (3D primitive + Collider + Rigidbody). ---
            var placeholderPrefab = CreatePlaceholderEnemyPrefab();

            // --- GameSession bootstrap + pool root, with serialized fields wired up. ---
            var bootstrapGo = new GameObject("GameSession", typeof(GameSessionBootstrap));
            var bootstrap = bootstrapGo.GetComponent<GameSessionBootstrap>();
            var poolRoot = new GameObject("PooledEnemies");
            poolRoot.transform.SetParent(bootstrapGo.transform, false);

            var boSo = new SerializedObject(bootstrap);
            boSo.FindProperty("_poolRoot").objectReferenceValue = poolRoot.transform;
            boSo.FindProperty("_interactionCamera").objectReferenceValue = camera;
            boSo.FindProperty("_groundLayer").intValue = 1 << groundLayer;
            boSo.ApplyModifiedPropertiesWithoutUndo();

            // --- Temporary pooling smoke test (Task 01 verification only). ---
            var smokeTest = bootstrapGo.AddComponent<EnemyPoolSmokeTest>();
            var stSo = new SerializedObject(smokeTest);
            stSo.FindProperty("_bootstrap").objectReferenceValue = bootstrap;
            stSo.FindProperty("_placeholderPrefab").objectReferenceValue = placeholderPrefab;
            stSo.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log("[Task01SceneSetup] Scene scaffold created. Save the scene (Ctrl+S) to persist it.");
        }

        private static GameObject CreatePlaceholderEnemyPrefab()
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(PlaceholderPrefabPath);
            if (existing != null) return existing;

            var temp = GameObject.CreatePrimitive(PrimitiveType.Capsule); // capsule ships with a CapsuleCollider
            temp.name = "PlaceholderEnemy";
            temp.AddComponent<Rigidbody>();

            var prefab = PrefabUtility.SaveAsPrefabAsset(temp, PlaceholderPrefabPath);
            Object.DestroyImmediate(temp);
            AssetDatabase.Refresh();
            return prefab;
        }
    }
}
#endif
