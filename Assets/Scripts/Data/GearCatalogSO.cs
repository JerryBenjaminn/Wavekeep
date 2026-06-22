using System.Collections.Generic;
using UnityEngine;

namespace Wavekeep.Data
{
    /// <summary>
    /// Master registry of every <see cref="LootItemSO"/> in the game (Task 12). Save files store stable
    /// <see cref="LootItemSO.ItemId"/> strings (not asset references), so on load the persistence layer
    /// resolves each id back to its SO through this catalog. A single authored asset wired into the
    /// <c>GameSessionBootstrap</c>, so it works in builds without Resources/AssetDatabase lookups.
    /// Read-only at runtime.
    /// </summary>
    [CreateAssetMenu(fileName = "GearCatalog", menuName = "Wavekeep/Gear Catalog")]
    public sealed class GearCatalogSO : ScriptableObject
    {
        [SerializeField] private List<LootItemSO> _items = new List<LootItemSO>();

        public IReadOnlyList<LootItemSO> Items => _items;

        private Dictionary<string, LootItemSO> _byId;

        /// <summary>Resolve an item id to its SO, or null if unknown (e.g. a save references a removed item).</summary>
        public LootItemSO Find(string itemId)
        {
            if (string.IsNullOrEmpty(itemId)) return null;
            EnsureIndex();
            return _byId.TryGetValue(itemId, out var item) ? item : null;
        }

        private void EnsureIndex()
        {
            if (_byId != null) return;
            _byId = new Dictionary<string, LootItemSO>();
            for (int i = 0; i < _items.Count; i++)
            {
                var item = _items[i];
                if (item == null || string.IsNullOrEmpty(item.ItemId)) continue;
                _byId[item.ItemId] = item; // last wins on duplicate ids (authoring error)
            }
        }
    }
}
