using System;
using System.Collections.Generic;

namespace Wavekeep.Gear
{
    /// <summary>
    /// Plain serializable DTOs for the gear save file (Task 67 — format v2; extended Task 71). v2 stores UNIQUE
    /// per-item instances (instanceId + baseId + rarity + rolled affixes) instead of v1's stacked {itemId, count},
    /// plus the persistent Salvage Dust total. <see cref="instances"/> holds EVERY instance the player owns
    /// (inventory + equipped + overflow); <see cref="loadouts"/> reference equipped ones by instanceId, and
    /// <see cref="overflowInstanceIds"/> marks which owned instances are pending in the at-capacity overflow buffer
    /// (Task 71) rather than in the main inventory. A save below the current version is WIPED on load — there is NO
    /// v1→v2 converter, by design (see <c>GearManager.Load</c>).
    ///
    /// Task 71 note: <see cref="overflowInstanceIds"/> was added WITHOUT bumping <see cref="saveVersion"/> — it is
    /// an additive, backward-compatible field (a pre-Task-71 v2 save simply has none → empty buffer), so existing
    /// gear is NOT wiped. A version bump would have wiped all v2 gear, which is not warranted for an additive field.
    /// </summary>
    [Serializable]
    public sealed class GearSaveData
    {
        public int saveVersion;
        public int salvageDust;
        public List<GearInstanceData> instances = new List<GearInstanceData>();
        public List<LoadoutEntry> loadouts = new List<LoadoutEntry>();
        public List<string> overflowInstanceIds = new List<string>(); // Task 71: ids of instances in the overflow buffer
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
