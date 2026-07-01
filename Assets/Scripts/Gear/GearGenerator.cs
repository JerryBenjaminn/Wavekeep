using System.Collections.Generic;
using UnityEngine;
using Wavekeep.Data;
using Wavekeep.Economy;

namespace Wavekeep.Gear
{
    /// <summary>
    /// Builds a freshly rolled <see cref="GearInstance"/> from a <see cref="LootTableSO"/> at drop-time (Task 68 —
    /// gear redesign part 2). Replaces the old "pick a finished item" flow with three independent steps:
    /// <list type="number">
    /// <item>pick a SLOT/base (weighted) from the table's slot pool;</item>
    /// <item>roll a RARITY as its own step, reusing the SAME Luck-weighted approach the loot tables used before
    /// (<see cref="LuckState.LootTierMultiplier"/>, normalised across the table's listed rarity span) — no second
    /// weighting model;</item>
    /// <item>resolve the implicit (the <see cref="GearInstance"/> reads it from base+rarity) and roll the rarity's
    /// affix count from the shared pool (distinct types, slot-eligible). Unique pulls the base's hand-authored
    /// fixed set instead of rolling.</item>
    /// </list>
    /// A non-static plain C# class owned by <c>LootService</c>. SOs are never mutated (CLAUDE.md §3.5).
    /// </summary>
    public sealed class GearGenerator
    {
        private readonly GearAffixCountConfigSO _affixConfig;
        private readonly LuckState _luck;

        // Reusable buffer so affix drawing doesn't allocate a fresh eligible list per drop.
        private readonly List<AffixDefinitionSO> _eligibleBuffer = new List<AffixDefinitionSO>();

        public GearGenerator(GearAffixCountConfigSO affixConfig, LuckState luck)
        {
            _affixConfig = affixConfig;
            _luck = luck;
        }

        /// <summary>Run the drop gate then generate an instance, or return null (no drop / unusable table).</summary>
        public GearInstance TryGenerate(LootTableSO table)
        {
            if (table == null) return null;
            if (Random.value >= table.DropChance) return null;

            var baseTemplate = PickBase(table);
            if (baseTemplate == null)
            {
                Debug.LogWarning("[GearGenerator] Loot table has no usable slot entries (run the Task 68 setup to " +
                                 "author them); drop skipped.");
                return null;
            }

            if (!TryRollRarity(table, out Rarity rarity))
            {
                Debug.LogWarning("[GearGenerator] Loot table has no usable rarity weights; drop skipped.");
                return null;
            }

            var affixes = RollAffixes(baseTemplate, rarity);
            return GearInstance.Create(baseTemplate, rarity, affixes);
        }

        /// <summary>Task 71 (Artifact Forge): generate an instance of a SPECIFIC base + rarity, with affixes rolled
        /// exactly as a drop of that rarity would (Unique → the base's fixed set). No drop gate, no slot/rarity
        /// roll — the caller chose them deterministically. Used by <c>GearManager.ForgeArtifact</c>.</summary>
        public GearInstance GenerateForBase(GearBaseSO baseTemplate, Rarity rarity)
        {
            if (baseTemplate == null) return null;
            return GearInstance.Create(baseTemplate, rarity, RollAffixes(baseTemplate, rarity));
        }

        // --- Slot pick (plain weighted; slot choice is rarity-neutral) -------------------------------

        private GearBaseSO PickBase(LootTableSO table)
        {
            var entries = table.SlotEntries;
            int total = 0;
            for (int i = 0; i < entries.Count; i++)
            {
                var e = entries[i];
                // Task 71: Artifacts are craft-only (Forge) — never a drop outcome. Hard runtime guard so even a
                // table that still lists an Artifact base (legacy data) can never roll one.
                if (e != null && e.Base != null && e.Base.Slot != GearSlot.Artifact && e.Weight > 0) total += e.Weight;
            }
            if (total <= 0) return null;

            int roll = Random.Range(0, total);
            for (int i = 0; i < entries.Count; i++)
            {
                var e = entries[i];
                if (e == null || e.Base == null || e.Base.Slot == GearSlot.Artifact || e.Weight <= 0) continue;
                roll -= e.Weight;
                if (roll < 0) return e.Base;
            }
            return null;
        }

        // --- Rarity roll (Luck-weighted — the SAME step the shop/loot used before) -------------------

        private bool TryRollRarity(LootTableSO table, out Rarity rarity)
        {
            rarity = Rarity.Common;
            var weights = table.RarityWeights;

            // Tier span of the listed rarities, so the Luck weighting normalises across only the tiers actually
            // droppable here (the boss-exclusive lock = which tiers the table lists), exactly like the old path.
            FindRarityRange(weights, out int minRarity, out int maxRarity, out bool any);
            if (!any) return false;
            int span = maxRarity - minRarity;

            float totalWeight = 0f;
            for (int i = 0; i < weights.Count; i++) totalWeight += AdjustedWeight(weights[i], minRarity, span);
            if (totalWeight <= 0f) return false;

            float roll = Random.value * totalWeight;
            for (int i = 0; i < weights.Count; i++)
            {
                float w = AdjustedWeight(weights[i], minRarity, span);
                if (w <= 0f) continue;
                roll -= w;
                if (roll < 0f) { rarity = weights[i].Rarity; return true; }
            }
            return false;
        }

        // Base rarity weight × the (weaker) loot tier multiplier for this rarity within the table's span.
        private float AdjustedWeight(LootRarityWeight entry, int minRarity, int span)
        {
            if (entry == null || entry.Weight <= 0) return 0f;
            // normTier 0 = the table's lowest listed rarity (never reduced); 1 = its highest.
            float normTier = span > 0 ? (float)((int)entry.Rarity - minRarity) / span : 0f;
            float multiplier = _luck != null ? _luck.LootTierMultiplier(normTier) : 1f;
            return entry.Weight * multiplier;
        }

        private static void FindRarityRange(IReadOnlyList<LootRarityWeight> weights,
            out int minRarity, out int maxRarity, out bool any)
        {
            minRarity = int.MaxValue;
            maxRarity = int.MinValue;
            any = false;
            for (int i = 0; i < weights.Count; i++)
            {
                var w = weights[i];
                if (w == null || w.Weight <= 0) continue;
                int r = (int)w.Rarity;
                if (r < minRarity) minRarity = r;
                if (r > maxRarity) maxRarity = r;
                any = true;
            }
        }

        // --- Affix roll -----------------------------------------------------------------------------

        private List<RolledAffix> RollAffixes(GearBaseSO baseTemplate, Rarity rarity)
        {
            // Unique never randomises (CLAUDE.md §6 / Task 68): take the base's hand-authored fixed set verbatim.
            if (rarity == Rarity.Unique) return BuildUniqueAffixes(baseTemplate);

            var affixes = new List<RolledAffix>();
            if (_affixConfig == null) return affixes;

            int count = _affixConfig.AffixCountFor(rarity);
            if (count <= 0) return affixes;

            // Eligible pool for this slot, with distinct affix types (drawn without replacement, by draw weight).
            _eligibleBuffer.Clear();
            var pool = _affixConfig.AffixPool;
            for (int i = 0; i < pool.Count; i++)
            {
                var def = pool[i];
                if (def != null && def.IsEligibleFor(baseTemplate.Slot)) _eligibleBuffer.Add(def);
            }

            for (int n = 0; n < count && _eligibleBuffer.Count > 0; n++)
            {
                var def = DrawWeighted(_eligibleBuffer);
                if (def == null) break;
                float value = Random.Range(def.MinValue, def.MaxValue);
                affixes.Add(new RolledAffix(def, value));
            }

            if (affixes.Count < count)
            {
                Debug.LogWarning($"[GearGenerator] {baseTemplate.Slot} {rarity}: pool only had {affixes.Count} " +
                                 $"eligible distinct affix(es) for a target count of {count}. Add more affixes to the pool.");
            }
            return affixes;
        }

        // Weighted pick from the eligible buffer, REMOVING the chosen affix so types stay distinct on one item.
        private static AffixDefinitionSO DrawWeighted(List<AffixDefinitionSO> eligible)
        {
            int total = 0;
            for (int i = 0; i < eligible.Count; i++) total += Mathf.Max(0, eligible[i].DrawWeight);

            int pickIndex;
            if (total <= 0)
            {
                pickIndex = Random.Range(0, eligible.Count); // all zero-weight → uniform fallback
            }
            else
            {
                int roll = Random.Range(0, total);
                pickIndex = eligible.Count - 1;
                for (int i = 0; i < eligible.Count; i++)
                {
                    roll -= Mathf.Max(0, eligible[i].DrawWeight);
                    if (roll < 0) { pickIndex = i; break; }
                }
            }

            var def = eligible[pickIndex];
            eligible.RemoveAt(pickIndex);
            return def;
        }

        private static List<RolledAffix> BuildUniqueAffixes(GearBaseSO baseTemplate)
        {
            var affixes = new List<RolledAffix>();
            var fixedSet = baseTemplate.UniqueAffixes;
            for (int i = 0; i < fixedSet.Count; i++)
            {
                var fa = fixedSet[i];
                if (fa?.Affix == null) continue;
                affixes.Add(new RolledAffix(fa.Affix, fa.Value));
            }
            return affixes;
        }
    }
}
