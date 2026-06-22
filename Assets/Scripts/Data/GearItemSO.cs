using UnityEngine;

namespace Wavekeep.Data
{
    /// <summary>
    /// A piece of equippable gear (Task 12): occupies one of the five armour slots (Helmet/Body/Hands/
    /// Legs/Feet — NOT Artifact; that's <see cref="ArtifactItemSO"/>). Read-only template; see
    /// <see cref="LootItemSO"/>.
    /// </summary>
    [CreateAssetMenu(fileName = "GearItem", menuName = "Wavekeep/Gear Item")]
    public sealed class GearItemSO : LootItemSO
    {
        [Header("Gear")]
        [Tooltip("Armour slot this gear occupies. Do NOT set this to Artifact — use an ArtifactItemSO instead.")]
        [SerializeField] private GearSlot _slot = GearSlot.Helmet;

        public override GearSlot Slot => _slot;
    }
}
