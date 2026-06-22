#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Wavekeep.Core;
using Wavekeep.Data;
using Wavekeep.Runtime;

namespace Wavekeep.EditorTools
{
    /// <summary>
    /// Authors the Task 12 gear test content: sample <see cref="GearItemSO"/>/<see cref="ArtifactItemSO"/>
    /// assets spanning all six slots, several rarities and all four modifier types; a
    /// <see cref="GearCatalogSO"/> registering them; wires the catalog onto the
    /// <c>GameSessionBootstrap</c>; and drops a <see cref="GearDebugController"/> into the scene for
    /// grant/equip/persist verification (G/J/L/I keys).
    ///
    /// Built in code like Tasks 01–11. Run "Wavekeep/Setup Task 12 (Gear Core)" after the earlier
    /// setups, then save the scene. Editor-only; not part of the runtime build.
    /// </summary>
    public static class Task12SceneSetup
    {
        private const string GearFolder = "Assets/Data/Gear";
        private const string CatalogPath = GearFolder + "/GearCatalog.asset";

        [MenuItem("Wavekeep/Setup Task 12 (Gear Core)")]
        public static void SetupScene()
        {
            var bootstrap = Object.FindFirstObjectByType<GameSessionBootstrap>();
            if (bootstrap == null)
            {
                Debug.LogError("[Task12SceneSetup] No GameSessionBootstrap in scene. Run the Task 01 setup first.");
                return;
            }

            EnsureFolder("Assets/Data", "Gear");

            // --- Sample items: one per slot, varied rarities, covering all four modifier types. ---
            var items = new List<LootItemSO>
            {
                CreateGear("Gear_CommonHelm", "gear_common_helm", "Worn Helm", GearSlot.Helmet, Rarity.Common,
                    (AbilityModifierType.DamageFlatBonus, 5f)),
                CreateGear("Gear_UncommonBody", "gear_uncommon_body", "Sturdy Cuirass", GearSlot.Body, Rarity.Uncommon,
                    (AbilityModifierType.DamageMultiplier, 1.2f)),
                CreateGear("Gear_RareHands", "gear_rare_hands", "Swift Gauntlets", GearSlot.Hands, Rarity.Rare,
                    (AbilityModifierType.CooldownMultiplier, 0.85f)),
                CreateGear("Gear_EpicLegs", "gear_epic_legs", "Farseer Greaves", GearSlot.Legs, Rarity.Epic,
                    (AbilityModifierType.RangeMultiplier, 1.25f)),
                CreateGear("Gear_LegendaryFeet", "gear_legendary_feet", "Boots of Wrath", GearSlot.Feet, Rarity.Legendary,
                    (AbilityModifierType.DamageFlatBonus, 15f)),
                CreateArtifact("Artifact_UniqueCore", "artifact_unique_core", "Heartstone Core", Rarity.Unique,
                    (AbilityModifierType.DamageMultiplier, 1.3f)),
            };

            var catalog = CreateCatalog(items);
            AssetDatabase.SaveAssets();

            // --- Wire the catalog onto the bootstrap so the GearManager can resolve saved item ids. ---
            var bso = new SerializedObject(bootstrap);
            bso.FindProperty("_gearCatalog").objectReferenceValue = catalog;
            bso.ApplyModifiedPropertiesWithoutUndo();

            // --- Debug controller (idempotent). ---
            DestroyIfExists("GearDebug");
            var debugGo = new GameObject("GearDebug", typeof(GearDebugController));
            var dso = new SerializedObject(debugGo.GetComponent<GearDebugController>());
            dso.FindProperty("_bootstrap").objectReferenceValue = bootstrap;
            var list = dso.FindProperty("_sampleItems");
            list.arraySize = items.Count;
            for (int i = 0; i < items.Count; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = items[i];
            dso.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log("[Task12SceneSetup] Gear core wired. Play, pick a hero, then: G=grant, J=equip first owned, " +
                      "I=inspect (logs gear-inclusive damage), L=unequip. Close+reopen to confirm persistence. Save the scene (Ctrl+S).");
        }

        private static GearItemSO CreateGear(
            string fileName, string itemId, string itemName, GearSlot slot, Rarity rarity,
            params (AbilityModifierType type, float value)[] modifiers)
        {
            var asset = AbilityAssetUtil.LoadOrCreate<GearItemSO>($"{GearFolder}/{fileName}.asset");
            var so = new SerializedObject(asset);
            WriteBaseFields(so, itemId, itemName, rarity, modifiers);
            so.FindProperty("_slot").enumValueIndex = (int)slot;
            so.ApplyModifiedPropertiesWithoutUndo();
            return asset;
        }

        private static ArtifactItemSO CreateArtifact(
            string fileName, string itemId, string itemName, Rarity rarity,
            params (AbilityModifierType type, float value)[] modifiers)
        {
            var asset = AbilityAssetUtil.LoadOrCreate<ArtifactItemSO>($"{GearFolder}/{fileName}.asset");
            var so = new SerializedObject(asset);
            WriteBaseFields(so, itemId, itemName, rarity, modifiers);
            so.ApplyModifiedPropertiesWithoutUndo();
            return asset;
        }

        private static void WriteBaseFields(
            SerializedObject so, string itemId, string itemName, Rarity rarity,
            (AbilityModifierType type, float value)[] modifiers)
        {
            so.FindProperty("_itemId").stringValue = itemId;
            so.FindProperty("_itemName").stringValue = itemName;
            so.FindProperty("_rarity").enumValueIndex = (int)rarity;

            var mods = so.FindProperty("_statModifiers");
            mods.arraySize = modifiers.Length;
            for (int i = 0; i < modifiers.Length; i++)
            {
                var element = mods.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("_modifierType").enumValueIndex = (int)modifiers[i].type;
                element.FindPropertyRelative("_value").floatValue = modifiers[i].value;
            }
        }

        private static GearCatalogSO CreateCatalog(List<LootItemSO> items)
        {
            var catalog = AbilityAssetUtil.LoadOrCreate<GearCatalogSO>(CatalogPath);
            var so = new SerializedObject(catalog);
            var list = so.FindProperty("_items");
            list.arraySize = items.Count;
            for (int i = 0; i < items.Count; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = items[i];
            so.ApplyModifiedPropertiesWithoutUndo();
            return catalog;
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
            if (existing != null) Object.DestroyImmediate(existing);
        }
    }
}
#endif
