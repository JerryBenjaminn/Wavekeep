#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Wavekeep.Data;

namespace Wavekeep.EditorTools
{
    /// <summary>
    /// Task 35 — authors Bolt Striker's content: configures its Basic (Lightning Bolt) + Ultimate
    /// (single-target nuke) abilities, the 8 upgrade lines (4 basic / 4 ultimate), and the 2 cross-skill
    /// apex talents (Thunderstorm, Lethal Surge), then wires the hero. Single-target DPS only — NO AoE
    /// (beyond Chain Lightning's specified single/double jump) and NO damage-over-time.
    ///
    /// Every effect resolves through the existing UpgradeInventory/AbilityRuntime pipeline via the generic
    /// Task 35 data fields (UpgradeDefinitionSO + AbilityDefinitionSO) — no hero-identity branching. Both
    /// abilities are tagged Magical (Task 34). Placeholder base damage/cooldown values; tier numbers are
    /// exactly as specified in the task.
    ///
    /// Run "Wavekeep/Setup Task 35 (Bolt Striker Content)" after the Task 05 setup. Idempotent. Editor-only.
    /// </summary>
    public static class Task35BoltStrikerContentSetup
    {
        private const string UpgradeFolder = "Assets/Data/Upgrades";
        private const string LineFolder = "Assets/Data/UpgradeLines";
        private const string AbilityFolder = "Assets/Data/Abilities";
        private const string HeroPath = "Assets/Data/Heroes/Hero_BoltStriker.asset";

        [MenuItem("Wavekeep/Setup Task 35 (Bolt Striker Content)")]
        public static void SetupScene()
        {
            var hero = AssetDatabase.LoadAssetAtPath<HeroDefinitionSO>(HeroPath);
            if (hero == null)
            {
                Debug.LogError("[Task35] Hero_BoltStriker not found. Run 'Wavekeep/Setup Task 05 (Heroes)' first.");
                return;
            }

            EnsureFolder("Assets/Data", "Upgrades");
            EnsureFolder("Assets/Data", "UpgradeLines");

            // --- Abilities: a single-target Magical basic + ultimate. ---
            var basic = Basic("Ability_LightningBolt", "Lightning Bolt", damage: 10f, cooldown: 0.5f, range: 28f);
            var ultimate = Ultimate("Ability_BoltNuke", "Voltaic Nuke", damage: 50f, cooldown: 6f, range: 28f);

            // === BASIC LINES (Lightning Bolt) ===

            // Line 1 — Chain Lightning (the only secondary-target reach; single-hit-per-jump, not AoE).
            var chainLightning = Line("UpgradeLine_ChainLightning", "Chain Lightning", hero, AbilityRole.Basic,
                ("Bolt jumps to 1 enemy for 40% of the hit.", Chain("Upg_ChainLightning_T1", "Chain Lightning I", 1, 0.40f)),
                ("Bolt jumps to 1 enemy for 55% of the hit.", Chain("Upg_ChainLightning_T2", "Chain Lightning II", 1, 0.55f)),
                ("Bolt jumps to 2 enemies for 55% each.", Chain("Upg_ChainLightning_T3", "Chain Lightning III", 2, 0.55f)));

            // Line 2 — Static Charge (consecutive hits on one target stack a bonus; switching resets).
            var staticCharge = Line("UpgradeLine_StaticCharge", "Static Charge", hero, AbilityRole.Basic,
                ("+5% damage per stack, max 3 (+15%).", Static("Upg_StaticCharge_T1", "Static Charge I", 0.05f, 3)),
                ("+7% damage per stack, max 4 (+28%).", Static("Upg_StaticCharge_T2", "Static Charge II", 0.07f, 4)),
                ("+10% damage per stack, max 5 (+50%).", Static("Upg_StaticCharge_T3", "Static Charge III", 0.10f, 5)));

            // Line 3 — Overcharge (passive crit chance + an independent bonus-spike chance).
            var overcharge = Line("UpgradeLine_Overcharge", "Overcharge", hero, AbilityRole.Basic,
                ("+5% crit; 5% chance for a +50% spike.", Overcharge("Upg_Overcharge_T1", "Overcharge I", 0.05f, 0.05f, 0.50f)),
                ("+10% crit; 10% chance for a +75% spike.", Overcharge("Upg_Overcharge_T2", "Overcharge II", 0.10f, 0.10f, 0.75f)),
                ("+15% crit; 15% chance for a +100% spike.", Overcharge("Upg_Overcharge_T3", "Overcharge III", 0.15f, 0.15f, 1.00f)));

            // Line 4 — Piercing Bolt (Task 34 temporary Armor reduction; affects all Physical sources).
            var piercingBolt = Line("UpgradeLine_PiercingBolt", "Piercing Bolt", hero, AbilityRole.Basic,
                ("-10 effective Armor for 2s on hit.", Pierce("Upg_PiercingBolt_T1", "Piercing Bolt I", 10f, 2f)),
                ("-15 effective Armor for 3s on hit.", Pierce("Upg_PiercingBolt_T2", "Piercing Bolt II", 15f, 3f)),
                ("-20 effective Armor for 4s on hit.", Pierce("Upg_PiercingBolt_T3", "Piercing Bolt III", 20f, 4f)));

            // === ULTIMATE LINES (single-target nuke) ===

            // Line 5 — Multi-Strike (same target hit multiple times per cast, each at 60%).
            var multiStrike = Line("UpgradeLine_MultiStrike", "Multi-Strike", hero, AbilityRole.Ultimate,
                ("Ultimate hits 2× at 60% each.", Multi("Upg_MultiStrike_T1", "Multi-Strike I", 2, 0.60f)),
                ("Ultimate hits 3× at 60% each.", Multi("Upg_MultiStrike_T2", "Multi-Strike II", 3, 0.60f)),
                ("Ultimate hits 4× at 60% each.", Multi("Upg_MultiStrike_T3", "Multi-Strike III", 4, 0.60f)));

            // Line 6 — Execute (bonus damage vs low-HP targets).
            var execute = Line("UpgradeLine_Execute", "Execute", hero, AbilityRole.Ultimate,
                ("+20% damage vs targets under 25% HP.", Execute("Upg_Execute_T1", "Execute I", 0.25f, 0.20f)),
                ("+35% damage vs targets under 30% HP.", Execute("Upg_Execute_T2", "Execute II", 0.30f, 0.35f)),
                ("+50% damage vs targets under 35% HP.", Execute("Upg_Execute_T3", "Execute III", 0.35f, 0.50f)));

            // Line 7 — Charged Finisher (flat increase to the ultimate's base damage).
            var chargedFinisher = Line("UpgradeLine_ChargedFinisher", "Charged Finisher", hero, AbilityRole.Ultimate,
                ("+15% ultimate base damage.", UltDamage("Upg_ChargedFinisher_T1", "Charged Finisher I", 1.15f)),
                ("+30% ultimate base damage.", UltDamage("Upg_ChargedFinisher_T2", "Charged Finisher II", 1.30f)),
                ("+50% ultimate base damage.", UltDamage("Upg_ChargedFinisher_T3", "Charged Finisher III", 1.50f)));

            // Line 8 — Overload (generic incoming-damage vulnerability; NOT Armor reduction, NOT DoT).
            var overload = Line("UpgradeLine_Overload", "Overload", hero, AbilityRole.Ultimate,
                ("Target takes +10% damage for 2s.", Overload("Upg_Overload_T1", "Overload I", 0.10f, 2f)),
                ("Target takes +15% damage for 3s.", Overload("Upg_Overload_T2", "Overload II", 0.15f, 3f)),
                ("Target takes +20% damage for 4s.", Overload("Upg_Overload_T3", "Overload III", 0.20f, 4f)));

            // === APEX TALENTS (cross-skill) ===

            // Thunderstorm — Chain Lightning T3 + Multi-Strike T3. 2 hits @50% of basic + 1 jump @40% of that.
            var thunderstormAbility = ApexAbility("Ability_Thunderstorm", "Thunderstorm",
                cooldown: 9f, damageFractionOfBasic: 0.5f, hitCount: 2,
                chainJumps: 1, chainFraction: 0.4f,
                consumesStaticCharge: false, staticChargePerStack: 0f, lowHpExecuteBonus: 0f);
            var thunderstorm = Apex("ApexTalent_Thunderstorm", "Thunderstorm", hero,
                new List<UpgradeLineDefinitionSO> { chainLightning, multiStrike }, thunderstormAbility);

            // Lethal Surge — Static Charge T3 + Execute T3. 60% of basic, +10%/static stack, +30% if executable.
            var lethalSurgeAbility = ApexAbility("Ability_LethalSurge", "Lethal Surge",
                cooldown: 11f, damageFractionOfBasic: 0.6f, hitCount: 1,
                chainJumps: 0, chainFraction: 0f,
                consumesStaticCharge: true, staticChargePerStack: 0.10f, lowHpExecuteBonus: 0.30f);
            var lethalSurge = Apex("ApexTalent_LethalSurge", "Lethal Surge", hero,
                new List<UpgradeLineDefinitionSO> { staticCharge, execute }, lethalSurgeAbility);

            // === Wire the hero (basic/ultimate + 8 lines + 2 apexes). ===
            WireHero(hero, basic, ultimate,
                new List<UpgradeLineDefinitionSO>
                {
                    chainLightning, staticCharge, overcharge, piercingBolt,
                    multiStrike, execute, chargedFinisher, overload
                },
                new List<ApexTalentDefinitionSO> { thunderstorm, lethalSurge });

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Task35] Bolt Striker authored: Lightning Bolt + Voltaic Nuke (both Magical), 8 lines " +
                      "(Chain Lightning/Static Charge/Overcharge/Piercing Bolt; Multi-Strike/Execute/Charged " +
                      "Finisher/Overload), 2 apexes (Thunderstorm = CL3+MS3, Lethal Surge = SC3+Exec3). " +
                      "Level the required pairs to T3 in a run → each apex auto-fires on its cooldown.");
        }

        // --- Ability builders ----------------------------------------------------------------------

        private static AbilityDefinitionSO Basic(string file, string name, float damage, float cooldown, float range)
        {
            var a = AbilityAssetUtil.LoadOrCreate<AbilityDefinitionSO>($"{AbilityFolder}/{file}.asset");
            var so = new SerializedObject(a);
            so.FindProperty("_abilityName").stringValue = name;
            so.FindProperty("_baseDamage").floatValue = damage;
            so.FindProperty("_baseCooldown").floatValue = cooldown;
            so.FindProperty("_range").floatValue = range;
            so.FindProperty("_targetingType").enumValueIndex = (int)AbilityTargetingType.SingleTarget;
            so.FindProperty("_damageType").enumValueIndex = (int)DamageType.Magical;
            so.FindProperty("_hitCount").intValue = 1;
            so.FindProperty("_hitDamageFraction").floatValue = 1f;
            so.FindProperty("_chainRange").floatValue = 8f; // Chain Lightning jump search radius
            so.ApplyModifiedPropertiesWithoutUndo();
            return a;
        }

        private static AbilityDefinitionSO Ultimate(string file, string name, float damage, float cooldown, float range)
        {
            var a = AbilityAssetUtil.LoadOrCreate<AbilityDefinitionSO>($"{AbilityFolder}/{file}.asset");
            var so = new SerializedObject(a);
            so.FindProperty("_abilityName").stringValue = name;
            so.FindProperty("_baseDamage").floatValue = damage;
            so.FindProperty("_baseCooldown").floatValue = cooldown;
            so.FindProperty("_range").floatValue = range;
            so.FindProperty("_targetingType").enumValueIndex = (int)AbilityTargetingType.SingleTarget;
            so.FindProperty("_damageType").enumValueIndex = (int)DamageType.Magical;
            so.FindProperty("_hitCount").intValue = 1;
            so.FindProperty("_hitDamageFraction").floatValue = 1f;
            so.ApplyModifiedPropertiesWithoutUndo();
            return a;
        }

        private static AbilityDefinitionSO ApexAbility(string file, string name, float cooldown,
            float damageFractionOfBasic, int hitCount, int chainJumps, float chainFraction,
            bool consumesStaticCharge, float staticChargePerStack, float lowHpExecuteBonus)
        {
            var a = AbilityAssetUtil.LoadOrCreate<AbilityDefinitionSO>($"{AbilityFolder}/{file}.asset");
            var so = new SerializedObject(a);
            so.FindProperty("_abilityName").stringValue = name;
            so.FindProperty("_baseDamage").floatValue = 0f;       // scales off basic instead
            so.FindProperty("_baseCooldown").floatValue = cooldown;
            so.FindProperty("_range").floatValue = 100f;          // reaches across the arena to auto-fire reliably
            so.FindProperty("_targetingType").enumValueIndex = (int)AbilityTargetingType.SingleTarget;
            so.FindProperty("_damageType").enumValueIndex = (int)DamageType.Magical;
            so.FindProperty("_damageScalesWithBasicFraction").floatValue = damageFractionOfBasic;
            so.FindProperty("_hitCount").intValue = Mathf.Max(1, hitCount);
            so.FindProperty("_hitDamageFraction").floatValue = 1f;
            so.FindProperty("_chainJumps").intValue = chainJumps;
            so.FindProperty("_chainDamageFraction").floatValue = chainFraction;
            so.FindProperty("_chainRange").floatValue = 8f;
            so.FindProperty("_consumesStaticCharge").boolValue = consumesStaticCharge;
            so.FindProperty("_staticChargeConsumeBonusPerStack").floatValue = staticChargePerStack;
            so.FindProperty("_lowHpExecuteBonus").floatValue = lowHpExecuteBonus;
            so.ApplyModifiedPropertiesWithoutUndo();
            return a;
        }

        // --- Upgrade-effect builders ----------------------------------------------------------------

        private static UpgradeDefinitionSO Chain(string file, string name, int jumps, float fraction)
        {
            var so = NewUpgrade(file, name, out var a);
            so.FindProperty("_chainLightningJumps").intValue = jumps;
            so.FindProperty("_chainLightningFraction").floatValue = fraction;
            so.ApplyModifiedPropertiesWithoutUndo();
            return a;
        }

        private static UpgradeDefinitionSO Static(string file, string name, float perStack, int maxStacks)
        {
            var so = NewUpgrade(file, name, out var a);
            so.FindProperty("_staticChargePerStack").floatValue = perStack;
            so.FindProperty("_staticChargeMaxStacks").intValue = maxStacks;
            so.ApplyModifiedPropertiesWithoutUndo();
            return a;
        }

        private static UpgradeDefinitionSO Overcharge(string file, string name, float critChance, float spikeChance, float spikeBonus)
        {
            var so = NewUpgrade(file, name, out var a);
            so.FindProperty("_critChanceBonus").floatValue = critChance;
            so.FindProperty("_overchargeSpikeChance").floatValue = spikeChance;
            so.FindProperty("_overchargeSpikeBonus").floatValue = spikeBonus;
            so.ApplyModifiedPropertiesWithoutUndo();
            return a;
        }

        private static UpgradeDefinitionSO Pierce(string file, string name, float amount, float duration)
        {
            var so = NewUpgrade(file, name, out var a);
            so.FindProperty("_armorReductionAmount").floatValue = amount;
            so.FindProperty("_armorReductionDuration").floatValue = duration;
            so.ApplyModifiedPropertiesWithoutUndo();
            return a;
        }

        private static UpgradeDefinitionSO Multi(string file, string name, int hits, float fraction)
        {
            var so = NewUpgrade(file, name, out var a);
            so.FindProperty("_multiStrikeHits").intValue = hits;
            so.FindProperty("_multiStrikeFraction").floatValue = fraction;
            so.ApplyModifiedPropertiesWithoutUndo();
            return a;
        }

        private static UpgradeDefinitionSO Execute(string file, string name, float threshold, float bonus)
        {
            var so = NewUpgrade(file, name, out var a);
            so.FindProperty("_executeThreshold").floatValue = threshold;
            so.FindProperty("_executeBonus").floatValue = bonus;
            so.ApplyModifiedPropertiesWithoutUndo();
            return a;
        }

        private static UpgradeDefinitionSO Overload(string file, string name, float bonus, float duration)
        {
            var so = NewUpgrade(file, name, out var a);
            so.FindProperty("_vulnerabilityBonus").floatValue = bonus;
            so.FindProperty("_vulnerabilityDuration").floatValue = duration;
            so.ApplyModifiedPropertiesWithoutUndo();
            return a;
        }

        // Charged Finisher: a generic UltimateDamage Multiply modifier (resolved only for the Ultimate role).
        private static UpgradeDefinitionSO UltDamage(string file, string name, float multiplier)
        {
            var so = NewUpgrade(file, name, out var a);
            var list = so.FindProperty("_statModifiers");
            list.arraySize = 1;
            var el = list.GetArrayElementAtIndex(0);
            el.FindPropertyRelative("_target").enumValueIndex = (int)UpgradeModifierTarget.UltimateDamage;
            el.FindPropertyRelative("_op").enumValueIndex = (int)UpgradeModifierOp.Multiply;
            el.FindPropertyRelative("_value").floatValue = multiplier;
            so.ApplyModifiedPropertiesWithoutUndo();
            return a;
        }

        private static SerializedObject NewUpgrade(string file, string name, out UpgradeDefinitionSO asset)
        {
            asset = AbilityAssetUtil.LoadOrCreate<UpgradeDefinitionSO>($"{UpgradeFolder}/{file}.asset");
            var so = new SerializedObject(asset);
            so.FindProperty("_upgradeName").stringValue = name;
            so.FindProperty("_statModifiers").arraySize = 0; // cleared unless the builder sets some
            return so;
        }

        // --- Line / apex / hero wiring --------------------------------------------------------------

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

        private static void WireHero(HeroDefinitionSO hero, AbilityDefinitionSO basic, AbilityDefinitionSO ultimate,
            List<UpgradeLineDefinitionSO> lines, List<ApexTalentDefinitionSO> apexes)
        {
            var so = new SerializedObject(hero);
            so.FindProperty("_basicAbility").objectReferenceValue = basic;
            so.FindProperty("_ultimateAbility").objectReferenceValue = ultimate;
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

        private static void EnsureFolder(string parent, string child)
        {
            if (!AssetDatabase.IsValidFolder($"{parent}/{child}"))
                AssetDatabase.CreateFolder(parent, child);
        }
    }
}
#endif
