using System.Collections.Generic;
using Wavekeep.Data;

namespace Wavekeep.Gear
{
    /// <summary>
    /// One hero's equipped items, one per <see cref="GearSlot"/> (Task 67: now <see cref="GearInstance"/>
    /// references, not shared SOs). A non-static plain C# class. <see cref="Equip"/>/<see cref="Unequip"/> RETURN
    /// the displaced instance (never destroy it), so the caller can return it to inventory.
    ///
    /// The aggregated <see cref="AggregatedModifiers"/> + <see cref="TotalLuckBonus"/> are rebuilt on any change
    /// and fed to <c>AbilityRuntime</c>'s existing modifier pipeline. That CONTRACT is unchanged from Task 12 —
    /// <c>HeroRuntime</c> still reads an <see cref="IReadOnlyList{StatModifier}"/> + a Luck float — so the combat
    /// side needs no changes; only the per-slot storage moved from SO to instance.
    /// </summary>
    public sealed class HeroLoadout
    {
        private static readonly int SlotCount = System.Enum.GetValues(typeof(GearSlot)).Length;

        private readonly GearInstance[] _slots;
        private readonly List<StatModifier> _aggregated = new List<StatModifier>();
        private float _totalLuckBonus;

        public HeroLoadout()
        {
            _slots = new GearInstance[SlotCount];
        }

        /// <summary>All stat modifiers from every equipped instance, recomputed on change. Empty when bare.</summary>
        public IReadOnlyList<StatModifier> AggregatedModifiers => _aggregated;

        /// <summary>Summed <see cref="GearInstance.LuckBonus"/> across all equipped slots, recomputed on change
        /// (not per-frame). Feeds the gear-derived portion of the hero's Luck via <c>HeroRuntime</c>.</summary>
        public float TotalLuckBonus => _totalLuckBonus;

        public GearInstance GetEquipped(GearSlot slot) => _slots[(int)slot];

        /// <summary>Place <paramref name="item"/> in its own slot. Returns whatever was there before (or null)
        /// so the caller can return it to inventory — never destroyed.</summary>
        public GearInstance Equip(GearInstance item)
        {
            if (item == null) return null;

            int index = (int)item.Slot;
            var previous = _slots[index];
            _slots[index] = item;
            Rebuild();
            return previous;
        }

        /// <summary>Clear a slot, returning the instance that was there (or null).</summary>
        public GearInstance Unequip(GearSlot slot)
        {
            int index = (int)slot;
            var previous = _slots[index];
            _slots[index] = null;
            Rebuild();
            return previous;
        }

        public void Clear()
        {
            for (int i = 0; i < _slots.Length; i++) _slots[i] = null;
            Rebuild();
        }

        private void Rebuild()
        {
            _aggregated.Clear();
            _totalLuckBonus = 0f;
            for (int i = 0; i < _slots.Length; i++)
            {
                var item = _slots[i];
                if (item == null) continue;
                var mods = item.StatModifiers;
                for (int m = 0; m < mods.Count; m++) _aggregated.Add(mods[m]);
                _totalLuckBonus += item.LuckBonus;
            }
        }
    }
}
