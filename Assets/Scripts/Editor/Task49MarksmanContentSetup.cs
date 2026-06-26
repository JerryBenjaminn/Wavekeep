#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Wavekeep.Data;

namespace Wavekeep.EditorTools
{
    /// <summary>
    /// Task 49 — authors the new hero <b>Marksman</b> (a Physical-damage pierce DPS): its Basic (a fast
    /// single-shot that becomes a piercing line via upgrades) + Ultimate (Minigun — a sustained full-width
    /// shot-burst channel), the 8 upgrade lines (4 basic / 4 ultimate), and the 2 cross-skill apex talents
    /// (Bullet Storm, Executioner's Volley), then creates + wires the hero asset.
    ///
    /// Same structural pattern as Task 31/35/48: every effect resolves through the existing
    /// UpgradeInventory/AbilityRuntime pipeline via generic Task 49 data fields — no hero-identity branching.
    /// Both core abilities are tagged <b>Physical</b> (Task 34), the first hero to do so, so Armor mitigation
    /// finally matters.
    ///
    /// Documented placeholder base values (left unspecified by the task): Basic damage 8 / cd 0.5 / range 28.
    /// Minigun per-shot damage 6 / base shot-interval 0.15s / cooldown 18s (base duration 5s as specified).
    /// Bullet Storm: 1.5s / 12 shots → interval 0.125s, 80° arc. Pierce corridor half-width 1m.
    ///
    /// Run "Wavekeep/Setup Task 49 (Marksman Content)" after the Task 05 setup (it reuses the shared hero
    /// prefab). Idempotent. Re-run Task 14 (Hub) so the hero joins the team-select roster; this script also
    /// refreshes the Talent Catalog directly. Editor-only.
    /// </summary>
    public static class Task49MarksmanContentSetup
    {
        private const string UpgradeFolder = "Assets/Data/Upgrades";
        private const string LineFolder = "Assets/Data/UpgradeLines";
        private const string AbilityFolder = "Assets/Data/Abilities";
        private const string HeroPath = "Assets/Data/Heroes/Hero_Marksman.asset";
        private const string HeroPrefabPath = "Assets/Prefabs/Heroes/PlaceholderHero.prefab";

        [MenuItem("Wavekeep/Setup Task 49 (Marksman Content)")]
        public static void SetupScene()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(HeroPrefabPath);
            if (prefab == null)
            {
                Debug.LogError("[Task49] Shared hero prefab not found. Run 'Wavekeep/Setup Task 05 (Heroes)' first.");
                return;
            }

            EnsureFolder("Assets/Data", "Upgrades");
            EnsureFolder("Assets/Data", "UpgradeLines");
            EnsureFolder("Assets/Data", "Heroes");

            // --- Core abilities: a plain Physical piercing-shot Basic + a channelled Minigun Ultimate. ---
            var basic = BasicShot("Ability_MarksmanShot", "Marksman Shot", damage: 8f, cooldown: 0.5f, range: 28f);
            var ultimate = Minigun("Ability_Minigun", "Minigun",
                damagePerShot: 6f, cooldown: 18f, range: 100f, duration: 5f, shotInterval: 0.15f);

            var hero = CreateHero("Marksman", new Color(0.55f, 0.5f, 0.32f), 95f, prefab, basic, ultimate);

            // === BASIC LINES ===

            // Line 1 — Piercing Rounds (shots pierce a line; T3 unlimited).
            var piercingRounds = Line("UpgradeLine_PiercingRounds", "Piercing Rounds", hero, AbilityRole.Basic,
                ("Shots pierce up to 2 enemies, 100% each.", PiercingRounds("Upg_PiercingRounds_T1", "Piercing Rounds I", 2)),
                ("Shots pierce up to 4 enemies, 100% each.", PiercingRounds("Upg_PiercingRounds_T2", "Piercing Rounds II", 4)),
                ("Shots pierce unlimited enemies, 100% each.", PiercingRounds("Upg_PiercingRounds_T3", "Piercing Rounds III", 0)));

            // Line 2 — Rapid Fire (+fire rate = shorter Basic cooldown). BasicCooldown Multiply (replace).
            var rapidFire = Line("UpgradeLine_RapidFire", "Rapid Fire", hero, AbilityRole.Basic,
                ("+20% fire rate.", RapidFire("Upg_RapidFire_T1", "Rapid Fire I", 0.20f)),
                ("+35% fire rate.", RapidFire("Upg_RapidFire_T2", "Rapid Fire II", 0.35f)),
                ("+50% fire rate.", RapidFire("Upg_RapidFire_T3", "Rapid Fire III", 0.50f)));

            // Line 3 — Multishot (several shots per trigger in a narrow spread).
            var multishot = Line("UpgradeLine_Multishot", "Multishot", hero, AbilityRole.Basic,
                ("2 shots, 15° spread.", Multishot("Upg_Multishot_T1", "Multishot I", 2, 15f)),
                ("3 shots, 15° spread.", Multishot("Upg_Multishot_T2", "Multishot II", 3, 15f)),
                ("4 shots, 20° spread.", Multishot("Upg_Multishot_T3", "Multishot III", 4, 20f)));

            // Line 4 — Armor Shredder (STACKING armor reduction per hit; distinct from Piercing Bolt).
            var armorShredder = Line("UpgradeLine_ArmorShredder", "Armor Shredder", hero, AbilityRole.Basic,
                ("-3 Armor/stack, max 5, 3s.", ArmorShredder("Upg_ArmorShredder_T1", "Armor Shredder I", 3f, 5, 3f)),
                ("-5 Armor/stack, max 6, 3s.", ArmorShredder("Upg_ArmorShredder_T2", "Armor Shredder II", 5f, 6, 3f)),
                ("-7 Armor/stack, max 8, 3s.", ArmorShredder("Upg_ArmorShredder_T3", "Armor Shredder III", 7f, 8, 3f)));

            // === ULTIMATE LINES (Minigun) ===

            // Line 5 — Sustained Barrage (+Minigun duration). UltimateDuration Add.
            var sustainedBarrage = Line("UpgradeLine_SustainedBarrage", "Sustained Barrage", hero, AbilityRole.Ultimate,
                ("Minigun duration +1.5s.", SustainedBarrage("Upg_SustainedBarrage_T1", "Sustained Barrage I", 1.5f)),
                ("Minigun duration +3s.", SustainedBarrage("Upg_SustainedBarrage_T2", "Sustained Barrage II", 3f)),
                ("Minigun duration +4.5s.", SustainedBarrage("Upg_SustainedBarrage_T3", "Sustained Barrage III", 4.5f)));

            // Line 6 — Faster Spin-Up (+Minigun internal fire rate).
            var fasterSpinUp = Line("UpgradeLine_FasterSpinUp", "Faster Spin-Up", hero, AbilityRole.Ultimate,
                ("+25% Minigun fire rate.", FasterSpinUp("Upg_FasterSpinUp_T1", "Faster Spin-Up I", 0.25f)),
                ("+45% Minigun fire rate.", FasterSpinUp("Upg_FasterSpinUp_T2", "Faster Spin-Up II", 0.45f)),
                ("+65% Minigun fire rate.", FasterSpinUp("Upg_FasterSpinUp_T3", "Faster Spin-Up III", 0.65f)));

            // Line 7 — Heavy Rounds (+Minigun per-shot damage). UltimateDamage Multiply.
            var heavyRounds = Line("UpgradeLine_HeavyRounds", "Heavy Rounds", hero, AbilityRole.Ultimate,
                ("+20% Minigun damage per shot.", HeavyRounds("Upg_HeavyRounds_T1", "Heavy Rounds I", 1.20f)),
                ("+35% Minigun damage per shot.", HeavyRounds("Upg_HeavyRounds_T2", "Heavy Rounds II", 1.35f)),
                ("+50% Minigun damage per shot.", HeavyRounds("Upg_HeavyRounds_T3", "Heavy Rounds III", 1.50f)));

            // Line 8 — Full Pierce (+damage to pierced targets beyond the first; stacks with Piercing Rounds).
            var fullPierce = Line("UpgradeLine_FullPierce", "Full Pierce", hero, AbilityRole.Ultimate,
                ("+15% damage to pierced targets beyond the first.", FullPierce("Upg_FullPierce_T1", "Full Pierce I", 0.15f)),
                ("+30% damage to pierced targets beyond the first.", FullPierce("Upg_FullPierce_T2", "Full Pierce II", 0.30f)),
                ("+50% damage to pierced targets beyond the first.", FullPierce("Upg_FullPierce_T3", "Full Pierce III", 0.50f)));

            // === APEX TALENTS (cross-skill) ===

            // Bullet Storm — Multishot T3 + Faster Spin-Up T3. A dense 1.5s arc burst: 12 shots, 50% of Basic, full pierce.
            var bulletStormAbility = BulletStormAbility("Ability_BulletStorm", "Bullet Storm",
                cooldown: 9f, duration: 1.5f, shotInterval: 1.5f / 12f, arcAngle: 80f, basicFraction: 0.50f, range: 100f);
            var bulletStorm = Apex("ApexTalent_BulletStorm", "Bullet Storm", hero,
                new List<UpgradeLineDefinitionSO> { multishot, fasterSpinUp }, bulletStormAbility);

            // Executioner's Volley — Armor Shredder T3 + Heavy Rounds T3. One heavy shot at the most-shredded
            // target: 80% of Basic, +15% per current Armor-Shredder stack on it.
            var volleyAbility = ExecutionersVolleyAbility("Ability_ExecutionersVolley", "Executioner's Volley",
                cooldown: 10f, basicFraction: 0.80f, shredStackBonus: 0.15f, range: 100f);
            var executionersVolley = Apex("ApexTalent_ExecutionersVolley", "Executioner's Volley", hero,
                new List<UpgradeLineDefinitionSO> { armorShredder, heavyRounds }, volleyAbility);

            // === Wire the hero (basic/ultimate + 8 lines + 2 apexes). ===
            WireHero(hero, basic, ultimate,
                new List<UpgradeLineDefinitionSO>
                {
                    piercingRounds, rapidFire, multishot, armorShredder,
                    sustainedBarrage, fasterSpinUp, heavyRounds, fullPierce
                },
                new List<ApexTalentDefinitionSO> { bulletStorm, executionersVolley });

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // Keep the Codex's master talent list current so the new apexes are discoverable.
            Task43CodexSetup.EnsureCatalog();

            Debug.Log("[Task49] Marksman authored: Marksman Shot + Minigun (both Physical), 8 lines (Piercing Rounds/" +
                      "Rapid Fire/Multishot/Armor Shredder; Sustained Barrage/Faster Spin-Up/Heavy Rounds/Full Pierce), " +
                      "2 apexes (Bullet Storm = Multishot3+FasterSpinUp3, Executioner's Volley = ArmorShredder3+HeavyRounds3). " +
                      "Re-run Task 14 (Hub) so it joins the team-select roster.");
        }

        // --- Core ability builders -----------------------------------------------------------------

        private static AbilityDefinitionSO BasicShot(string file, string name, float damage, float cooldown, float range)
        {
            var a = AbilityAssetUtil.LoadOrCreate<AbilityDefinitionSO>($"{AbilityFolder}/{file}.asset");
            var so = new SerializedObject(a);
            so.FindProperty("_abilityName").stringValue = name;
            so.FindProperty("_baseDamage").floatValue = damage;
            so.FindProperty("_baseCooldown").floatValue = cooldown;
            so.FindProperty("_range").floatValue = range;
            so.FindProperty("_targetingType").enumValueIndex = (int)AbilityTargetingType.PiercingLine;
            so.FindProperty("_damageType").enumValueIndex = (int)DamageType.Physical;
            so.FindProperty("_hitCount").intValue = 1;
            so.FindProperty("_hitDamageFraction").floatValue = 1f;
            so.FindProperty("_vfxStyle").enumValueIndex = (int)AbilityVfxStyle.Kinetic; // Task 52: tracers
            so.ApplyModifiedPropertiesWithoutUndo();
            return a;
        }

        private static AbilityDefinitionSO Minigun(string file, string name, float damagePerShot, float cooldown,
            float range, float duration, float shotInterval)
        {
            var a = AbilityAssetUtil.LoadOrCreate<AbilityDefinitionSO>($"{AbilityFolder}/{file}.asset");
            var so = new SerializedObject(a);
            so.FindProperty("_abilityName").stringValue = name;
            so.FindProperty("_baseDamage").floatValue = damagePerShot; // per-shot (Heavy Rounds scales it)
            so.FindProperty("_baseCooldown").floatValue = cooldown;
            so.FindProperty("_range").floatValue = range;
            so.FindProperty("_targetingType").enumValueIndex = (int)AbilityTargetingType.PiercingLine;
            so.FindProperty("_damageType").enumValueIndex = (int)DamageType.Physical;
            so.FindProperty("_appliesShotBurst").boolValue = true;
            so.FindProperty("_channelDuration").floatValue = duration;
            so.FindProperty("_channelShotInterval").floatValue = shotInterval;
            so.FindProperty("_channelSpreadAngle").floatValue = 0f; // 0 = sweep across the width (random aim)
            so.FindProperty("_vfxStyle").enumValueIndex = (int)AbilityVfxStyle.Kinetic; // Task 52: minigun tracers
            so.ApplyModifiedPropertiesWithoutUndo();
            return a;
        }

        private static AbilityDefinitionSO BulletStormAbility(string file, string name, float cooldown,
            float duration, float shotInterval, float arcAngle, float basicFraction, float range)
        {
            var a = AbilityAssetUtil.LoadOrCreate<AbilityDefinitionSO>($"{AbilityFolder}/{file}.asset");
            var so = new SerializedObject(a);
            so.FindProperty("_abilityName").stringValue = name;
            so.FindProperty("_baseDamage").floatValue = 0f; // scales off basic instead
            so.FindProperty("_baseCooldown").floatValue = cooldown;
            so.FindProperty("_range").floatValue = range;
            so.FindProperty("_targetingType").enumValueIndex = (int)AbilityTargetingType.PiercingLine;
            so.FindProperty("_damageType").enumValueIndex = (int)DamageType.Physical;
            so.FindProperty("_damageScalesWithBasicFraction").floatValue = basicFraction;
            so.FindProperty("_appliesShotBurst").boolValue = true;
            so.FindProperty("_channelDuration").floatValue = duration;
            so.FindProperty("_channelShotInterval").floatValue = shotInterval;
            so.FindProperty("_channelSpreadAngle").floatValue = arcAngle; // >0 = fixed arc fan in front
            so.ApplyModifiedPropertiesWithoutUndo();
            return a;
        }

        private static AbilityDefinitionSO ExecutionersVolleyAbility(string file, string name, float cooldown,
            float basicFraction, float shredStackBonus, float range)
        {
            var a = AbilityAssetUtil.LoadOrCreate<AbilityDefinitionSO>($"{AbilityFolder}/{file}.asset");
            var so = new SerializedObject(a);
            so.FindProperty("_abilityName").stringValue = name;
            so.FindProperty("_baseDamage").floatValue = 0f; // scales off basic instead
            so.FindProperty("_baseCooldown").floatValue = cooldown;
            so.FindProperty("_range").floatValue = range;
            so.FindProperty("_targetingType").enumValueIndex = (int)AbilityTargetingType.SingleTarget;
            so.FindProperty("_damageType").enumValueIndex = (int)DamageType.Physical;
            so.FindProperty("_damageScalesWithBasicFraction").floatValue = basicFraction;
            so.FindProperty("_targetsHighestShred").boolValue = true;
            so.FindProperty("_shredStackDamageBonus").floatValue = shredStackBonus;
            so.ApplyModifiedPropertiesWithoutUndo();
            return a;
        }

        // --- Upgrade-effect builders ---------------------------------------------------------------

        private static UpgradeDefinitionSO PiercingRounds(string file, string name, int limit)
        {
            var so = NewUpgrade(file, name, out var a);
            so.FindProperty("_piercingRounds").boolValue = true;
            so.FindProperty("_piercingRoundsLimit").intValue = limit; // 0 = unlimited
            so.ApplyModifiedPropertiesWithoutUndo();
            return a;
        }

        private static UpgradeDefinitionSO Multishot(string file, string name, int count, float spread)
        {
            var so = NewUpgrade(file, name, out var a);
            so.FindProperty("_multishotCount").intValue = count;
            so.FindProperty("_multishotSpreadAngle").floatValue = spread;
            so.ApplyModifiedPropertiesWithoutUndo();
            return a;
        }

        private static UpgradeDefinitionSO ArmorShredder(string file, string name, float perStack, int maxStacks, float refresh)
        {
            var so = NewUpgrade(file, name, out var a);
            so.FindProperty("_armorShredPerStack").floatValue = perStack;
            so.FindProperty("_armorShredMaxStacks").intValue = maxStacks;
            so.FindProperty("_armorShredRefresh").floatValue = refresh;
            so.ApplyModifiedPropertiesWithoutUndo();
            return a;
        }

        private static UpgradeDefinitionSO FasterSpinUp(string file, string name, float bonus)
        {
            var so = NewUpgrade(file, name, out var a);
            so.FindProperty("_minigunFireRateBonus").floatValue = bonus;
            so.ApplyModifiedPropertiesWithoutUndo();
            return a;
        }

        private static UpgradeDefinitionSO FullPierce(string file, string name, float bonus)
        {
            var so = NewUpgrade(file, name, out var a);
            so.FindProperty("_fullPierceBonus").floatValue = bonus;
            so.ApplyModifiedPropertiesWithoutUndo();
            return a;
        }

        // Rapid Fire: a BasicCooldown Multiply (×1/(1+fireRate)) — faster fire rate = shorter cooldown.
        private static UpgradeDefinitionSO RapidFire(string file, string name, float fireRateBonus) =>
            StatMod(file, name, UpgradeModifierTarget.BasicCooldown, UpgradeModifierOp.Multiply, 1f / (1f + fireRateBonus));

        // Sustained Barrage: an UltimateDuration Add (resolved for the Minigun channel duration).
        private static UpgradeDefinitionSO SustainedBarrage(string file, string name, float seconds) =>
            StatMod(file, name, UpgradeModifierTarget.UltimateDuration, UpgradeModifierOp.Add, seconds);

        // Heavy Rounds: an UltimateDamage Multiply (scales the Minigun's per-shot damage).
        private static UpgradeDefinitionSO HeavyRounds(string file, string name, float multiplier) =>
            StatMod(file, name, UpgradeModifierTarget.UltimateDamage, UpgradeModifierOp.Multiply, multiplier);

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
