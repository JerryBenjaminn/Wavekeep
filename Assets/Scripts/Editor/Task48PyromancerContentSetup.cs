#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Wavekeep.Data;

namespace Wavekeep.EditorTools
{
    /// <summary>
    /// Task 48 — authors the new hero <b>Pyromancer</b> (a DoT/AoE hero): its Basic (Fireball — a single-target
    /// Magical hit that applies a Burn DoT) + Ultimate (Firewall — a full-arena-width sustained-DoT fire band,
    /// same geometry as the Frost Zone), the 8 upgrade lines (4 basic / 4 ultimate), and the 2 cross-skill apex
    /// talents (Wildfire Apocalypse, Cataclysm), then creates + wires the hero asset.
    ///
    /// Same structural pattern as Task 31 (Frost Warden) and Task 35 (Bolt Striker): every effect resolves
    /// through the existing UpgradeInventory/AbilityRuntime pipeline (+ the GroundZone fire layer and the
    /// FireSubsystem Burn-reaction poller) via generic Task 48 data fields — no hero-identity branching. Both
    /// core abilities are tagged Magical (Task 34).
    ///
    /// Documented placeholder base values (the ones the task left unspecified): Fireball damage 9 / cd 0.6 /
    /// range 28; base Burn = 3 dmg/tick for 3s. Firewall base tick = 6 / 0.5s for 5s, band depth 6m, cd 14.
    /// Wildfire Apocalypse's burn equals Smoldering Wound T3 applied to the base burn (×1.5 dmg, +3s dur =
    /// 4.5/tick for 6s). Tier numbers are EXACTLY as specified in the task.
    ///
    /// Run "Wavekeep/Setup Task 48 (Pyromancer Content)" after the Task 05 setup (it reuses the shared hero
    /// prefab). Idempotent. Re-run Task 14 (Hub) + Task 43 (Codex) so the new hero/apexes appear in the roster
    /// and Codex (this script also refreshes the Talent Catalog directly). Editor-only.
    /// </summary>
    public static class Task48PyromancerContentSetup
    {
        private const string UpgradeFolder = "Assets/Data/Upgrades";
        private const string LineFolder = "Assets/Data/UpgradeLines";
        private const string AbilityFolder = "Assets/Data/Abilities";
        private const string HeroPath = "Assets/Data/Heroes/Hero_Pyromancer.asset";
        private const string HeroPrefabPath = "Assets/Prefabs/Heroes/PlaceholderHero.prefab";

        [MenuItem("Wavekeep/Setup Task 48 (Pyromancer Content)")]
        public static void SetupScene()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(HeroPrefabPath);
            if (prefab == null)
            {
                Debug.LogError("[Task48] Shared hero prefab not found. Run 'Wavekeep/Setup Task 05 (Heroes)' first.");
                return;
            }

            EnsureFolder("Assets/Data", "Upgrades");
            EnsureFolder("Assets/Data", "UpgradeLines");
            EnsureFolder("Assets/Data", "Heroes");

            // --- Core abilities: a single-target Magical burning Basic + a full-width fire-band Ultimate. ---
            var basic = Basic("Ability_Fireball", "Fireball",
                damage: 9f, cooldown: 0.6f, range: 28f, burnPerTick: 3f, burnDuration: 3f);
            var ultimate = Firewall("Ability_Firewall", "Firewall",
                cooldown: 14f, tickDamage: 6f, tickInterval: 0.5f, duration: 5f, bandDepth: 6f);

            // Create the hero now so the lines/apexes can reference it.
            var hero = CreateHero("Pyromancer", new Color(1f, 0.35f, 0.1f), 110f, prefab, basic, ultimate);

            // === BASIC LINES (Fireball) ===

            // Line 1 — Smoldering Wound (more Burn damage + duration).
            var smolderingWound = Line("UpgradeLine_SmolderingWound", "Smoldering Wound", hero, AbilityRole.Basic,
                ("+20% Burn damage, +1s duration.", SmolderingWound("Upg_SmolderingWound_T1", "Smoldering Wound I", 1.20f, 1f)),
                ("+35% Burn damage, +2s duration.", SmolderingWound("Upg_SmolderingWound_T2", "Smoldering Wound II", 1.35f, 2f)),
                ("+50% Burn damage, +3s duration.", SmolderingWound("Upg_SmolderingWound_T3", "Smoldering Wound III", 1.50f, 3f)));

            // Line 2 — Spreading Flame (a Burning target's death spreads Burn to nearby enemies).
            var spreadingFlame = Line("UpgradeLine_SpreadingFlame", "Spreading Flame", hero, AbilityRole.Basic,
                ("On Burn-death: spread to 1 enemy (15m).", SpreadingFlame("Upg_SpreadingFlame_T1", "Spreading Flame I", 1, 15f, 1f)),
                ("On Burn-death: spread to 1 enemy (20m), 100% potency.", SpreadingFlame("Upg_SpreadingFlame_T2", "Spreading Flame II", 1, 20f, 1f)),
                ("On Burn-death: spread to 2 enemies (20m), 100% potency.", SpreadingFlame("Upg_SpreadingFlame_T3", "Spreading Flame III", 2, 20f, 1f)));

            // Line 3 — Stacking Embers (repeated Fireball hits stack Burn damage on one target).
            var stackingEmbers = Line("UpgradeLine_StackingEmbers", "Stacking Embers", hero, AbilityRole.Basic,
                ("+10% Burn damage per stack, max 3.", StackingEmbers("Upg_StackingEmbers_T1", "Stacking Embers I", 0.10f, 3)),
                ("+15% Burn damage per stack, max 4.", StackingEmbers("Upg_StackingEmbers_T2", "Stacking Embers II", 0.15f, 4)),
                ("+20% Burn damage per stack, max 5.", StackingEmbers("Upg_StackingEmbers_T3", "Stacking Embers III", 0.20f, 5)));

            // Line 4 — Combustion (a naturally-expiring Burn may detonate in a small AoE).
            var combustion = Line("UpgradeLine_Combustion", "Combustion", hero, AbilityRole.Basic,
                ("20% on Burn-expiry: 2m blast, 30% of Basic.", Combustion("Upg_Combustion_T1", "Combustion I", 0.20f, 2f, 0.30f)),
                ("35% on Burn-expiry: 2.5m blast, 45% of Basic.", Combustion("Upg_Combustion_T2", "Combustion II", 0.35f, 2.5f, 0.45f)),
                ("50% on Burn-expiry: 3m blast, 60% of Basic.", Combustion("Upg_Combustion_T3", "Combustion III", 0.50f, 3f, 0.60f)));

            // === ULTIMATE LINES (Firewall) ===

            // Line 5 — Raging Wall (more Firewall tick damage). FirewallTickDamage Multiply (replace semantics).
            var ragingWall = Line("UpgradeLine_RagingWall", "Raging Wall", hero, AbilityRole.Ultimate,
                ("+20% Firewall tick damage.", RagingWall("Upg_RagingWall_T1", "Raging Wall I", 1.20f)),
                ("+35% Firewall tick damage.", RagingWall("Upg_RagingWall_T2", "Raging Wall II", 1.35f)),
                ("+50% Firewall tick damage.", RagingWall("Upg_RagingWall_T3", "Raging Wall III", 1.50f)));

            // Line 6 — Lingering Embers (+Firewall duration). UltimateDuration Add.
            var lingeringEmbers = Line("UpgradeLine_LingeringEmbers", "Lingering Embers", hero, AbilityRole.Ultimate,
                ("Firewall duration +1.5s.", LingeringEmbers("Upg_LingeringEmbers_T1", "Lingering Embers I", 1.5f)),
                ("Firewall duration +3s.", LingeringEmbers("Upg_LingeringEmbers_T2", "Lingering Embers II", 3f)),
                ("Firewall duration +4.5s.", LingeringEmbers("Upg_LingeringEmbers_T3", "Lingering Embers III", 4.5f)));

            // Line 7 — Wildfire Spread (enemies dying inside leave a patch that lingers AFTER Firewall ends).
            var wildfireSpread = Line("UpgradeLine_WildfireSpread", "Wildfire Spread", hero, AbilityRole.Ultimate,
                ("Death-patch: 2s after Firewall, 20% of tick.", WildfireSpread("Upg_WildfireSpread_T1", "Wildfire Spread I", 2f, 0.20f)),
                ("Death-patch: 3s after Firewall, 30% of tick.", WildfireSpread("Upg_WildfireSpread_T2", "Wildfire Spread II", 3f, 0.30f)),
                ("Death-patch: 4s after Firewall, 40% of tick.", WildfireSpread("Upg_WildfireSpread_T3", "Wildfire Spread III", 4f, 0.40f)));

            // Line 8 — Inferno Surge (Firewall periodically bursts extra instant AoE).
            var infernoSurge = Line("UpgradeLine_InfernoSurge", "Inferno Surge", hero, AbilityRole.Ultimate,
                ("Burst every 3s for 40% of Basic.", InfernoSurge("Upg_InfernoSurge_T1", "Inferno Surge I", 3f, 0.40f)),
                ("Burst every 2.5s for 55% of Basic.", InfernoSurge("Upg_InfernoSurge_T2", "Inferno Surge II", 2.5f, 0.55f)),
                ("Burst every 2s for 70% of Basic.", InfernoSurge("Upg_InfernoSurge_T3", "Inferno Surge III", 2f, 0.70f)));

            // === APEX TALENTS (cross-skill) ===

            // Wildfire Apocalypse — Spreading Flame T3 + Wildfire Spread T3. Ignites every enemy within 6m with a
            // fresh Burn at Smoldering Wound T3 potency (base burn ×1.5 dmg, +3s dur = 4.5/tick for 6s).
            var apocalypseAbility = WildfireApocalypseAbility("Ability_WildfireApocalypse", "Wildfire Apocalypse",
                cooldown: 9f, radius: 6f, burnPerTick: 4.5f, burnDuration: 6f);
            var wildfireApocalypse = Apex("ApexTalent_WildfireApocalypse", "Wildfire Apocalypse", hero,
                new List<UpgradeLineDefinitionSO> { spreadingFlame, wildfireSpread }, apocalypseAbility);

            // Cataclysm — Combustion T3 + Inferno Surge T3. A 5m AoE burst for 60% of Basic, +40% to Burning.
            var cataclysmAbility = CataclysmAbility("Ability_Cataclysm", "Cataclysm",
                cooldown: 11f, radius: 5f, basicFraction: 0.60f, burningBonus: 0.40f);
            var cataclysm = Apex("ApexTalent_Cataclysm", "Cataclysm", hero,
                new List<UpgradeLineDefinitionSO> { combustion, infernoSurge }, cataclysmAbility);

            // === Wire the hero (basic/ultimate + 8 lines + 2 apexes). ===
            WireHero(hero, basic, ultimate,
                new List<UpgradeLineDefinitionSO>
                {
                    smolderingWound, spreadingFlame, stackingEmbers, combustion,
                    ragingWall, lingeringEmbers, wildfireSpread, infernoSurge
                },
                new List<ApexTalentDefinitionSO> { wildfireApocalypse, cataclysm });

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // Keep the Codex's master talent list current so the new apexes are discoverable.
            Task43CodexSetup.EnsureCatalog();

            Debug.Log("[Task48] Pyromancer authored: Fireball + Firewall (both Magical), 8 lines (Smoldering Wound/" +
                      "Spreading Flame/Stacking Embers/Combustion; Raging Wall/Lingering Embers/Wildfire Spread/" +
                      "Inferno Surge), 2 apexes (Wildfire Apocalypse = SpreadingFlame3+WildfireSpread3, Cataclysm = " +
                      "Combustion3+InfernoSurge3). Re-run Task 14 (Hub) so it joins the team-select roster.");
        }

        // --- Core ability builders -----------------------------------------------------------------

        private static AbilityDefinitionSO Basic(string file, string name, float damage, float cooldown,
            float range, float burnPerTick, float burnDuration)
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
            so.FindProperty("_appliesBurnOnHit").boolValue = true;
            so.FindProperty("_burnDamagePerTick").floatValue = burnPerTick;
            so.FindProperty("_burnDuration").floatValue = burnDuration;
            so.FindProperty("_vfxStyle").enumValueIndex = (int)AbilityVfxStyle.Fire; // Task 51: fireball VFX
            so.ApplyModifiedPropertiesWithoutUndo();
            return a;
        }

        private static AbilityDefinitionSO Firewall(string file, string name, float cooldown,
            float tickDamage, float tickInterval, float duration, float bandDepth)
        {
            var a = AbilityAssetUtil.LoadOrCreate<AbilityDefinitionSO>($"{AbilityFolder}/{file}.asset");
            var so = new SerializedObject(a);
            so.FindProperty("_abilityName").stringValue = name;
            so.FindProperty("_baseDamage").floatValue = 0f; // damage comes from the band's DoT, not a hit
            so.FindProperty("_baseCooldown").floatValue = cooldown;
            so.FindProperty("_range").floatValue = 100f;
            so.FindProperty("_targetingType").enumValueIndex = (int)AbilityTargetingType.AreaOfEffect;
            so.FindProperty("_damageType").enumValueIndex = (int)DamageType.Magical;
            so.FindProperty("_aoeRadius").floatValue = bandDepth; // band DEPTH in front of the wall (Task 33 geometry)
            so.FindProperty("_appliesFireWall").boolValue = true;
            so.FindProperty("_fireWallTickInterval").floatValue = tickInterval;
            so.FindProperty("_fireWallTickDamage").floatValue = tickDamage;
            so.FindProperty("_fireWallDuration").floatValue = duration;
            so.FindProperty("_vfxStyle").enumValueIndex = (int)AbilityVfxStyle.Fire; // Task 51: firewall palette
            so.ApplyModifiedPropertiesWithoutUndo();
            return a;
        }

        private static AbilityDefinitionSO WildfireApocalypseAbility(string file, string name, float cooldown,
            float radius, float burnPerTick, float burnDuration)
        {
            var a = AbilityAssetUtil.LoadOrCreate<AbilityDefinitionSO>($"{AbilityFolder}/{file}.asset");
            var so = new SerializedObject(a);
            so.FindProperty("_abilityName").stringValue = name;
            so.FindProperty("_baseDamage").floatValue = 0f; // it only ignites — no direct damage
            so.FindProperty("_baseCooldown").floatValue = cooldown;
            so.FindProperty("_range").floatValue = radius;   // AreaOfEffect: Range is the caster-centred radius
            so.FindProperty("_targetingType").enumValueIndex = (int)AbilityTargetingType.AreaOfEffect;
            so.FindProperty("_damageType").enumValueIndex = (int)DamageType.Magical;
            so.FindProperty("_maxTargets").intValue = 0;     // ignite EVERY enemy in radius
            so.FindProperty("_appliesBurnOnHit").boolValue = true;
            so.FindProperty("_burnDamagePerTick").floatValue = burnPerTick;
            so.FindProperty("_burnDuration").floatValue = burnDuration;
            so.ApplyModifiedPropertiesWithoutUndo();
            return a;
        }

        private static AbilityDefinitionSO CataclysmAbility(string file, string name, float cooldown,
            float radius, float basicFraction, float burningBonus)
        {
            var a = AbilityAssetUtil.LoadOrCreate<AbilityDefinitionSO>($"{AbilityFolder}/{file}.asset");
            var so = new SerializedObject(a);
            so.FindProperty("_abilityName").stringValue = name;
            so.FindProperty("_baseDamage").floatValue = 0f;  // scales off basic instead
            so.FindProperty("_baseCooldown").floatValue = cooldown;
            so.FindProperty("_range").floatValue = radius;   // AreaOfEffect: Range is the caster-centred radius
            so.FindProperty("_targetingType").enumValueIndex = (int)AbilityTargetingType.AreaOfEffect;
            so.FindProperty("_damageType").enumValueIndex = (int)DamageType.Magical;
            so.FindProperty("_maxTargets").intValue = 0;
            so.FindProperty("_damageScalesWithBasicFraction").floatValue = basicFraction;
            so.FindProperty("_bonusDamageVsBurningFraction").floatValue = burningBonus;
            so.ApplyModifiedPropertiesWithoutUndo();
            return a;
        }

        // --- Upgrade-effect builders ---------------------------------------------------------------

        private static UpgradeDefinitionSO SmolderingWound(string file, string name, float dmgMul, float durBonus)
        {
            var so = NewUpgrade(file, name, out var a);
            so.FindProperty("_burnDamageMultiplier").floatValue = dmgMul;
            so.FindProperty("_burnDurationBonus").floatValue = durBonus;
            so.ApplyModifiedPropertiesWithoutUndo();
            return a;
        }

        private static UpgradeDefinitionSO SpreadingFlame(string file, string name, int targets, float range, float potency)
        {
            var so = NewUpgrade(file, name, out var a);
            so.FindProperty("_burnSpreadTargets").intValue = targets;
            so.FindProperty("_burnSpreadRange").floatValue = range;
            so.FindProperty("_burnSpreadPotency").floatValue = potency;
            so.ApplyModifiedPropertiesWithoutUndo();
            return a;
        }

        private static UpgradeDefinitionSO StackingEmbers(string file, string name, float perStack, int maxStacks)
        {
            var so = NewUpgrade(file, name, out var a);
            so.FindProperty("_burnStackPerStackBonus").floatValue = perStack;
            so.FindProperty("_burnMaxStacks").intValue = maxStacks;
            so.ApplyModifiedPropertiesWithoutUndo();
            return a;
        }

        private static UpgradeDefinitionSO Combustion(string file, string name, float chance, float radius, float basicFraction)
        {
            var so = NewUpgrade(file, name, out var a);
            so.FindProperty("_combustionChance").floatValue = chance;
            so.FindProperty("_combustionRadius").floatValue = radius;
            so.FindProperty("_combustionBasicFraction").floatValue = basicFraction;
            so.ApplyModifiedPropertiesWithoutUndo();
            return a;
        }

        private static UpgradeDefinitionSO WildfireSpread(string file, string name, float patchDuration, float tickFraction)
        {
            var so = NewUpgrade(file, name, out var a);
            so.FindProperty("_wildfirePatchDuration").floatValue = patchDuration;
            so.FindProperty("_wildfirePatchTickFraction").floatValue = tickFraction;
            so.ApplyModifiedPropertiesWithoutUndo();
            return a;
        }

        private static UpgradeDefinitionSO InfernoSurge(string file, string name, float interval, float basicFraction)
        {
            var so = NewUpgrade(file, name, out var a);
            so.FindProperty("_infernoSurgeInterval").floatValue = interval;
            so.FindProperty("_infernoSurgeBasicFraction").floatValue = basicFraction;
            so.ApplyModifiedPropertiesWithoutUndo();
            return a;
        }

        // Raging Wall: a generic FirewallTickDamage Multiply modifier (resolved when the Firewall is spawned).
        private static UpgradeDefinitionSO RagingWall(string file, string name, float multiplier) =>
            StatMod(file, name, UpgradeModifierTarget.FirewallTickDamage, UpgradeModifierOp.Multiply, multiplier);

        // Lingering Embers: a generic UltimateDuration Add modifier (resolved for the firewall duration).
        private static UpgradeDefinitionSO LingeringEmbers(string file, string name, float seconds) =>
            StatMod(file, name, UpgradeModifierTarget.UltimateDuration, UpgradeModifierOp.Add, seconds);

        private static UpgradeDefinitionSO StatMod(string file, string name,
            UpgradeModifierTarget target, UpgradeModifierOp op, float value)
        {
            var so = NewUpgrade(file, name, out var a);
            var list = so.FindProperty("_statModifiers");
            list.arraySize = 1;
            var el = list.GetArrayElementAtIndex(0);
            el.FindPropertyRelative("_target").enumValueIndex = (int)target;
            el.FindPropertyRelative("_op").enumValueIndex = (int)op;
            el.FindPropertyRelative("_value").floatValue = value;
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

        // --- Line / apex / hero wiring -------------------------------------------------------------

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

        private static HeroDefinitionSO CreateHero(string name, Color tint, float baseHealth,
            GameObject prefab, AbilityDefinitionSO basic, AbilityDefinitionSO ultimate)
        {
            var hero = AbilityAssetUtil.LoadOrCreate<HeroDefinitionSO>(HeroPath);
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
