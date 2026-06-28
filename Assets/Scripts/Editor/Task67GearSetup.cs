#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Wavekeep.Data;
using Wavekeep.Runtime;

namespace Wavekeep.EditorTools
{
    /// <summary>
    /// Task 67 — authors the new gear-redesign templates (gear redesign part 1) and wires them so the debug
    /// spawn path works:
    /// <list type="bullet">
    /// <item>Six <see cref="GearBaseSO"/> (one per slot), each mapping to a LIVE stat: Helmet/Feet → flat damage,
    /// Body → damage ×, Hands → cooldown ×, Legs → range ×, Artifact → Luck. Per-rarity implicit values authored.</item>
    /// <item>Six <see cref="AffixDefinitionSO"/> (the shared pool), including a Luck affix and one tagged affix
    /// (Elemental_Fire) to exercise the optional tag field.</item>
    /// <item>A <see cref="GearAffixCountConfigSO"/>: Common 0 / Uncommon 1 / Rare 2 / Epic 3 / Legendary 4 /
    /// Unique 0 (hand-authored, no random rolls), referencing the pool.</item>
    /// <item>Registers all bases + affixes into the existing <c>GearCatalog</c> so saved instances resolve.</item>
    /// <item>Wires the open scene's <see cref="GearDebugController"/> base list + config (press G to spawn).</item>
    /// </list>
    /// Values are placeholders (balance is a separate concern). Run "Wavekeep/Setup Task 67 (Gear Redesign Data)"
    /// from the gameplay scene, then save the scene. Idempotent. Editor-only.
    /// </summary>
    public static class Task67GearSetup
    {
        private const string GearFolder = "Assets/Data/Gear";
        private const string BasesFolder = GearFolder + "/Bases";
        private const string AffixesFolder = GearFolder + "/Affixes";
        private const string CatalogPath = GearFolder + "/GearCatalog.asset";
        private const string ConfigPath = GearFolder + "/GearAffixCountConfig.asset";

        [MenuItem("Wavekeep/Setup Task 67 (Gear Redesign Data)")]
        public static void Setup()
        {
            EnsureFolder(GearFolder, "Bases");
            EnsureFolder(GearFolder, "Affixes");

            var bases = BuildBases();
            var affixes = BuildAffixes();
            var config = BuildConfig(affixes);
            RegisterInCatalog(bases, affixes);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            WireDebugController(bases, config);

            Debug.Log($"[Task67] Gear redesign data authored: {bases.Count} bases, {affixes.Count} affixes, " +
                      "1 affix-count config; registered in GearCatalog. If a GearDebugController was wired, save " +
                      "the scene (Ctrl+S). Press G in play to spawn a test instance.");
        }

        // --- Bases -----------------------------------------------------------------------------------

        private static List<GearBaseSO> BuildBases()
        {
            var list = new List<GearBaseSO>
            {
                Base("base_helmet", "Helm", GearSlot.Helmet, GearStatType.DamageFlatBonus,
                    new[] { 5f, 8f, 12f, 16f, 22f, 30f }),
                Base("base_body", "Cuirass", GearSlot.Body, GearStatType.DamageMultiplier,
                    new[] { 1.05f, 1.12f, 1.20f, 1.30f, 1.45f, 1.65f }),
                Base("base_hands", "Gauntlets", GearSlot.Hands, GearStatType.CooldownMultiplier,
                    new[] { 0.97f, 0.93f, 0.88f, 0.82f, 0.74f, 0.62f }),
                Base("base_legs", "Greaves", GearSlot.Legs, GearStatType.RangeMultiplier,
                    new[] { 1.05f, 1.10f, 1.18f, 1.28f, 1.42f, 1.60f }),
                Base("base_feet", "Boots", GearSlot.Feet, GearStatType.DamageFlatBonus,
                    new[] { 6f, 10f, 14f, 18f, 24f, 32f }),
                Base("base_artifact", "Core", GearSlot.Artifact, GearStatType.Luck,
                    new[] { 2f, 4f, 6f, 9f, 13f, 18f }),
            };
            return list;
        }

        private static GearBaseSO Base(string id, string name, GearSlot slot, GearStatType stat, float[] perRarity)
        {
            var asset = AbilityAssetUtil.LoadOrCreate<GearBaseSO>($"{BasesFolder}/GearBase_{name}.asset");
            var so = new SerializedObject(asset);
            so.FindProperty("_baseId").stringValue = id;
            so.FindProperty("_displayName").stringValue = name;
            so.FindProperty("_slot").enumValueIndex = (int)slot;
            so.FindProperty("_implicitStat").enumValueIndex = (int)stat;
            SetFloatArray(so.FindProperty("_implicitValueByRarity"), perRarity);
            so.ApplyModifiedPropertiesWithoutUndo();
            return asset;
        }

        // --- Affixes ---------------------------------------------------------------------------------

        private static List<AffixDefinitionSO> BuildAffixes()
        {
            return new List<AffixDefinitionSO>
            {
                Affix("affix_sharpened", "Sharpened", GearStatType.DamageFlatBonus, 3f, 12f, 10, false, default),
                Affix("affix_empowered", "Empowered", GearStatType.DamageMultiplier, 1.05f, 1.25f, 8, false, default),
                Affix("affix_swift", "Swift", GearStatType.CooldownMultiplier, 0.80f, 0.95f, 6, false, default),
                Affix("affix_farsight", "Farsight", GearStatType.RangeMultiplier, 1.05f, 1.25f, 6, false, default),
                Affix("affix_lucky", "Lucky", GearStatType.Luck, 1f, 5f, 5, false, default),
                Affix("affix_emberforged", "Emberforged", GearStatType.DamageMultiplier, 1.05f, 1.20f, 4,
                    true, UpgradeTag.Elemental_Fire), // exercises the optional tag field
            };
        }

        private static AffixDefinitionSO Affix(string id, string name, GearStatType stat, float min, float max,
            int weight, bool hasTag, UpgradeTag tag)
        {
            var asset = AbilityAssetUtil.LoadOrCreate<AffixDefinitionSO>($"{AffixesFolder}/Affix_{name}.asset");
            var so = new SerializedObject(asset);
            so.FindProperty("_affixId").stringValue = id;
            so.FindProperty("_displayName").stringValue = name;

            var effect = so.FindProperty("_effect");
            effect.FindPropertyRelative("_kind").enumValueIndex = (int)GearEffectKind.StatModifier;
            effect.FindPropertyRelative("_stat").enumValueIndex = (int)stat;

            so.FindProperty("_minValue").floatValue = min;
            so.FindProperty("_maxValue").floatValue = max;
            so.FindProperty("_eligibleSlots").arraySize = 0; // empty = all slots
            so.FindProperty("_drawWeight").intValue = weight;
            so.FindProperty("_hasTag").boolValue = hasTag;
            so.FindProperty("_tag").enumValueIndex = (int)tag;
            so.ApplyModifiedPropertiesWithoutUndo();
            return asset;
        }

        // --- Config + catalog ------------------------------------------------------------------------

        private static GearAffixCountConfigSO BuildConfig(List<AffixDefinitionSO> affixes)
        {
            var asset = AbilityAssetUtil.LoadOrCreate<GearAffixCountConfigSO>(ConfigPath);
            var so = new SerializedObject(asset);
            SetIntArray(so.FindProperty("_affixCountByRarity"), new[] { 0, 1, 2, 3, 4, 0 });
            SetObjectList(so.FindProperty("_affixPool"), affixes);
            so.ApplyModifiedPropertiesWithoutUndo();
            return asset;
        }

        private static void RegisterInCatalog(List<GearBaseSO> bases, List<AffixDefinitionSO> affixes)
        {
            var catalog = AbilityAssetUtil.LoadOrCreate<GearCatalogSO>(CatalogPath);
            var so = new SerializedObject(catalog);
            SetObjectList(so.FindProperty("_bases"), bases);
            SetObjectList(so.FindProperty("_affixes"), affixes);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void WireDebugController(List<GearBaseSO> bases, GearAffixCountConfigSO config)
        {
            var debug = Object.FindFirstObjectByType<GearDebugController>();
            if (debug == null)
            {
                Debug.Log("[Task67] No GearDebugController in the open scene — skipped debug wiring (open the " +
                          "gameplay scene and re-run to let G spawn instances).");
                return;
            }

            var so = new SerializedObject(debug);
            SetObjectList(so.FindProperty("_debugBases"), bases);
            so.FindProperty("_affixConfig").objectReferenceValue = config;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(debug.gameObject.scene);
            Debug.Log("[Task67] Wired debug bases + affix config into the scene's GearDebugController. Save the scene.");
        }

        // --- helpers ---------------------------------------------------------------------------------

        private static void SetFloatArray(SerializedProperty prop, float[] values)
        {
            prop.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) prop.GetArrayElementAtIndex(i).floatValue = values[i];
        }

        private static void SetIntArray(SerializedProperty prop, int[] values)
        {
            prop.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) prop.GetArrayElementAtIndex(i).intValue = values[i];
        }

        private static void SetObjectList<T>(SerializedProperty prop, List<T> items) where T : Object
        {
            prop.arraySize = items.Count;
            for (int i = 0; i < items.Count; i++) prop.GetArrayElementAtIndex(i).objectReferenceValue = items[i];
        }

        private static void EnsureFolder(string parent, string child)
        {
            if (!AssetDatabase.IsValidFolder($"{parent}/{child}"))
                AssetDatabase.CreateFolder(parent, child);
        }
    }
}
#endif
