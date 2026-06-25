using System.Collections.Generic;
using Wavekeep.Data;

namespace Wavekeep.Abilities
{
    /// <summary>
    /// The per-run set of <see cref="UpgradeDefinitionSO"/> the player currently holds (CLAUDE.md §3.8).
    /// A non-static plain C# class owned by <c>GameSession</c>. In Task 04 it is filled via debug
    /// keys; Task 07's level-up card picker adds to it. Hero abilities query it to resolve their
    /// <c>TagInteractionRule</c>s against the tags of held upgrades.
    ///
    /// Task 19 adds two responsibilities, both kept generic (driven by data, never a hero identity):
    /// per-run BRANCH-LOCK state (picking a Mage/Defender upgrade permanently locks the opposite branch
    /// out of the draw pool for the run) and resolution of held upgrades' parametric
    /// <see cref="UpgradeStatModifier"/>s / behaviour flags so abilities can read their effective values.
    /// Because <c>GameSessionBootstrap</c> constructs a fresh instance per scene load, this per-run
    /// state resets automatically on a new run (Task 08); <see cref="Clear"/> also wipes it.
    /// </summary>
    public sealed class UpgradeInventory
    {
        private readonly List<UpgradeDefinitionSO> _upgrades = new List<UpgradeDefinitionSO>();
        private readonly HashSet<UpgradeBranch> _lockedBranches = new HashSet<UpgradeBranch>();

        public IReadOnlyList<UpgradeDefinitionSO> Upgrades => _upgrades;

        public void Add(UpgradeDefinitionSO upgrade)
        {
            if (upgrade == null) return;
            _upgrades.Add(upgrade);

            // Task 19: committing to a branch permanently locks its opposite for the rest of the run.
            var opposite = OppositeBranch(upgrade.Branch);
            if (opposite != UpgradeBranch.Neutral) _lockedBranches.Add(opposite);
        }

        /// <summary>Task 31: remove a single held instance of an upgrade (used by the upgrade-line REPLACE
        /// semantics — a line swaps its previous tier's effect for the new tier's). Branch locks, once set,
        /// are intentionally NOT released. Returns false if it wasn't held.</summary>
        public bool Remove(UpgradeDefinitionSO upgrade)
        {
            return upgrade != null && _upgrades.Remove(upgrade);
        }

        /// <summary>True if any held upgrade carries <paramref name="tag"/>.</summary>
        public bool HasTag(UpgradeTag tag)
        {
            for (int i = 0; i < _upgrades.Count; i++)
            {
                if (_upgrades[i] != null && _upgrades[i].HasTag(tag)) return true;
            }
            return false;
        }

        /// <summary>Task 19: true if <paramref name="branch"/> has been locked out for this run by a
        /// prior pick from the opposing branch. <see cref="UpgradeBranch.Neutral"/> is never locked.</summary>
        public bool IsBranchLocked(UpgradeBranch branch)
        {
            return branch != UpgradeBranch.Neutral && _lockedBranches.Contains(branch);
        }

        /// <summary>Task 19: apply every held upgrade's stat modifiers for <paramref name="target"/> to
        /// <paramref name="baseValue"/>, in held order (Multiply scales, Add sums). Generic — switches on
        /// the modifier's data, never on a specific upgrade.</summary>
        public float ResolveModifier(UpgradeModifierTarget target, float baseValue)
        {
            float value = baseValue;
            for (int i = 0; i < _upgrades.Count; i++)
            {
                var mods = _upgrades[i]?.StatModifiers;
                if (mods == null) continue;
                for (int m = 0; m < mods.Count; m++)
                {
                    var mod = mods[m];
                    if (mod == null || mod.Target != target) continue;
                    switch (mod.Op)
                    {
                        case UpgradeModifierOp.Multiply: value *= mod.Value; break;
                        case UpgradeModifierOp.Set: value = mod.Value; break; // Task 31: last Set wins (held order)
                        default: value += mod.Value; break;                   // Add
                    }
                }
            }
            return value;
        }

        /// <summary>Task 19: the first held Chain-Frost upgrade's spread parameters, if any.</summary>
        public bool TryGetChainSpread(out int stacks, out float radius)
        {
            for (int i = 0; i < _upgrades.Count; i++)
            {
                var u = _upgrades[i];
                if (u != null && u.FrostChainSpread)
                {
                    stacks = u.FrostChainStacks;
                    radius = u.FrostChainRadius;
                    return true;
                }
            }
            stacks = 0;
            radius = 0f;
            return false;
        }

        /// <summary>Task 19: the first held Ultimate-Freeze upgrade's parameters, if any.</summary>
        public bool TryGetUltimateFreeze(out int stackThreshold, out float duration)
        {
            for (int i = 0; i < _upgrades.Count; i++)
            {
                var u = _upgrades[i];
                if (u != null && u.UltimateFreezeOnStacks)
                {
                    stackThreshold = u.UltimateFreezeStackThreshold;
                    duration = u.UltimateFreezeDuration;
                    return true;
                }
            }
            stackThreshold = 0;
            duration = 0f;
            return false;
        }

        /// <summary>Task 31 (Shattering Impact): the strongest held bonus-damage-vs-impaired fraction (0 if
        /// none). Max rather than sum, since upgrade-line REPLACE semantics holds one tier per line.</summary>
        public float BonusDamageVsImpaired()
        {
            float best = 0f;
            for (int i = 0; i < _upgrades.Count; i++)
            {
                var u = _upgrades[i];
                if (u != null && u.BonusDamageVsImpaired > best) best = u.BonusDamageVsImpaired;
            }
            return best;
        }

        /// <summary>Task 31 (Hard Freeze): the strongest held hard-freeze chance + its stun duration, if any.</summary>
        public bool TryGetHardFreeze(out float chance, out float duration)
        {
            chance = 0f;
            duration = 0f;
            for (int i = 0; i < _upgrades.Count; i++)
            {
                var u = _upgrades[i];
                if (u == null || u.HardFreezeChance <= 0f) continue;
                if (u.HardFreezeChance > chance)
                {
                    chance = u.HardFreezeChance;
                    duration = u.HardFreezeDuration;
                }
            }
            return chance > 0f;
        }

        /// <summary>Task 31 Pass 2 (Frozen Ground): the held basic ice-patch params, if any.</summary>
        public bool TryGetFrozenGround(out float radius, out float duration, out float slow)
        {
            for (int i = 0; i < _upgrades.Count; i++)
            {
                var u = _upgrades[i];
                if (u != null && u.FrozenGroundRadius > 0f)
                {
                    radius = u.FrozenGroundRadius;
                    duration = u.FrozenGroundDuration;
                    slow = u.FrozenGroundSlow;
                    return true;
                }
            }
            radius = duration = slow = 0f;
            return false;
        }

        /// <summary>Task 31 Pass 2 (Zone Pulse): the held Frost Zone pulse params, if any.</summary>
        public bool TryGetZonePulse(out float interval, out float basicFraction)
        {
            for (int i = 0; i < _upgrades.Count; i++)
            {
                var u = _upgrades[i];
                if (u != null && u.ZonePulseInterval > 0f && u.ZonePulseBasicFraction > 0f)
                {
                    interval = u.ZonePulseInterval;
                    basicFraction = u.ZonePulseBasicFraction;
                    return true;
                }
            }
            interval = basicFraction = 0f;
            return false;
        }

        /// <summary>Task 33 (Absolute Zero): the held Frost Zone duration-extension params, if any —
        /// seconds added per death inside, plus the cap headroom over the zone's cast duration.</summary>
        public bool TryGetZoneDurationExtend(out float perDeath, out float capBonus)
        {
            for (int i = 0; i < _upgrades.Count; i++)
            {
                var u = _upgrades[i];
                if (u != null && u.ZoneDurationExtendPerDeath > 0f)
                {
                    perDeath = u.ZoneDurationExtendPerDeath;
                    capBonus = u.ZoneDurationExtendCapBonus;
                    return true;
                }
            }
            perDeath = capBonus = 0f;
            return false;
        }

        // === Task 35: Bolt Striker line getters. Same generic pattern as the Frost Warden getters above —
        // scan held upgrades for the relevant data, switching on data never on a specific upgrade/hero. ===

        /// <summary>Task 35 (Chain Lightning): the held jump count + per-jump damage fraction, if any.</summary>
        public bool TryGetChainLightning(out int jumps, out float fraction)
        {
            for (int i = 0; i < _upgrades.Count; i++)
            {
                var u = _upgrades[i];
                if (u != null && u.ChainLightningJumps > 0 && u.ChainLightningFraction > 0f)
                {
                    jumps = u.ChainLightningJumps;
                    fraction = u.ChainLightningFraction;
                    return true;
                }
            }
            jumps = 0;
            fraction = 0f;
            return false;
        }

        /// <summary>Task 35 (Static Charge): the held per-stack bonus + max stacks, if any.</summary>
        public bool TryGetStaticCharge(out float perStack, out int maxStacks)
        {
            for (int i = 0; i < _upgrades.Count; i++)
            {
                var u = _upgrades[i];
                if (u != null && u.StaticChargePerStack > 0f && u.StaticChargeMaxStacks > 0)
                {
                    perStack = u.StaticChargePerStack;
                    maxStacks = u.StaticChargeMaxStacks;
                    return true;
                }
            }
            perStack = 0f;
            maxStacks = 0;
            return false;
        }

        /// <summary>Task 35 (Overcharge): the strongest held flat crit-chance bonus [0..1] (0 if none). Fed
        /// into the existing Task 23 crit roll, never a parallel crit path. Max (not sum) since line REPLACE
        /// semantics holds one tier per line.</summary>
        public float CritChanceBonus()
        {
            float best = 0f;
            for (int i = 0; i < _upgrades.Count; i++)
            {
                var u = _upgrades[i];
                if (u != null && u.CritChanceBonus > best) best = u.CritChanceBonus;
            }
            return best;
        }

        /// <summary>Task 35 (Overcharge): the held bonus-spike chance + bonus fraction, if any.</summary>
        public bool TryGetOverchargeSpike(out float chance, out float bonus)
        {
            for (int i = 0; i < _upgrades.Count; i++)
            {
                var u = _upgrades[i];
                if (u != null && u.OverchargeSpikeChance > 0f && u.OverchargeSpikeBonus > 0f)
                {
                    chance = u.OverchargeSpikeChance;
                    bonus = u.OverchargeSpikeBonus;
                    return true;
                }
            }
            chance = 0f;
            bonus = 0f;
            return false;
        }

        /// <summary>Task 35 (Piercing Bolt): the held temporary Armor-reduction params, if any.</summary>
        public bool TryGetPiercingBolt(out float amount, out float duration)
        {
            for (int i = 0; i < _upgrades.Count; i++)
            {
                var u = _upgrades[i];
                if (u != null && u.ArmorReductionAmount > 0f && u.ArmorReductionDuration > 0f)
                {
                    amount = u.ArmorReductionAmount;
                    duration = u.ArmorReductionDuration;
                    return true;
                }
            }
            amount = 0f;
            duration = 0f;
            return false;
        }

        /// <summary>Task 35 (Multi-Strike): the held hit count + per-hit fraction, if any.</summary>
        public bool TryGetMultiStrike(out int hits, out float fraction)
        {
            for (int i = 0; i < _upgrades.Count; i++)
            {
                var u = _upgrades[i];
                if (u != null && u.MultiStrikeHits > 0 && u.MultiStrikeFraction > 0f)
                {
                    hits = u.MultiStrikeHits;
                    fraction = u.MultiStrikeFraction;
                    return true;
                }
            }
            hits = 0;
            fraction = 0f;
            return false;
        }

        /// <summary>Task 35 (Execute): the held low-HP threshold + bonus fraction, if any.</summary>
        public bool TryGetExecute(out float threshold, out float bonus)
        {
            for (int i = 0; i < _upgrades.Count; i++)
            {
                var u = _upgrades[i];
                if (u != null && u.ExecuteThreshold > 0f && u.ExecuteBonus > 0f)
                {
                    threshold = u.ExecuteThreshold;
                    bonus = u.ExecuteBonus;
                    return true;
                }
            }
            threshold = 0f;
            bonus = 0f;
            return false;
        }

        /// <summary>Task 35 (Overload): the held generic-vulnerability bonus + duration, if any.</summary>
        public bool TryGetOverload(out float bonus, out float duration)
        {
            for (int i = 0; i < _upgrades.Count; i++)
            {
                var u = _upgrades[i];
                if (u != null && u.VulnerabilityBonus > 0f && u.VulnerabilityDuration > 0f)
                {
                    bonus = u.VulnerabilityBonus;
                    duration = u.VulnerabilityDuration;
                    return true;
                }
            }
            bonus = 0f;
            duration = 0f;
            return false;
        }

        public void Clear()
        {
            _upgrades.Clear();
            _lockedBranches.Clear();
        }

        private static UpgradeBranch OppositeBranch(UpgradeBranch branch)
        {
            switch (branch)
            {
                case UpgradeBranch.Mage: return UpgradeBranch.Defender;
                case UpgradeBranch.Defender: return UpgradeBranch.Mage;
                default: return UpgradeBranch.Neutral;
            }
        }
    }
}
