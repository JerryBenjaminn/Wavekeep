using System.Collections.Generic;
using Wavekeep.Data;

namespace Wavekeep.Abilities
{
    /// <summary>
    /// The per-run set of <see cref="UpgradeDefinitionSO"/> the player currently holds (CLAUDE.md §3.8).
    /// A non-static plain C# class owned by <c>GameSession</c>. In Task 04 it is filled via debug
    /// keys; Task 07's level-up card picker will add to it. Hero abilities query it to resolve their
    /// <c>TagInteractionRule</c>s against the tags of held upgrades.
    /// </summary>
    public sealed class UpgradeInventory
    {
        private readonly List<UpgradeDefinitionSO> _upgrades = new List<UpgradeDefinitionSO>();

        public IReadOnlyList<UpgradeDefinitionSO> Upgrades => _upgrades;

        public void Add(UpgradeDefinitionSO upgrade)
        {
            if (upgrade != null) _upgrades.Add(upgrade);
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

        public void Clear() => _upgrades.Clear();
    }
}
