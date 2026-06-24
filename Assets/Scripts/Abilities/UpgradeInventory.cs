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
