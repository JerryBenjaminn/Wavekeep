using System.Collections.Generic;
using Wavekeep.Data;

namespace Wavekeep.Gear
{
    /// <summary>
    /// One hero's equipped items, one per <see cref="GearSlot"/> (Task 12). A non-static plain C# class.
    /// Pure slot state — inventory moves and persistence are orchestrated by <c>GearManager</c>; this
    /// class only knows which item sits in each slot and the aggregated stat modifiers that result.
    ///
    /// <see cref="Equip"/>/<see cref="Unequip"/> RETURN the displaced item (never destroy it), so the
    /// caller can return it to inventory. The aggregated <see cref="AggregatedModifiers"/> are rebuilt
    /// on any change and fed to <c>AbilityRuntime</c>'s existing modifier pipeline (not a parallel path).
    /// </summary>
    public sealed class HeroLoadout
    {
        private static readonly int SlotCount = System.Enum.GetValues(typeof(GearSlot)).Length;

        private readonly LootItemSO[] _slots;
        private readonly List<StatModifier> _aggregated = new List<StatModifier>();

        public HeroLoadout()
        {
            _slots = new LootItemSO[SlotCount];
        }

        /// <summary>All stat modifiers from every equipped item, recomputed on change. Empty when bare.</summary>
        public IReadOnlyList<StatModifier> AggregatedModifiers => _aggregated;

        public LootItemSO GetEquipped(GearSlot slot) => _slots[(int)slot];

        /// <summary>Place <paramref name="item"/> in its own slot. Returns whatever was there before
        /// (or null) so the caller can return it to inventory — never destroyed.</summary>
        public LootItemSO Equip(LootItemSO item)
        {
            if (item == null) return null;

            int index = (int)item.Slot;
            var previous = _slots[index];
            _slots[index] = item;
            Rebuild();
            return previous;
        }

        /// <summary>Clear a slot, returning the item that was there (or null).</summary>
        public LootItemSO Unequip(GearSlot slot)
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
            for (int i = 0; i < _slots.Length; i++)
            {
                var item = _slots[i];
                if (item == null) continue;
                var mods = item.StatModifiers;
                for (int m = 0; m < mods.Count; m++) _aggregated.Add(mods[m]);
            }
        }
    }
}
