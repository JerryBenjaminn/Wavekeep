using UnityEngine;

namespace Wavekeep.Data
{
    /// <summary>
    /// An equippable artifact (Task 12): always occupies the single <see cref="GearSlot.Artifact"/>
    /// slot. For this task an artifact's effect is just a stat modifier list like gear (per the task
    /// scope); unique/non-stat artifact behaviours are a documented later refinement once the pattern
    /// is proven. Read-only template; see <see cref="LootItemSO"/>.
    /// </summary>
    [CreateAssetMenu(fileName = "ArtifactItem", menuName = "Wavekeep/Artifact Item")]
    public sealed class ArtifactItemSO : LootItemSO
    {
        public override GearSlot Slot => GearSlot.Artifact;
    }
}
