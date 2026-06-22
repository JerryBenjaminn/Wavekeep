#if UNITY_EDITOR
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Wavekeep.Core;
using Wavekeep.Data;
using Wavekeep.UI;

namespace Wavekeep.EditorTools
{
    /// <summary>
    /// Builds the Task 07 level-up flow on top of Tasks 01–06: expands the shared
    /// <see cref="UpgradeDefinitionSO"/> pool to 8 distinct upgrades across the full
    /// <see cref="UpgradeTag"/> set, and adds a card-picker Canvas screen wired to a
    /// <see cref="LevelUpCardPicker"/> (the picker generates its card slots at runtime, so this builds
    /// only the frame: panel, title, card container).
    ///
    /// Built in code for the same reason as Tasks 01–06 (scenes/assets can't be reliably hand-authored
    /// as YAML outside the editor). Run "Wavekeep/Setup Task 07 (Level-Up)" after the Task 01–06
    /// setups, then save the scene. Editor-only; not part of the runtime build.
    /// </summary>
    public static class Task07SceneSetup
    {
        private const string UpgradeFolder = "Assets/Data/Upgrades";

        // Reuse Task 04's three upgrade paths so no duplicate assets are created, then add five more.
        private static readonly string GlacialPath = $"{UpgradeFolder}/Upgrade_GlacialField.asset";
        private static readonly string VenomPath = $"{UpgradeFolder}/Upgrade_VenomCoating.asset";
        private static readonly string ShadowPath = $"{UpgradeFolder}/Upgrade_ShadowInfusion.asset";
        private static readonly string StormPath = $"{UpgradeFolder}/Upgrade_StormCaller.asset";
        private static readonly string HairTriggerPath = $"{UpgradeFolder}/Upgrade_HairTrigger.asset";

        [MenuItem("Wavekeep/Setup Task 07 (Level-Up)")]
        public static void SetupScene()
        {
            var bootstrap = Object.FindFirstObjectByType<GameSessionBootstrap>();
            var canvas = Object.FindFirstObjectByType<Canvas>();
            if (bootstrap == null || canvas == null)
            {
                Debug.LogError("[Task07SceneSetup] Missing GameSessionBootstrap / Canvas. Run the Task 01–06 setups first.");
                return;
            }

            // --- Expand the shared upgrade pool to 8 distinct upgrades across every tag. ---
            EnsureFolder("Assets/Data", "Upgrades");
            var pool = new List<UpgradeDefinitionSO>
            {
                CreateUpgrade(Task04SceneSetup.PrecisionPath, "Precision", UpgradeTag.SingleTarget, UpgradeEffectType.FlatDamageBonus, 5f),
                CreateUpgrade(Task04SceneSetup.FirePath, "Fire Infusion", UpgradeTag.Elemental_Fire, UpgradeEffectType.FlatDamageBonus, 5f),
                CreateUpgrade(Task04SceneSetup.MaelstromPath, "Maelstrom", UpgradeTag.AoE, UpgradeEffectType.AoeRadiusBonus, 3f),
                CreateUpgrade(GlacialPath, "Glacial Field", UpgradeTag.Slow, UpgradeEffectType.AoeRadiusBonus, 2f),
                CreateUpgrade(VenomPath, "Venom Coating", UpgradeTag.DoT, UpgradeEffectType.FlatDamageBonus, 6f),
                CreateUpgrade(ShadowPath, "Shadow Infusion", UpgradeTag.Elemental_Dark, UpgradeEffectType.FlatDamageBonus, 7f),
                CreateUpgrade(StormPath, "Storm Caller", UpgradeTag.AoE, UpgradeEffectType.FlatDamageBonus, 8f),
                CreateUpgrade(HairTriggerPath, "Hair Trigger", UpgradeTag.SingleTarget, UpgradeEffectType.CooldownReductionPercent, 15f),
            };
            AssetDatabase.SaveAssets();

            // --- Scene cleanup (idempotent). ---
            DestroyIfExists("LevelUpPanel");
            DestroyIfExists("LevelUpPicker");

            // --- Card-picker UI frame. ---
            BuildLevelUpScreen(canvas, bootstrap, pool);

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log("[Task07SceneSetup] Level-up picker built. Play: gain XP → on level-up the run pauses and cards appear → pick one to resume. Save the scene (Ctrl+S).");
        }

        private static UpgradeDefinitionSO CreateUpgrade(
            string path, string name, UpgradeTag tag, UpgradeEffectType effectType, float effectValue)
        {
            var upgrade = AbilityAssetUtil.LoadOrCreate<UpgradeDefinitionSO>(path);
            var so = new SerializedObject(upgrade);
            so.FindProperty("_upgradeName").stringValue = name;
            so.FindProperty("_effectType").enumValueIndex = (int)effectType;
            so.FindProperty("_effectValue").floatValue = effectValue;

            var tags = so.FindProperty("_tags");
            tags.arraySize = 1;
            tags.GetArrayElementAtIndex(0).enumValueIndex = (int)tag;
            so.ApplyModifiedPropertiesWithoutUndo();
            return upgrade;
        }

        private static void BuildLevelUpScreen(Canvas canvas, GameSessionBootstrap bootstrap, List<UpgradeDefinitionSO> pool)
        {
            if (TMP_Settings.defaultFontAsset == null)
            {
                Debug.LogWarning("[Task07SceneSetup] TMP has no default font asset. If card text doesn't render, " +
                                 "import via Window > TextMeshPro > Import TMP Essential Resources, then re-run.");
            }

            // Full-screen dimmed panel (controller hides it on Start; shown only during a pick).
            var panel = new GameObject("LevelUpPanel", typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(canvas.transform, false);
            var prt = (RectTransform)panel.transform;
            prt.anchorMin = Vector2.zero;
            prt.anchorMax = Vector2.one;
            prt.offsetMin = Vector2.zero;
            prt.offsetMax = Vector2.zero;
            panel.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.8f);

            // Title.
            var titleGo = new GameObject("Title", typeof(RectTransform));
            titleGo.transform.SetParent(panel.transform, false);
            var title = titleGo.AddComponent<TextMeshProUGUI>();
            title.text = "Level Up — Choose an Upgrade";
            title.fontSize = 40f;
            title.alignment = TextAlignmentOptions.Center;
            title.color = Color.white;
            if (TMP_Settings.defaultFontAsset != null) title.font = TMP_Settings.defaultFontAsset;
            var trt = title.rectTransform;
            trt.anchorMin = new Vector2(0.5f, 1f);
            trt.anchorMax = new Vector2(0.5f, 1f);
            trt.pivot = new Vector2(0.5f, 1f);
            trt.anchoredPosition = new Vector2(0f, -60f);
            trt.sizeDelta = new Vector2(800f, 70f);

            // Card container with a horizontal layout — the controller fills it at runtime.
            var container = new GameObject("CardContainer", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            container.transform.SetParent(panel.transform, false);
            var crt = (RectTransform)container.transform;
            crt.anchorMin = new Vector2(0.5f, 0.5f);
            crt.anchorMax = new Vector2(0.5f, 0.5f);
            crt.pivot = new Vector2(0.5f, 0.5f);
            crt.anchoredPosition = Vector2.zero;
            crt.sizeDelta = new Vector2(760f, 320f);
            var hlg = container.GetComponent<HorizontalLayoutGroup>();
            hlg.spacing = 24f;
            hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.childControlWidth = false;
            hlg.childControlHeight = false;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = false;

            // Controller on a separate root object so hiding the panel doesn't disable it.
            var pickerGo = new GameObject("LevelUpPicker", typeof(LevelUpCardPicker));
            var controller = pickerGo.GetComponent<LevelUpCardPicker>();
            var so = new SerializedObject(controller);
            so.FindProperty("_bootstrap").objectReferenceValue = bootstrap;
            so.FindProperty("_panel").objectReferenceValue = panel;
            so.FindProperty("_cardContainer").objectReferenceValue = crt;

            var poolProp = so.FindProperty("_upgradePool");
            poolProp.arraySize = pool.Count;
            for (int i = 0; i < pool.Count; i++)
            {
                poolProp.GetArrayElementAtIndex(i).objectReferenceValue = pool[i];
            }
            so.ApplyModifiedPropertiesWithoutUndo();
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
