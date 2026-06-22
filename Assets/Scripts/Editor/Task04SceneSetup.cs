#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using Wavekeep.Data;

namespace Wavekeep.EditorTools
{
    /// <summary>
    /// Authors the Task 04 ability + upgrade test assets: a basic (single-target, auto-fire) and an
    /// ultimate (AoE) <see cref="AbilityDefinitionSO"/> — each with <see cref="TagInteractionRule"/>s —
    /// plus three tagged test <see cref="UpgradeDefinitionSO"/>s.
    ///
    /// NOTE (Task 05): the placeholder Hero capsule + driver this script used to spawn has moved to
    /// the hero-select flow (<c>Wavekeep/Setup Task 05 (Heroes)</c>), which replaced the throwaway
    /// <c>HeroAbilityController</c> with <c>HeroRuntime</c>. This menu now only authors assets and is
    /// safe to run before Task 05. Editor-only; not in the runtime build.
    /// </summary>
    public static class Task04SceneSetup
    {
        public const string BasicPath = "Assets/Data/Abilities/BasicAutoBolt.asset";
        public const string UltimatePath = "Assets/Data/Abilities/UltimateNova.asset";
        public const string PrecisionPath = "Assets/Data/Upgrades/Upgrade_Precision.asset";
        public const string FirePath = "Assets/Data/Upgrades/Upgrade_FireInfusion.asset";
        public const string MaelstromPath = "Assets/Data/Upgrades/Upgrade_Maelstrom.asset";

        [MenuItem("Wavekeep/Setup Task 04 (Abilities)")]
        public static void SetupScene()
        {
            CreateBasicAbility();
            CreateUltimateAbility();
            CreateUpgrade(PrecisionPath, "Precision", UpgradeTag.SingleTarget, UpgradeEffectType.FlatDamageBonus, 5f);
            CreateUpgrade(FirePath, "Fire Infusion", UpgradeTag.Elemental_Fire, UpgradeEffectType.FlatDamageBonus, 5f);
            CreateUpgrade(MaelstromPath, "Maelstrom", UpgradeTag.AoE, UpgradeEffectType.AoeRadiusBonus, 3f);

            AssetDatabase.SaveAssets();
            Debug.Log("[Task04SceneSetup] Authored ability + upgrade assets. Run 'Wavekeep/Setup Task 05 (Heroes)' to wire heroes + the select screen.");
        }

        private static AbilityDefinitionSO CreateBasicAbility()
        {
            var ability = AbilityAssetUtil.LoadOrCreate<AbilityDefinitionSO>(BasicPath);
            var so = new SerializedObject(ability);
            so.FindProperty("_abilityName").stringValue = "Auto Bolt";
            so.FindProperty("_baseDamage").floatValue = 8f;
            so.FindProperty("_baseCooldown").floatValue = 0.5f;
            so.FindProperty("_range").floatValue = 16f;
            so.FindProperty("_targetingType").enumValueIndex = (int)AbilityTargetingType.SingleTarget;

            var rules = so.FindProperty("_tagInteractionRules");
            rules.arraySize = 2;
            AbilityAssetUtil.SetRule(rules.GetArrayElementAtIndex(0), UpgradeTag.SingleTarget, AbilityModifierType.DamageMultiplier, 1.5f);
            AbilityAssetUtil.SetRule(rules.GetArrayElementAtIndex(1), UpgradeTag.Elemental_Fire, AbilityModifierType.DamageFlatBonus, 10f);
            so.ApplyModifiedPropertiesWithoutUndo();
            return ability;
        }

        private static AbilityDefinitionSO CreateUltimateAbility()
        {
            var ability = AbilityAssetUtil.LoadOrCreate<AbilityDefinitionSO>(UltimatePath);
            var so = new SerializedObject(ability);
            so.FindProperty("_abilityName").stringValue = "Nova";
            so.FindProperty("_baseDamage").floatValue = 25f;
            so.FindProperty("_baseCooldown").floatValue = 4f;
            // Task 08 Part A fix: AoE radius enlarged to reach wall-edge enemies (was 12, too small for
            // the ~12.8u distance from the set-back caster to the wall corners).
            so.FindProperty("_range").floatValue = 16f;
            so.FindProperty("_targetingType").enumValueIndex = (int)AbilityTargetingType.AreaOfEffect;

            var rules = so.FindProperty("_tagInteractionRules");
            rules.arraySize = 1;
            AbilityAssetUtil.SetRule(rules.GetArrayElementAtIndex(0), UpgradeTag.AoE, AbilityModifierType.DamageMultiplier, 2.0f);
            so.ApplyModifiedPropertiesWithoutUndo();
            return ability;
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
    }
}
#endif
