#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Wavekeep.Data;

namespace Wavekeep.EditorTools
{
    /// <summary>
    /// Task 31 — authors Frost Warden's 8 final upgrade lines + 2 apex talents, replacing the placeholder
    /// lines from Task 29. Pure CC + AoE DPS; NO damage-over-time (the ultimate's old DoT is zeroed here).
    /// All effects resolve through the existing UpgradeInventory/AbilityRuntime pipeline plus the Pass-2
    /// persistent ground/zone subsystem (GroundZone/GroundZoneManager): Frozen Ground patches, and the Frost
    /// Zone's slow / Zone Pulse / Absolute Zero growth.
    ///
    /// Documented base values (the ones the task left unspecified): basic "Frost Bolt Burst" base max-targets
    /// = 3 (Wider Burst adds +1/+2/+3 → 4/5/6); Frost Zone base radius = 6m (placeholder). Frost Zone base
    /// slow is 0.25; Deepening Frost OVERRIDES it to 0.30/0.40/0.50 (Set op), it does not stack.
    ///
    /// Run "Wavekeep/Setup Task 31 (Frost Warden Content)" after the Task 05/29 setups. Idempotent. Editor-only.
    /// </summary>
    public static class Task31FrostWardenContentSetup
    {
        private const string UpgradeFolder = "Assets/Data/Upgrades";
        private const string LineFolder = "Assets/Data/UpgradeLines";
        private const string AbilityFolder = "Assets/Data/Abilities";
        private const string HeroPath = "Assets/Data/Heroes/Hero_FrostWarden.asset";
        private const string BasicPath = "Assets/Data/Abilities/BasicFrostNova.asset";
        private const string UltimatePath = "Assets/Data/Abilities/UltimateIcicle.asset";

        [MenuItem("Wavekeep/Setup Task 31 (Frost Warden Content)")]
        public static void SetupScene()
        {
            var hero = AssetDatabase.LoadAssetAtPath<HeroDefinitionSO>(HeroPath);
            var basic = AssetDatabase.LoadAssetAtPath<AbilityDefinitionSO>(BasicPath);
            var ultimate = AssetDatabase.LoadAssetAtPath<AbilityDefinitionSO>(UltimatePath);
            if (hero == null || basic == null || ultimate == null)
            {
                Debug.LogError("[Task31] Missing Frost Warden hero/basic/ultimate assets. Run Task 05/29 setups first.");
                return;
            }

            EnsureFolder("Assets/Data", "Upgrades");
            EnsureFolder("Assets/Data", "UpgradeLines");

            // --- Base value adjustments on the abilities themselves. ---
            SetInt(basic, "_maxTargets", 3);                 // base cap; Wider Burst raises it (assumed base)
            SetFloat(ultimate, "_zoneDotDamagePerSecond", 0f); // Task 31: NO DoT on Frost Warden
            SetFloat(ultimate, "_aoeRadius", 6f);              // Task 33: Frost Zone band DEPTH in front of the wall (placeholder 6m)

            // === BASIC LINES (Frost Bolt Burst) ===

            // Line 1 — Frozen Ground (persistent ice patch at the impact point). FULLY functional (Pass 2).
            var frozenGround = Line("UpgradeLine_FrozenGround", "Frozen Ground", hero, AbilityRole.Basic,
                ("Ice patch on hit: radius 1.5m, 2s, slow 20%.", FrozenGround("Upg_FrozenGround_T1", "Frozen Ground I", 1.5f, 2f, 0.20f)),
                ("Ice patch on hit: radius 2m, 3s, slow 30%.", FrozenGround("Upg_FrozenGround_T2", "Frozen Ground II", 2f, 3f, 0.30f)),
                ("Ice patch on hit: radius 2.5m, 4s, slow 40%.", FrozenGround("Upg_FrozenGround_T3", "Frozen Ground III", 2.5f, 4f, 0.40f)));

            // Line 2 — Wider Burst (radius + max targets). FULLY functional.
            var widerBurst = Line("UpgradeLine_WiderBurst", "Wider Burst", hero, AbilityRole.Basic,
                ("+15% blast radius, +1 max target.", StatMods("Upg_WiderBurst_T1", "Wider Burst I",
                    (UpgradeModifierTarget.BasicRadius, UpgradeModifierOp.Multiply, 1.15f),
                    (UpgradeModifierTarget.BasicMaxTargets, UpgradeModifierOp.Add, 1f))),
                ("+30% blast radius, +2 max targets.", StatMods("Upg_WiderBurst_T2", "Wider Burst II",
                    (UpgradeModifierTarget.BasicRadius, UpgradeModifierOp.Multiply, 1.30f),
                    (UpgradeModifierTarget.BasicMaxTargets, UpgradeModifierOp.Add, 2f))),
                ("+50% blast radius, +3 max targets.", StatMods("Upg_WiderBurst_T3", "Wider Burst III",
                    (UpgradeModifierTarget.BasicRadius, UpgradeModifierOp.Multiply, 1.50f),
                    (UpgradeModifierTarget.BasicMaxTargets, UpgradeModifierOp.Add, 3f))));

            // Line 3 — Shattering Impact (bonus damage vs slowed/frozen, on the hit). FULLY functional.
            var shattering = Line("UpgradeLine_ShatteringImpact", "Shattering Impact", hero, AbilityRole.Basic,
                ("+15% damage vs slowed/frozen targets.", Shatter("Upg_Shattering_T1", "Shattering Impact I", 0.15f)),
                ("+30% damage vs slowed/frozen targets.", Shatter("Upg_Shattering_T2", "Shattering Impact II", 0.30f)),
                ("+50% damage vs slowed/frozen targets.", Shatter("Upg_Shattering_T3", "Shattering Impact III", 0.50f)));

            // Line 4 — Hard Freeze (chance to hard-stun on hit). FULLY functional.
            var hardFreeze = Line("UpgradeLine_HardFreeze", "Hard Freeze", hero, AbilityRole.Basic,
                ("10% chance to freeze for 0.5s on hit.", Freeze("Upg_HardFreeze_T1", "Hard Freeze I", 0.10f, 0.5f)),
                ("20% chance to freeze for 0.75s on hit.", Freeze("Upg_HardFreeze_T2", "Hard Freeze II", 0.20f, 0.75f)),
                ("30% chance to freeze for 1s on hit.", Freeze("Upg_HardFreeze_T3", "Hard Freeze III", 0.30f, 1.0f)));

            // === ULTIMATE LINES (Frost Zone) ===

            // Line 5 — Deepening Frost (override zone slow). FULLY functional (Set op).
            var deepeningFrost = Line("UpgradeLine_DeepeningFrost", "Deepening Frost", hero, AbilityRole.Ultimate,
                ("Frost Zone slow 30% (overrides base 25%).", SlowSet("Upg_Deepening_T1", "Deepening Frost I", 0.30f)),
                ("Frost Zone slow 40%.", SlowSet("Upg_Deepening_T2", "Deepening Frost II", 0.40f)),
                ("Frost Zone slow 50%.", SlowSet("Upg_Deepening_T3", "Deepening Frost III", 0.50f)));

            // Line 6 — Lingering Chill (+zone duration). FULLY functional (Add op).
            var lingeringChill = Line("UpgradeLine_LingeringChill", "Lingering Chill", hero, AbilityRole.Ultimate,
                ("Frost Zone duration +1.5s.", DurationAdd("Upg_Lingering_T1", "Lingering Chill I", 1.5f)),
                ("Frost Zone duration +3s.", DurationAdd("Upg_Lingering_T2", "Lingering Chill II", 3.0f)),
                ("Frost Zone duration +4.5s.", DurationAdd("Upg_Lingering_T3", "Lingering Chill III", 4.5f)));

            // Line 7 — Zone Pulse (Frost Zone area-tied AoE pulse). FULLY functional (Pass 2).
            var zonePulse = Line("UpgradeLine_ZonePulse", "Zone Pulse", hero, AbilityRole.Ultimate,
                ("Pulse every 1.5s for 10% of basic damage.", ZonePulse("Upg_ZonePulse_T1", "Zone Pulse I", 1.5f, 0.10f)),
                ("Pulse every 1.2s for 18% of basic damage.", ZonePulse("Upg_ZonePulse_T2", "Zone Pulse II", 1.2f, 0.18f)),
                ("Pulse every 1s for 28% of basic damage.", ZonePulse("Upg_ZonePulse_T3", "Zone Pulse III", 1.0f, 0.28f)));

            // Line 8 — Absolute Zero (Task 33: Frost Zone duration extends on a death inside). Cap = cast
            // duration + 3s, so uptime can't run away. FULLY functional.
            var absoluteZero = Line("UpgradeLine_AbsoluteZero", "Absolute Zero", hero, AbilityRole.Ultimate,
                ("+1s Frost Zone duration per death inside (capped).", AbsoluteZero("Upg_AbsoluteZero_T1", "Absolute Zero I", 1.0f, 3f)),
                ("+1.5s Frost Zone duration per death inside (capped).", AbsoluteZero("Upg_AbsoluteZero_T2", "Absolute Zero II", 1.5f, 3f)),
                ("+2s Frost Zone duration per death inside (capped).", AbsoluteZero("Upg_AbsoluteZero_T3", "Absolute Zero III", 2.0f, 3f)));

            // === APEX TALENTS ===

            // Remorseless Winter — freeze nearest enemy (Frozen Ground T3 + Deepening Frost T3).
            var rwAbility = ApexAbility("Ability_RemorselessWinter", "Remorseless Winter",
                AbilityTargetingType.SingleTarget, cooldown: 8f, range: 100f, damage: 0f, radius: 0f,
                baselineFreezeDuration: 1.5f, damageFractionOfBasic: 0f);
            var remorselessWinter = Apex("ApexTalent_RemorselessWinter", "Remorseless Winter", hero,
                new List<UpgradeLineDefinitionSO> { frozenGround, deepeningFrost }, rwAbility);

            // Permafrost Eruption — AoE burst = 50% basic damage (Wider Burst T3 + Zone Pulse T3).
            var peAbility = ApexAbility("Ability_PermafrostEruption", "Permafrost Eruption",
                AbilityTargetingType.AreaOfEffect, cooldown: 10f, range: 4f, damage: 0f, radius: 4f,
                baselineFreezeDuration: 0f, damageFractionOfBasic: 0.5f);
            var permafrostEruption = Apex("ApexTalent_PermafrostEruption", "Permafrost Eruption", hero,
                new List<UpgradeLineDefinitionSO> { widerBurst, zonePulse }, peAbility);

            // === Wire the hero (replaces Task 29's lines/apexes). ===
            WireHero(hero,
                new List<UpgradeLineDefinitionSO>
                {
                    frozenGround, widerBurst, shattering, hardFreeze,
                    deepeningFrost, lingeringChill, zonePulse, absoluteZero
                },
                new List<ApexTalentDefinitionSO> { remorselessWinter, permafrostEruption });

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Task31/33] Frost Warden content authored: all 8 lines + 2 apexes wired and FUNCTIONAL; " +
                      "ultimate DoT removed; basic base max-targets = 3; Frost Zone is a full-width band (depth 6m) " +
                      "in front of the wall; Absolute Zero extends duration on death-inside (capped).");
        }

        // --- Effect builders -----------------------------------------------------------------------

        private static UpgradeDefinitionSO StatMods(string file, string name,
            params (UpgradeModifierTarget target, UpgradeModifierOp op, float value)[] mods)
        {
            var a = AbilityAssetUtil.LoadOrCreate<UpgradeDefinitionSO>($"{UpgradeFolder}/{file}.asset");
            var so = new SerializedObject(a);
            so.FindProperty("_upgradeName").stringValue = name;
            var list = so.FindProperty("_statModifiers");
            list.arraySize = mods.Length;
            for (int i = 0; i < mods.Length; i++)
            {
                var el = list.GetArrayElementAtIndex(i);
                el.FindPropertyRelative("_target").enumValueIndex = (int)mods[i].target;
                el.FindPropertyRelative("_op").enumValueIndex = (int)mods[i].op;
                el.FindPropertyRelative("_value").floatValue = mods[i].value;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            return a;
        }

        private static UpgradeDefinitionSO SlowSet(string file, string name, float slow) =>
            StatMods(file, name, (UpgradeModifierTarget.UltimateSlowMagnitude, UpgradeModifierOp.Set, slow));

        private static UpgradeDefinitionSO DurationAdd(string file, string name, float seconds) =>
            StatMods(file, name, (UpgradeModifierTarget.UltimateDuration, UpgradeModifierOp.Add, seconds));

        private static UpgradeDefinitionSO Shatter(string file, string name, float bonus)
        {
            var a = AbilityAssetUtil.LoadOrCreate<UpgradeDefinitionSO>($"{UpgradeFolder}/{file}.asset");
            var so = new SerializedObject(a);
            so.FindProperty("_upgradeName").stringValue = name;
            so.FindProperty("_statModifiers").arraySize = 0;
            so.FindProperty("_bonusDamageVsImpaired").floatValue = bonus;
            so.ApplyModifiedPropertiesWithoutUndo();
            return a;
        }

        private static UpgradeDefinitionSO Freeze(string file, string name, float chance, float duration)
        {
            var a = AbilityAssetUtil.LoadOrCreate<UpgradeDefinitionSO>($"{UpgradeFolder}/{file}.asset");
            var so = new SerializedObject(a);
            so.FindProperty("_upgradeName").stringValue = name;
            so.FindProperty("_statModifiers").arraySize = 0;
            so.FindProperty("_hardFreezeChance").floatValue = chance;
            so.FindProperty("_hardFreezeDuration").floatValue = duration;
            so.ApplyModifiedPropertiesWithoutUndo();
            return a;
        }

        private static UpgradeDefinitionSO FrozenGround(string file, string name, float radius, float duration, float slow)
        {
            var a = AbilityAssetUtil.LoadOrCreate<UpgradeDefinitionSO>($"{UpgradeFolder}/{file}.asset");
            var so = new SerializedObject(a);
            so.FindProperty("_upgradeName").stringValue = name;
            so.FindProperty("_statModifiers").arraySize = 0;
            so.FindProperty("_frozenGroundRadius").floatValue = radius;
            so.FindProperty("_frozenGroundDuration").floatValue = duration;
            so.FindProperty("_frozenGroundSlow").floatValue = slow;
            so.ApplyModifiedPropertiesWithoutUndo();
            return a;
        }

        private static UpgradeDefinitionSO ZonePulse(string file, string name, float interval, float fraction)
        {
            var a = AbilityAssetUtil.LoadOrCreate<UpgradeDefinitionSO>($"{UpgradeFolder}/{file}.asset");
            var so = new SerializedObject(a);
            so.FindProperty("_upgradeName").stringValue = name;
            so.FindProperty("_statModifiers").arraySize = 0;
            so.FindProperty("_zonePulseInterval").floatValue = interval;
            so.FindProperty("_zonePulseBasicFraction").floatValue = fraction;
            so.ApplyModifiedPropertiesWithoutUndo();
            return a;
        }

        private static UpgradeDefinitionSO AbsoluteZero(string file, string name, float extendPerDeath, float capBonus)
        {
            var a = AbilityAssetUtil.LoadOrCreate<UpgradeDefinitionSO>($"{UpgradeFolder}/{file}.asset");
            var so = new SerializedObject(a);
            so.FindProperty("_upgradeName").stringValue = name;
            so.FindProperty("_statModifiers").arraySize = 0;
            so.FindProperty("_zoneDurationExtendPerDeath").floatValue = extendPerDeath;
            so.FindProperty("_zoneDurationExtendCapBonus").floatValue = capBonus;
            so.ApplyModifiedPropertiesWithoutUndo();
            return a;
        }

        // --- Line / apex builders ------------------------------------------------------------------

        private static UpgradeLineDefinitionSO Line(string file, string name, HeroDefinitionSO hero,
            AbilityRole skill, params (string desc, UpgradeDefinitionSO effect)[] tiers)
        {
            var line = AbilityAssetUtil.LoadOrCreate<UpgradeLineDefinitionSO>($"{LineFolder}/{file}.asset");
            var so = new SerializedObject(line);
            so.FindProperty("_hero").objectReferenceValue = hero;
            so.FindProperty("_skill").enumValueIndex = (int)skill;
            so.FindProperty("_lineName").stringValue = name;
            var list = so.FindProperty("_tiers");
            list.arraySize = tiers.Length;
            for (int i = 0; i < tiers.Length; i++)
            {
                var el = list.GetArrayElementAtIndex(i);
                el.FindPropertyRelative("_description").stringValue = tiers[i].desc;
                el.FindPropertyRelative("_effect").objectReferenceValue = tiers[i].effect;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            return line;
        }

        private static AbilityDefinitionSO ApexAbility(string file, string name, AbilityTargetingType targeting,
            float cooldown, float range, float damage, float radius, float baselineFreezeDuration,
            float damageFractionOfBasic)
        {
            var a = AbilityAssetUtil.LoadOrCreate<AbilityDefinitionSO>($"{AbilityFolder}/{file}.asset");
            var so = new SerializedObject(a);
            so.FindProperty("_abilityName").stringValue = name;
            so.FindProperty("_baseDamage").floatValue = damage;
            so.FindProperty("_baseCooldown").floatValue = cooldown;
            so.FindProperty("_range").floatValue = range;
            so.FindProperty("_aoeRadius").floatValue = radius;
            so.FindProperty("_targetingType").enumValueIndex = (int)targeting;
            so.FindProperty("_damageScalesWithBasicFraction").floatValue = damageFractionOfBasic;
            if (baselineFreezeDuration > 0f)
            {
                so.FindProperty("_appliesBaselineStatus").boolValue = true;
                so.FindProperty("_baselineStatusType").enumValueIndex = (int)StatusEffectType.Freeze;
                so.FindProperty("_baselineStatusMagnitude").floatValue = 0f;
                so.FindProperty("_baselineStatusDuration").floatValue = baselineFreezeDuration;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            return a;
        }

        private static ApexTalentDefinitionSO Apex(string file, string name, HeroDefinitionSO hero,
            List<UpgradeLineDefinitionSO> requiredLines, AbilityDefinitionSO ability)
        {
            var apex = AbilityAssetUtil.LoadOrCreate<ApexTalentDefinitionSO>($"{LineFolder}/{file}.asset");
            var so = new SerializedObject(apex);
            so.FindProperty("_hero").objectReferenceValue = hero;
            so.FindProperty("_apexName").stringValue = name;
            so.FindProperty("_ability").objectReferenceValue = ability;
            var list = so.FindProperty("_requiredLines");
            list.arraySize = requiredLines.Count;
            for (int i = 0; i < requiredLines.Count; i++)
                list.GetArrayElementAtIndex(i).objectReferenceValue = requiredLines[i];
            so.ApplyModifiedPropertiesWithoutUndo();
            return apex;
        }

        private static void WireHero(HeroDefinitionSO hero,
            List<UpgradeLineDefinitionSO> lines, List<ApexTalentDefinitionSO> apexes)
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
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // --- Tiny SO field helpers -----------------------------------------------------------------

        private static void SetInt(Object asset, string prop, int value)
        {
            var so = new SerializedObject(asset);
            so.FindProperty(prop).intValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetFloat(Object asset, string prop, float value)
        {
            var so = new SerializedObject(asset);
            so.FindProperty(prop).floatValue = value;
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
