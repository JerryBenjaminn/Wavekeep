using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Wavekeep.Data;

namespace Wavekeep.Gear
{
    /// <summary>
    /// Owns the player's PERSISTENT gear state (Task 12): the <see cref="GearInventory"/> of owned items
    /// and one <see cref="HeroLoadout"/> per hero, plus disk save/load. A non-static plain C# class held
    /// by <see cref="Wavekeep.Core.GameSession"/> — no static singleton.
    ///
    /// Persistence model: disk is the source of truth. The manager LOADS from disk in its constructor
    /// and SAVES after every change (grant/equip/unequip). Because <c>GameSessionBootstrap</c> rebuilds
    /// the session on each scene load, a reconstructed manager reloads the identical state — so gear
    /// survives the Task 08 "Play Again" scene reload while per-run services reset (CLAUDE.md §6).
    ///
    /// All equip moves go through here so inventory↔loadout stays consistent and replace-not-destroy is
    /// guaranteed: equipping consumes one from inventory; a displaced item is returned to inventory.
    /// SOs are never mutated — only referenced (CLAUDE.md §3.5).
    /// </summary>
    public sealed class GearManager
    {
        private const int CurrentSaveVersion = 1;
        public const string DefaultSaveFileName = "gear_save.json";

        private readonly GearInventory _inventory = new GearInventory();
        private readonly Dictionary<string, HeroLoadout> _loadouts = new Dictionary<string, HeroLoadout>();
        private readonly GearCatalogSO _catalog;
        private readonly string _savePath;

        public GearInventory Inventory => _inventory;

        public GearManager(GearCatalogSO catalog, string savePath)
        {
            _catalog = catalog;
            _savePath = savePath;
            Load();
        }

        // --- Loadout access ---------------------------------------------------------------------

        /// <summary>Get (creating if needed) the loadout for a hero, keyed by its name.</summary>
        public HeroLoadout GetLoadout(HeroDefinitionSO hero) => GetLoadout(HeroKey(hero));

        public HeroLoadout GetLoadout(string heroId)
        {
            if (string.IsNullOrEmpty(heroId)) heroId = "";
            if (!_loadouts.TryGetValue(heroId, out var loadout))
            {
                loadout = new HeroLoadout();
                _loadouts[heroId] = loadout;
            }
            return loadout;
        }

        // --- Mutations (each persists) ----------------------------------------------------------

        /// <summary>Add an owned item to inventory (debug grant / future loot drop) and save.</summary>
        public void Grant(LootItemSO item)
        {
            if (item == null) return;
            _inventory.Add(item, 1);
            Save();
        }

        /// <summary>
        /// Equip an owned item onto a hero. Consumes one from inventory; any item already in that slot
        /// is RETURNED to inventory (never destroyed). Returns false (no change) if the player doesn't
        /// own an unequipped copy.
        /// </summary>
        public bool Equip(HeroDefinitionSO hero, LootItemSO item) => Equip(HeroKey(hero), item);

        public bool Equip(string heroId, LootItemSO item)
        {
            if (item == null) return false;
            if (!_inventory.Remove(item, 1)) return false; // must own an unequipped copy

            var previous = GetLoadout(heroId).Equip(item);
            if (previous != null) _inventory.Add(previous, 1); // replace, don't destroy
            Save();
            return true;
        }

        /// <summary>Unequip a slot, returning the item to inventory. Returns the removed item (or null).</summary>
        public LootItemSO Unequip(HeroDefinitionSO hero, GearSlot slot) => Unequip(HeroKey(hero), slot);

        public LootItemSO Unequip(string heroId, GearSlot slot)
        {
            var removed = GetLoadout(heroId).Unequip(slot);
            if (removed != null)
            {
                _inventory.Add(removed, 1);
                Save();
            }
            return removed;
        }

        private static string HeroKey(HeroDefinitionSO hero) => hero != null ? hero.HeroName : "";

        // --- Persistence ------------------------------------------------------------------------

        /// <summary>Serialize current state to disk (versioned JSON). Failures are logged, not thrown.</summary>
        public void Save()
        {
            try
            {
                var data = BuildSaveData();
                string json = JsonUtility.ToJson(data, prettyPrint: true);
                File.WriteAllText(_savePath, json);
            }
            catch (Exception e)
            {
                Debug.LogError($"[GearManager] Failed to save gear to '{_savePath}': {e.Message}");
            }
        }

        // Loads state from disk. Missing file = fresh empty state (no throw). Corrupt/unreadable file or
        // an unrecognised version = log + empty state, so a bad save can never hard-crash startup.
        private void Load()
        {
            _inventory.Clear();
            _loadouts.Clear();

            try
            {
                if (!File.Exists(_savePath))
                {
                    Debug.Log($"[GearManager] No save at '{_savePath}'; starting with empty gear state.");
                    return;
                }

                string json = File.ReadAllText(_savePath);
                var data = JsonUtility.FromJson<GearSaveData>(json);
                if (data == null)
                {
                    Debug.LogWarning("[GearManager] Save file was empty/unparseable; starting empty.");
                    return;
                }

                if (data.saveVersion != CurrentSaveVersion)
                {
                    // Only v1 exists today; a different version means a future/older format. Rather than
                    // risk mis-loading, start empty (a real migration would branch on version here).
                    Debug.LogWarning($"[GearManager] Save version {data.saveVersion} != {CurrentSaveVersion}; ignoring old save.");
                    return;
                }

                if (_catalog == null)
                {
                    Debug.LogError("[GearManager] No GearCatalog wired; cannot resolve saved items. Starting empty.");
                    return;
                }

                ApplySaveData(data);
            }
            catch (Exception e)
            {
                Debug.LogError($"[GearManager] Failed to load gear from '{_savePath}': {e.Message}. Starting empty.");
                _inventory.Clear();
                _loadouts.Clear();
            }
        }

        private GearSaveData BuildSaveData()
        {
            var data = new GearSaveData { saveVersion = CurrentSaveVersion };

            foreach (var pair in _inventory.Owned)
            {
                if (pair.Key == null || string.IsNullOrEmpty(pair.Key.ItemId)) continue;
                data.owned.Add(new OwnedItemEntry { itemId = pair.Key.ItemId, count = pair.Value });
            }

            foreach (var pair in _loadouts)
            {
                var entry = new LoadoutEntry { heroId = pair.Key };
                foreach (GearSlot slot in Enum.GetValues(typeof(GearSlot)))
                {
                    var item = pair.Value.GetEquipped(slot);
                    if (item == null || string.IsNullOrEmpty(item.ItemId)) continue;
                    entry.slots.Add(new EquippedSlotEntry { slot = slot.ToString(), itemId = item.ItemId });
                }
                if (entry.slots.Count > 0) data.loadouts.Add(entry);
            }

            return data;
        }

        private void ApplySaveData(GearSaveData data)
        {
            if (data.owned != null)
            {
                for (int i = 0; i < data.owned.Count; i++)
                {
                    var e = data.owned[i];
                    var item = _catalog.Find(e.itemId);
                    if (item != null) _inventory.Add(item, e.count);
                }
            }

            if (data.loadouts != null)
            {
                for (int i = 0; i < data.loadouts.Count; i++)
                {
                    var le = data.loadouts[i];
                    var loadout = GetLoadout(le.heroId);
                    if (le.slots == null) continue;
                    for (int s = 0; s < le.slots.Count; s++)
                    {
                        var item = _catalog.Find(le.slots[s].itemId);
                        if (item != null) loadout.Equip(item); // placed by item.Slot; fresh loadout → no displaced item
                    }
                }
            }
        }
    }
}
