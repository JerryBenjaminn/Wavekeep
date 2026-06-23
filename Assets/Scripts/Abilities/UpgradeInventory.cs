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
                    value = mod.Op == UpgradeModifierOp.Multiply ? value * mod.Value : value + mod.Value;
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
