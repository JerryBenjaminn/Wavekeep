using System.Collections.Generic;
using UnityEngine;

namespace Wavekeep.Data
{
    /// <summary>
    /// Shared read-only template base for ownable loot (Task 12) — <see cref="GearItemSO"/> and
    /// <see cref="ArtifactItemSO"/>. SO assets are templates only: ownership/equip/save state lives in
    /// the runtime <c>GearInventory</c>/<c>HeroLoadout</c>, never written back here (CLAUDE.md §3.5).
    ///
    /// Items are identical-by-definition for this task (no per-drop rolled stats — a documented future
    /// refinement), so a stable <see cref="ItemId"/> is enough to persist ownership and resolve the SO
    /// back via the <see cref="GearCatalogSO"/> on load. Every item exposes which <see cref="Slot"/> it
    /// occupies and its <see cref="StatModifiers"/> (applied to the hero via the existing AbilityRuntime
    /// pipeline).
    /// </summary>
    public abstract class LootItemSO : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Stable unique id used for save files + catalog lookup. Never change once shipped.")]
        [SerializeField] private string _itemId;
        [SerializeField] private string _itemName;
        [SerializeField] private Sprite _icon;
        [SerializeField] private Rarity _rarity = Rarity.Common;

        [Header("Effect")]
        [Tooltip("Stat modifiers applied while equipped (Task 12). Reuse the AbilityModifierType vocabulary.")]
        [SerializeField] private List<StatModifier> _statModifiers = new List<StatModifier>();

        public string ItemId => _itemId;
        public string ItemName => _itemName;
        public Sprite Icon => _icon;
        public Rarity Rarity => _rarity;
        public IReadOnlyList<StatModifier> StatModifiers => _statModifiers;

        /// <summary>Which equip slot this item occupies. Gear returns its authored slot; artifacts return Artifact.</summary>
        public abstract GearSlot Slot { get; }
    }
}
