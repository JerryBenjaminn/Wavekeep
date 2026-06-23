#if UNITY_EDITOR
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Wavekeep.Core;
using Wavekeep.Data;
using Wavekeep.Runtime;
using Wavekeep.UI;
using Wavekeep.Waves;

namespace Wavekeep.EditorTools
{
    /// <summary>
    /// Builds the Task 05 hero system on top of Tasks 01–04: a shared placeholder hero prefab (capsule
    /// + <see cref="HeroRuntime"/>), a second hero's two ability assets, two distinct
    /// <see cref="HeroDefinitionSO"/> assets, a hero spawn point, a debug upgrade granter, and a
    /// hero-select Canvas screen wired to a <see cref="HeroSelectController"/>. Also disables the
    /// WaveSpawner's auto-start so the run waits for the player's hero choice.
    ///
    /// Requires the Task 04 ability/upgrade assets (run "Wavekeep/Setup Task 04 (Abilities)" first).
    /// Run "Wavekeep/Setup Task 05 (Heroes)" after Task 01/02 setups, then save the scene. Editor-only.
    /// </summary>
    public static class Task05SceneSetup
    {
        private const string HeroPrefabPath = "Assets/Prefabs/Heroes/PlaceholderHero.prefab";
        private const string FrostNovaPath = "Assets/Data/Abilities/BasicFrostNova.asset";
        private const string IciclePath = "Assets/Data/Abilities/UltimateIcicle.asset";
        private const string HeroAPath = "Assets/Data/Heroes/Hero_BoltStriker.asset";
        private const string HeroBPath = "Assets/Data/Heroes/Hero_FrostWarden.asset";

        private static readonly Vector3 HeroSpawnPosition = new Vector3(0f, 1f, -3f);

        [MenuItem("Wavekeep/Setup Task 05 (Heroes)")]
        public static void SetupScene()
        {
            var bootstrap = Object.FindFirstObjectByType<GameSessionBootstrap>();
            var waveSpawner = Object.FindFirstObjectByType<WaveSpawner>();
            var canvas = Object.FindFirstObjectByType<Canvas>();
            if (bootstrap == null || waveSpawner == null || canvas == null)
            {
                Debug.LogError("[Task05SceneSetup] Missing GameSessionBootstrap / WaveSpawner / Canvas. Run the Task 01–02 setups first.");
                return;
            }

            var boltBasic = AssetDatabase.LoadAssetAtPath<AbilityDefinitionSO>(Task04SceneSetup.BasicPath);
            var nova = AssetDatabase.LoadAssetAtPath<AbilityDefinitionSO>(Task04SceneSetup.UltimatePath);
            if (boltBasic == null || nova == null)
            {
                Debug.LogError("[Task05SceneSetup] Task 04 ability assets not found. Run 'Wavekeep/Setup Task 04 (Abilities)' first.");
                return;
            }

            // --- Assets ---
            var heroPrefab = CreateHeroPrefab();
            var frostBasic = CreateFrostNova();
            var icicle = CreateIcicle();
            var heroA = CreateHero(HeroAPath, "Bolt Striker", new Color(0.3f, 0.6f, 1f), 100f, heroPrefab, boltBasic, nova);
            var heroB = CreateHero(HeroBPath, "Frost Warden", new Color(1f, 0.5f, 0.1f), 120f, heroPrefab, frostBasic, icicle);
            AssetDatabase.SaveAssets();

            // --- Scene cleanup (idempotent) ---
            DestroyIfExists("Hero");            // legacy Task 04 capsule
            DestroyIfExists("HeroSpawnPoint");
            DestroyIfExists("DebugUpgradeGranter");
            DestroyIfExists("HeroSelectPanel");
            DestroyIfExists("HeroSelect");

            // --- Hero spawn point ---
            var spawnPoint = new GameObject("HeroSpawnPoint");
            spawnPoint.transform.position = HeroSpawnPosition;

            // --- The run must wait for hero selection ---
            var wso = new SerializedObject(waveSpawner);
            wso.FindProperty("_autoStartOnPlay").boolValue = false;
            wso.ApplyModifiedPropertiesWithoutUndo();

            // --- Debug upgrade granter (keys 1..3) ---
            CreateDebugGranter(bootstrap);

            // --- Hero-select UI ---
            BuildHeroSelect(canvas, bootstrap, waveSpawner, spawnPoint.transform, new[] { heroA, heroB });

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log("[Task05SceneSetup] Hero select built. Play: pick a hero to start the run. U = ultimate, 1/2/3 = grant test upgrades. Save the scene (Ctrl+S).");
        }

        private static GameObject CreateHeroPrefab()
        {
            EnsureFolder("Assets/Prefabs", "Heroes");

            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(HeroPrefabPath);
            if (existing != null)
            {
                if (existing.GetComponent<HeroRuntime>() == null)
                {
                    var contents = PrefabUtility.LoadPrefabContents(HeroPrefabPath);
                    contents.AddComponent<HeroRuntime>();
                    PrefabUtility.SaveAsPrefabAsset(contents, HeroPrefabPath);
                    PrefabUtility.UnloadPrefabContents(contents);
                }
                return existing;
            }

            var temp = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            temp.name = "PlaceholderHero";
            temp.AddComponent<HeroRuntime>();
            var prefab = PrefabUtility.SaveAsPrefabAsset(temp, HeroPrefabPath);
            Object.DestroyImmediate(temp);
            return prefab;
        }

        private static AbilityDefinitionSO CreateFrostNova()
        {
            var ability = AbilityAssetUtil.LoadOrCreate<AbilityDefinitionSO>(FrostNovaPath);
            var so = new SerializedObject(ability);
            so.FindProperty("_abilityName").stringValue = "Frost Nova";
            so.FindProperty("_baseDamage").floatValue = 6f;
            so.FindProperty("_baseCooldown").floatValue = 0.8f;
            // Task 18: AoE radius covers the full far-side spawn line (corner markers ≈ 25.94u from the
            // set-back caster) so the basic hits enemies the instant they spawn, no dead zone.
            so.FindProperty("_range").floatValue = 28f;
            so.FindProperty("_targetingType").enumValueIndex = (int)AbilityTargetingType.AreaOfEffect;

            var rules = so.FindProperty("_tagInteractionRules");
            rules.arraySize = 1;
            AbilityAssetUtil.SetRule(rules.GetArrayElementAtIndex(0), UpgradeTag.AoE, AbilityModifierType.DamageMultiplier, 1.5f);
            so.ApplyModifiedPropertiesWithoutUndo();
            return ability;
        }

        private static AbilityDefinitionSO CreateIcicle()
        {
            var ability = AbilityAssetUtil.LoadOrCreate<AbilityDefinitionSO>(IciclePath);
            var so = new SerializedObject(ability);
            so.FindProperty("_abilityName").stringValue = "Icicle";
            so.FindProperty("_baseDamage").floatValue = 40f;
            so.FindProperty("_baseCooldown").floatValue = 5f;
            // Task 18: range covers the full far-side spawn line (corner markers ≈ 25.94u from the
            // set-back caster) so the ultimate is targetable the instant enemies spawn, no dead zone.
            so.FindProperty("_range").floatValue = 28f;
            so.FindProperty("_targetingType").enumValueIndex = (int)AbilityTargetingType.SingleTarget;

            var rules = so.FindProperty("_tagInteractionRules");
            rules.arraySize = 1;
            AbilityAssetUtil.SetRule(rules.GetArrayElementAtIndex(0), UpgradeTag.SingleTarget, AbilityModifierType.DamageMultiplier, 1.5f);
            so.ApplyModifiedPropertiesWithoutUndo();
            return ability;
        }

        private static HeroDefinitionSO CreateHero(
            string path, string name, Color tint, float baseHealth,
            GameObject prefab, AbilityDefinitionSO basic, AbilityDefinitionSO ultimate)
        {
            var hero = AbilityAssetUtil.LoadOrCreate<HeroDefinitionSO>(path);
            var so = new SerializedObject(hero);
            so.FindProperty("_heroName").stringValue = name;
            so.FindProperty("_tint").colorValue = tint;
            so.FindProperty("_baseHealth").floatValue = baseHealth;
            so.FindProperty("_prefab").objectReferenceValue = prefab;
            so.FindProperty("_basicAbility").objectReferenceValue = basic;
            so.FindProperty("_ultimateAbility").objectReferenceValue = ultimate;
            so.ApplyModifiedPropertiesWithoutUndo();
            return hero;
        }

        private static void CreateDebugGranter(GameSessionBootstrap bootstrap)
        {
            var granterGo = new GameObject("DebugUpgradeGranter", typeof(DebugUpgradeGranter));
            var so = new SerializedObject(granterGo.GetComponent<DebugUpgradeGranter>());
            so.FindProperty("_bootstrap").objectReferenceValue = bootstrap;

            var upgrades = new List<UpgradeDefinitionSO>();
            AddIfExists(upgrades, Task04SceneSetup.PrecisionPath);
            AddIfExists(upgrades, Task04SceneSetup.FirePath);
            AddIfExists(upgrades, Task04SceneSetup.MaelstromPath);

            var list = so.FindProperty("_debugUpgrades");
            list.arraySize = upgrades.Count;
            for (int i = 0; i < upgrades.Count; i++)
            {
                list.GetArrayElementAtIndex(i).objectReferenceValue = upgrades[i];
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void BuildHeroSelect(
            Canvas canvas, GameSessionBootstrap bootstrap, WaveSpawner waveSpawner,
            Transform spawnPoint, HeroDefinitionSO[] roster)
        {
            if (TMP_Settings.defaultFontAsset == null)
            {
                Debug.LogWarning("[Task05SceneSetup] TMP has no default font asset. If select text doesn't render, " +
                                 "import via Window > TextMeshPro > Import TMP Essential Resources, then re-run.");
            }

            // Full-screen dimmed panel.
            var panel = new GameObject("HeroSelectPanel", typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(canvas.transform, false);
            var prt = (RectTransform)panel.transform;
            prt.anchorMin = Vector2.zero;
            prt.anchorMax = Vector2.one;
            prt.offsetMin = Vector2.zero;
            prt.offsetMax = Vector2.zero;
            panel.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.6f);

            // Title.
            var titleGo = new GameObject("Title", typeof(RectTransform));
            titleGo.transform.SetParent(panel.transform, false);
            var title = titleGo.AddComponent<TextMeshProUGUI>();
            title.text = "Select Hero";
            title.fontSize = 48f;
            title.alignment = TextAlignmentOptions.Center;
            title.color = Color.white;
            if (TMP_Settings.defaultFontAsset != null) title.font = TMP_Settings.defaultFontAsset;
            var trt = title.rectTransform;
            trt.anchorMin = new Vector2(0.5f, 1f);
            trt.anchorMax = new Vector2(0.5f, 1f);
            trt.pivot = new Vector2(0.5f, 1f);
            trt.anchoredPosition = new Vector2(0f, -80f);
            trt.sizeDelta = new Vector2(600f, 80f);

            // Button container with a vertical layout.
            var container = new GameObject("ButtonContainer", typeof(RectTransform), typeof(VerticalLayoutGroup));
            container.transform.SetParent(panel.transform, false);
            var crt = (RectTransform)container.transform;
            crt.anchorMin = new Vector2(0.5f, 0.5f);
            crt.anchorMax = new Vector2(0.5f, 0.5f);
            crt.pivot = new Vector2(0.5f, 0.5f);
            crt.anchoredPosition = new Vector2(0f, -20f);
            crt.sizeDelta = new Vector2(320f, 400f);
            var vlg = container.GetComponent<VerticalLayoutGroup>();
            vlg.spacing = 12f;
            vlg.childAlignment = TextAnchor.MiddleCenter;
            vlg.childControlWidth = false;
            vlg.childControlHeight = false;
            vlg.childForceExpandWidth = false;
            vlg.childForceExpandHeight = false;

            // Controller on a separate root object so hiding the panel doesn't disable it.
            var selectGo = new GameObject("HeroSelect", typeof(HeroSelectController));
            var controller = selectGo.GetComponent<HeroSelectController>();
            var so = new SerializedObject(controller);
            so.FindProperty("_bootstrap").objectReferenceValue = bootstrap;
            so.FindProperty("_waveSpawner").objectReferenceValue = waveSpawner;
            so.FindProperty("_heroSpawnPoint").objectReferenceValue = spawnPoint;
            so.FindProperty("_selectPanel").objectReferenceValue = panel;
            so.FindProperty("_buttonContainer").objectReferenceValue = crt;

            var rosterProp = so.FindProperty("_heroRoster");
            rosterProp.arraySize = roster.Length;
            for (int i = 0; i < roster.Length; i++)
            {
                rosterProp.GetArrayElementAtIndex(i).objectReferenceValue = roster[i];
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void AddIfExists(List<UpgradeDefinitionSO> list, string path)
        {
            var asset = AssetDatabase.LoadAssetAtPath<UpgradeDefinitionSO>(path);
            if (asset != null) list.Add(asset);
        }

        private static void EnsureFolder(string parent, string child)
        {
            if (!AssetDatabase.IsValidFolder($"{parent}/{child}"))
            {
                AssetDatabase.CreateFolder(parent, child);
            }
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
