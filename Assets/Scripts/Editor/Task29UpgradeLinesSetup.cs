#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Wavekeep.Data;

namespace Wavekeep.EditorTools
{
    /// <summary>
    /// Task 29 content migration: reshapes Frost Warden's existing hero-exclusive upgrades into the new
    /// upgrade-LINE model and authors a placeholder apex talent, then rewires the hero to reference lines +
    /// apexes instead of the old flat exclusive-upgrade list.
    ///
    /// Each line's per-tier effect REFERENCES an existing <see cref="UpgradeDefinitionSO"/> asset (a true
    /// 1:1 reshape — picking a tier Adds that upgrade to the run's UpgradeInventory exactly as before), so
    /// no ability behaviour changes. Mapping (old upgrade → new line/tier):
    /// <list type="bullet">
    /// <item>Line "Glacial Onslaught" (Basic): T1 Biting Chill → T2 Permafrost → T3 Chain Frost.</item>
    /// <item>Line "Endless Winter" (Ultimate): T1 Glacial Zone → T2 Extended Zone → T3 Ultimate Freeze.</item>
    /// </list>
    /// Apex "Absolute Zero" requires BOTH lines at Tier 3 and triggers a new auto-firing AoE ability.
    ///
    /// Run "Wavekeep/Setup Task 29 (Upgrade Lines)" after the Task 05/07/11 setups. Idempotent. Editor-only.
    /// </summary>
    public static class Task29UpgradeLinesSetup
    {
        private const string LineFolder = "Assets/Data/UpgradeLines";
        private const string AbilityFolder = "Assets/Data/Abilities";
        private const string UpgradeFolder = "Assets/Data/Upgrades";
        private const string HeroFrostWardenPath = "Assets/Data/Heroes/Hero_FrostWarden.asset";

        [MenuItem("Wavekeep/Setup Task 29 (Upgrade Lines)")]
        public static void SetupScene()
        {
            var hero = AssetDatabase.LoadAssetAtPath<HeroDefinitionSO>(HeroFrostWardenPath);
            if (hero == null)
            {
                Debug.LogError("[Task29] Hero_FrostWarden not found. Run 'Wavekeep/Setup Task 05 (Heroes)' first.");
                return;
            }

            EnsureFolder("Assets/Data", "UpgradeLines");

            // --- Line 1: Basic skill — three frost-stacking upgrades reshaped into tiers. ---
            var line1 = CreateLine("UpgradeLine_GlacialOnslaught", "Glacial Onslaught", hero, AbilityRole.Basic,
                new[]
                {
                    ("Frost on hit — slows struck enemies.", "Upgrade_BitingChill"),
                    ("Frost can freeze solid at max stacks.", "Upgrade_Permafrost"),
                    ("A max-stack freeze chains to nearby enemies.", "Upgrade_ChainFrost"),
                });

            // --- Line 2: Ultimate skill — three glacial-zone upgrades reshaped into tiers. ---
            var line2 = CreateLine("UpgradeLine_EndlessWinter", "Endless Winter", hero, AbilityRole.Ultimate,
                new[]
                {
                    ("The ultimate leaves a slowing, damaging glacial zone.", "Upgrade_GlacialZone"),
                    ("The glacial zone lingers far longer.", "Upgrade_ExtendedZone"),
                    ("Heavily-frosted enemies in the zone freeze solid.", "Upgrade_UltimateFreeze"),
                });

            // --- Apex ability: a new, independent auto-firing AoE (placeholder values). ---
            var apexAbility = CreateApexAbility("Ability_AbsoluteZero", "Absolute Zero");

            // --- Apex talent: requires BOTH lines at max tier. ---
            var apex = CreateApex("ApexTalent_AbsoluteZero", "Absolute Zero", hero,
                new List<UpgradeLineDefinitionSO> { line1, line2 }, apexAbility);

            // --- Rewire the hero: lines + apexes replace the old exclusive-upgrade list. ---
            WireHero(hero, new List<UpgradeLineDefinitionSO> { line1, line2 },
                new List<ApexTalentDefinitionSO> { apex });

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Task29] Upgrade lines migrated: Frost Warden now owns 2 lines (Glacial Onslaught / " +
                      "Endless Winter, 3 tiers each) + the 'Absolute Zero' apex (needs both lines at T3). " +
                      "Play as Frost Warden, level up both lines to T3 → apex auto-fires on its own cooldown.");
        }

        private static UpgradeLineDefinitionSO CreateLine(
            string fileName, string lineName, HeroDefinitionSO hero, AbilityRole skill,
            (string description, string upgradeFile)[] tiers)
        {
            var line = AbilityAssetUtil.LoadOrCreate<UpgradeLineDefinitionSO>($"{LineFolder}/{fileName}.asset");
            var so = new SerializedObject(line);
            so.FindProperty("_hero").objectReferenceValue = hero;
            so.FindProperty("_skill").enumValueIndex = (int)skill;
            so.FindProperty("_lineName").stringValue = lineName;

            var tierList = so.FindProperty("_tiers");
            tierList.arraySize = tiers.Length;
            for (int i = 0; i < tiers.Length; i++)
            {
                var element = tierList.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("_description").stringValue = tiers[i].description;

                var effect = AssetDatabase.LoadAssetAtPath<UpgradeDefinitionSO>(
                    $"{UpgradeFolder}/{tiers[i].upgradeFile}.asset");
                if (effect == null)
                {
                    Debug.LogWarning($"[Task29] Upgrade '{tiers[i].upgradeFile}' not found for line '{lineName}' " +
                                     $"tier {i + 1}; the tier will have no effect. (Run the Task 11/19 setups first.)");
                }
                element.FindPropertyRelative("_effect").objectReferenceValue = effect;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            return line;
        }

        private static AbilityDefinitionSO CreateApexAbility(string fileName, string abilityName)
        {
            var ability = AbilityAssetUtil.LoadOrCreate<AbilityDefinitionSO>($"{AbilityFolder}/{fileName}.asset");
            var so = new SerializedObject(ability);
            so.FindProperty("_abilityName").stringValue = abilityName;
            so.FindProperty("_baseDamage").floatValue = 40f;       // placeholder
            so.FindProperty("_baseCooldown").floatValue = 6f;      // its own cooldown
            // Large caster-centred AoE so the auto-fire reliably connects (matches the project's
            // "range covers the whole arena" precedent) — makes the apex trigger obvious in a playtest.
            so.FindProperty("_range").floatValue = 30f;
            so.FindProperty("_targetingType").enumValueIndex = (int)AbilityTargetingType.AreaOfEffect;
            so.ApplyModifiedPropertiesWithoutUndo();
            return ability;
        }

        private static ApexTalentDefinitionSO CreateApex(
            string fileName, string apexName, HeroDefinitionSO hero,
            List<UpgradeLineDefinitionSO> requiredLines, AbilityDefinitionSO ability)
        {
            var apex = AbilityAssetUtil.LoadOrCreate<ApexTalentDefinitionSO>($"{LineFolder}/{fileName}.asset");
            var so = new SerializedObject(apex);
            so.FindProperty("_hero").objectReferenceValue = hero;
            so.FindProperty("_apexName").stringValue = apexName;
            so.FindProperty("_ability").objectReferenceValue = ability;

            var lines = so.FindProperty("_requiredLines");
            lines.arraySize = requiredLines.Count;
            for (int i = 0; i < requiredLines.Count; i++)
                lines.GetArrayElementAtIndex(i).objectReferenceValue = requiredLines[i];
            so.ApplyModifiedPropertiesWithoutUndo();
            return apex;
        }

        private static void WireHero(
            HeroDefinitionSO hero, List<UpgradeLineDefinitionSO> lines, List<ApexTalentDefinitionSO> apexes)
        {
            var so = new SerializedObject(hero);

            var lineProp = so.FindProperty("_upgradeLines");
            lineProp.arraySize = lines.Count;
            for (int i = 0; i < lines.Count; i++)
                lineProp.GetArrayElementAtIndex(i).objectReferenceValue = lines[i];

            var apexProp = so.FindProperty("_apexTalents");
            apexProp.arraySize = apexes.Count;
            for (int i = 0; i < apexes.Count; i++)
                apexProp.GetArrayElementAtIndex(i).objectReferenceValue = apexes[i];

            // The old _exclusiveUpgrades field is gone from HeroDefinitionSO; SaveAssets rewrites the asset
            // without that orphaned data, so no explicit clear is needed.
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void EnsureFolder(string parent, string child)
        {
            if (!AssetDatabase.IsValidFolder($"{parent}/{child}"))
                AssetDatabase.CreateFolder(parent, child);
        }
    }
}
#endif
