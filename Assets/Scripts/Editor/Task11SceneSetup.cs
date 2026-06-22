#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Wavekeep.Data;

namespace Wavekeep.EditorTools
{
    /// <summary>
    /// Authors the Task 11 content: per-hero exclusive <see cref="UpgradeDefinitionSO"/> pools (2 per
    /// existing hero), flags each hero's ULTIMATE as a status-delivery ability, and demonstrates all
    /// three status effects (Freeze/Slow on Frost Warden, Burn on Bolt Striker).
    ///
    /// Status-delivery design (documented): the status DATA lives on the upgrade; the
    /// <c>AbilityDefinitionSO.AppliesStatusEffects</c> flag picks WHICH ability delivers held statuses.
    /// We flag the ultimate (deliberate, infrequent) and not the auto-firing basic, so status effects
    /// aren't spammed every frame — this is what makes "freeze → resume" observable. Fully data-driven:
    /// no per-hero code anywhere.
    ///
    /// Run "Wavekeep/Setup Task 11 (Hero Upgrades &amp; Status)" after the Task 04/05 setups, then save
    /// the scene. Editor-only; not part of the runtime build. (No scene objects change — the level-up
    /// picker learns the exclusive pool at runtime via HeroSelectedEvent.)
    /// </summary>
    public static class Task11SceneSetup
    {
        private const string UpgradeFolder = "Assets/Data/Upgrades";

        private const string HeroBoltStrikerPath = "Assets/Data/Heroes/Hero_BoltStriker.asset";
        private const string HeroFrostWardenPath = "Assets/Data/Heroes/Hero_FrostWarden.asset";
        private const string UltimateNovaPath = "Assets/Data/Abilities/UltimateNova.asset";     // Bolt Striker ult
        private const string UltimateIciclePath = "Assets/Data/Abilities/UltimateIcicle.asset"; // Frost Warden ult

        [MenuItem("Wavekeep/Setup Task 11 (Hero Upgrades & Status)")]
        public static void SetupScene()
        {
            var boltStriker = AssetDatabase.LoadAssetAtPath<HeroDefinitionSO>(HeroBoltStrikerPath);
            var frostWarden = AssetDatabase.LoadAssetAtPath<HeroDefinitionSO>(HeroFrostWardenPath);
            var nova = AssetDatabase.LoadAssetAtPath<AbilityDefinitionSO>(UltimateNovaPath);
            var icicle = AssetDatabase.LoadAssetAtPath<AbilityDefinitionSO>(UltimateIciclePath);
            if (boltStriker == null || frostWarden == null || nova == null || icicle == null)
            {
                Debug.LogError("[Task11SceneSetup] Missing hero/ability assets. Run the Task 04 and Task 05 setups first.");
                return;
            }

            EnsureFolder("Assets/Data", "Upgrades");

            // --- Frost Warden exclusives: Freeze + Slow (the Frost example, plus a Slow companion). ---
            var permafrost = CreateExclusiveUpgrade(
                "Upgrade_Permafrost", "Permafrost", UpgradeTag.Slow,
                status: true, StatusEffectType.Freeze, magnitude: 0f, duration: 2.5f, flatDamage: 0f);
            var bitingChill = CreateExclusiveUpgrade(
                "Upgrade_BitingChill", "Biting Chill", UpgradeTag.Slow,
                status: true, StatusEffectType.Slow, magnitude: 0.5f, duration: 3f, flatDamage: 0f);

            // --- Bolt Striker exclusives: Burn + a pure stat upgrade. ---
            var searingBolts = CreateExclusiveUpgrade(
                "Upgrade_SearingBolts", "Searing Bolts", UpgradeTag.DoT,
                status: true, StatusEffectType.Burn, magnitude: 4f, duration: 3f, flatDamage: 0f);
            var overcharge = CreateExclusiveUpgrade(
                "Upgrade_Overcharge", "Overcharge", UpgradeTag.SingleTarget,
                status: false, StatusEffectType.Freeze, magnitude: 0f, duration: 0f, flatDamage: 12f);

            AssetDatabase.SaveAssets();

            // --- Flag each hero's ultimate as the status-delivery ability. ---
            SetAppliesStatusEffects(icicle, true); // Frost Warden ultimate delivers Freeze/Slow
            SetAppliesStatusEffects(nova, true);   // Bolt Striker ultimate delivers Burn

            // --- Assign exclusive pools to the heroes. ---
            SetExclusiveUpgrades(frostWarden, new List<UpgradeDefinitionSO> { permafrost, bitingChill });
            SetExclusiveUpgrades(boltStriker, new List<UpgradeDefinitionSO> { searingBolts, overcharge });

            AssetDatabase.SaveAssets();
            Debug.Log("[Task11SceneSetup] Hero-exclusive upgrades + status effects authored. " +
                      "Play as Frost Warden, pick 'Permafrost' on level-up, press U on an enemy → it freezes ~2.5s then resumes.");
        }

        private static UpgradeDefinitionSO CreateExclusiveUpgrade(
            string fileName, string displayName, UpgradeTag tag,
            bool status, StatusEffectType statusType, float magnitude, float duration, float flatDamage)
        {
            string path = $"{UpgradeFolder}/{fileName}.asset";
            var upgrade = AbilityAssetUtil.LoadOrCreate<UpgradeDefinitionSO>(path);
            var so = new SerializedObject(upgrade);
            so.FindProperty("_upgradeName").stringValue = displayName;
            so.FindProperty("_effectType").enumValueIndex = (int)UpgradeEffectType.FlatDamageBonus;
            so.FindProperty("_effectValue").floatValue = flatDamage;

            var tags = so.FindProperty("_tags");
            tags.arraySize = 1;
            tags.GetArrayElementAtIndex(0).enumValueIndex = (int)tag;

            so.FindProperty("_appliesStatusEffect").boolValue = status;
            so.FindProperty("_statusEffectType").enumValueIndex = (int)statusType;
            so.FindProperty("_statusMagnitude").floatValue = magnitude;
            so.FindProperty("_statusDuration").floatValue = duration;
            so.ApplyModifiedPropertiesWithoutUndo();
            return upgrade;
        }

        private static void SetAppliesStatusEffects(AbilityDefinitionSO ability, bool value)
        {
            var so = new SerializedObject(ability);
            so.FindProperty("_appliesStatusEffects").boolValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetExclusiveUpgrades(HeroDefinitionSO hero, List<UpgradeDefinitionSO> upgrades)
        {
            var so = new SerializedObject(hero);
            var list = so.FindProperty("_exclusiveUpgrades");
            list.arraySize = upgrades.Count;
            for (int i = 0; i < upgrades.Count; i++)
            {
                list.GetArrayElementAtIndex(i).objectReferenceValue = upgrades[i];
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
    }
}
#endif
