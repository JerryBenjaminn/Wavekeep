using System;
using System.Collections.Generic;

namespace Wavekeep.Gear
{
    /// <summary>
    /// Plain serializable DTOs for the gear save file (Task 67 — format v2). v2 stores UNIQUE per-item instances
    /// (instanceId + baseId + rarity + rolled affixes) instead of v1's stacked {itemId, count}, plus the
    /// persistent Salvage Dust total (the salvage feature lands in a later task, but the field exists now).
    /// <see cref="instances"/> holds EVERY instance the player owns (inventory + equipped); <see cref="loadouts"/>
    /// reference equipped ones by instanceId. A save below the current version is WIPED on load — there is NO
    /// v1→v2 converter, by design (see <c>GearManager.Load</c>).
    /// </summary>
    [Serializable]
    public sealed class GearSaveData
    {
        public int saveVersion;
        public int salvageDust;
        public List<GearInstanceData> instances = new List<GearInstanceData>();
        public List<LoadoutEntry> loadouts = new List<LoadoutEntry>();
    }

    [Serializable]
    public sealed class GearInstanceData
    {
        public string instanceId;
        public string baseId;
        public int rarity;                                       // (int)Rarity
        public List<RolledAffixData> affixes = new List<RolledAffixData>();
    }

    [Serializable]
    public sealed class RolledAffixData
    {
        public string affixId;
        public float value;
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
        public string slot;        // GearSlot enum name
        public string instanceId;  // the equipped instance's id (resolved against GearSaveData.instances)
    }
}
