using System;
using System.Collections.Generic;

namespace Wavekeep.Gear
{
    /// <summary>
    /// Plain serializable DTOs for the gear save file (Task 12). Designed for Unity's
    /// <c>JsonUtility</c>: only fields, only [Serializable] types and Lists. Kept separate from the
    /// runtime <c>GearInventory</c>/<c>HeroLoadout</c> so the on-disk shape can evolve independently.
    ///
    /// <see cref="SaveVersion"/> is the FIRST field so future migrations can branch on it without
    /// breaking older saves (this is the project's first persistence format — keep it simple + versioned).
    /// Items/heroes are stored as stable string ids, resolved back to SOs via the <c>GearCatalogSO</c>.
    /// </summary>
    [Serializable]
    public sealed class GearSaveData
    {
        public int saveVersion;
        public List<OwnedItemEntry> owned = new List<OwnedItemEntry>();
        public List<LoadoutEntry> loadouts = new List<LoadoutEntry>();
    }

    [Serializable]
    public sealed class OwnedItemEntry
    {
        public string itemId;
        public int count;
    }

    [Serializable]
    public sealed class LoadoutEntry
    {
        public string heroId;
        public List<EquippedSlotEntry> slots = new List<EquippedSlotEntry>();
    }

    [Serializable]
    public sealed class EquippedSlotEntry
    {
        public string slot;   // GearSlot enum name
        public string itemId; // the equipped item's id
    }
}
