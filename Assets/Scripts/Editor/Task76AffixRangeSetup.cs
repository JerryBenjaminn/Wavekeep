#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Wavekeep.Data;

namespace Wavekeep.EditorTools
{
    /// <summary>
    /// Task 76 — gear balance: authors the per-rarity roll ranges (Common..Legendary) on every normal affix asset
    /// so higher rarity is ALWAYS strictly better (adjacent tiers never overlap), and validates that invariant.
    /// Unique is exempt (its affixes are hand-authored fixed values, not rolled from ranges).
    ///
    /// The ranges below are the tunable source of truth: designers edit them here (or on the assets) — code never
    /// hardcodes roll values. Chained into <see cref="Task67GearSetup"/> so re-authoring the affixes re-applies the
    /// ranges. Run "Wavekeep/Setup Task 76 (Affix Rarity Ranges)" any time. Editor-only.
    ///
    /// No-overlap invariant (validated): for "higher is better" stats each tier's MAX &lt; next tier's MIN; for the
    /// "lower is better" <see cref="GearStatType.CooldownMultiplier"/> each tier's MIN &gt; next tier's MAX (higher
    /// rarity = lower/better multiplier). Either way the best roll of a tier can never reach the next tier's band.
    /// </summary>
    public static class Task76AffixRangeSetup
    {
        private const string AffixesFolder = "Assets/Data/Gear/Affixes";

        // Placeholder balance (flagged for tuning): five tiers per affix, Common..Legendary. Non-overlapping.
        private static readonly Dictionary<string, (float min, float max)[]> Ranges = new Dictionary<string, (float, float)[]>
        {
            // DamageFlatBonus (higher better)
            ["affix_sharpened"] = new[] { (2f, 4f), (5f, 8f), (9f, 13f), (14f, 20f), (21f, 30f) },
            // DamageMultiplier (higher better)
            ["affix_empowered"] = new[] { (1.05f, 1.08f), (1.09f, 1.14f), (1.15f, 1.21f), (1.22f, 1.30f), (1.31f, 1.45f) },
            ["affix_emberforged"] = new[] { (1.04f, 1.07f), (1.08f, 1.12f), (1.13f, 1.18f), (1.19f, 1.26f), (1.27f, 1.38f) },
            // RangeMultiplier (higher better)
            ["affix_farsight"] = new[] { (1.04f, 1.07f), (1.08f, 1.12f), (1.13f, 1.18f), (1.19f, 1.26f), (1.27f, 1.40f) },
            // Luck (higher better)
            ["affix_lucky"] = new[] { (1f, 2f), (3f, 4f), (5f, 7f), (8f, 11f), (12f, 16f) },
            // CooldownMultiplier (LOWER better → ranges descend; higher rarity = more cooldown reduction)
            ["affix_swift"] = new[] { (0.90f, 0.95f), (0.83f, 0.89f), (0.75f, 0.82f), (0.66f, 0.74f), (0.55f, 0.65f) },
        };

        [MenuItem("Wavekeep/Setup Task 76 (Affix Rarity Ranges)")]
        public static void Setup()
        {
            int authored = AuthorRanges();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[Task76] Authored per-rarity ranges on {authored} affix asset(s). Drops/forge/reroll now roll " +
                      "within the correct per-rarity band (no cross-tier overlap). Tune values in Task76AffixRangeSetup.");
        }

        /// <summary>Author + validate ranges on every affix asset that has a table entry. Returns the count authored.
        /// Shared by the menu and the Task 67 re-author chain.</summary>
        public static int AuthorRanges()
        {
            int authored = 0;
            var guids = AssetDatabase.FindAssets("t:AffixDefinitionSO", new[] { AffixesFolder });
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var affix = AssetDatabase.LoadAssetAtPath<AffixDefinitionSO>(path);
                if (affix == null) continue;

                var so = new SerializedObject(affix);
                string id = so.FindProperty("_affixId").stringValue;
                if (string.IsNullOrEmpty(id) || !Ranges.TryGetValue(id, out var tiers))
                {
                    Debug.LogWarning($"[Task76] No range table entry for affix '{id}' ({path}); left on its legacy " +
                                     "flat fallback. Add it to Task76AffixRangeSetup.Ranges.");
                    continue;
                }

                var stat = (GearStatType)so.FindProperty("_effect").FindPropertyRelative("_stat").enumValueIndex;
                if (!Validate(id, stat, tiers)) continue; // reviewer-blocking overlap → don't write bad data

                var rangesProp = so.FindProperty("_rarityRanges");
                rangesProp.arraySize = tiers.Length;
                for (int i = 0; i < tiers.Length; i++)
                {
                    var el = rangesProp.GetArrayElementAtIndex(i);
                    el.FindPropertyRelative("min").floatValue = tiers[i].min;
                    el.FindPropertyRelative("max").floatValue = tiers[i].max;
                }
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(affix);
                authored++;
            }
            return authored;
        }

        // Enforce the no-overlap invariant in the direction that matches the stat's semantics.
        private static bool Validate(string id, GearStatType stat, (float min, float max)[] tiers)
        {
            bool lowerBetter = stat == GearStatType.CooldownMultiplier;
            for (int i = 0; i < tiers.Length; i++)
            {
                if (tiers[i].min > tiers[i].max)
                {
                    Debug.LogError($"[Task76] Affix '{id}' tier {i}: min {tiers[i].min} > max {tiers[i].max}. Skipped.");
                    return false;
                }
                if (i == 0) continue;

                bool ok = lowerBetter
                    ? tiers[i - 1].min > tiers[i].max   // descending: each tier's worst (min) beats next tier's best (max)
                    : tiers[i - 1].max < tiers[i].min;  // ascending: each tier's best (max) below next tier's worst (min)
                if (!ok)
                {
                    Debug.LogError($"[Task76] Affix '{id}' tiers {i - 1}/{i} OVERLAP ({(lowerBetter ? "lower" : "higher")}" +
                                   $"-is-better): [{tiers[i - 1].min},{tiers[i - 1].max}] vs [{tiers[i].min},{tiers[i].max}]. " +
                                   "Fix the range table — higher rarity must strictly beat lower. Skipped.");
                    return false;
                }
            }
            return true;
        }
    }
}
#endif
