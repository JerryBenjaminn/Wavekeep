using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Wavekeep.Data;

namespace Wavekeep.Gear
{
    /// <summary>
    /// Owns the player's PERSISTENT gear state (Task 12, redesigned Task 67): the <see cref="GearInventory"/> of
    /// owned <see cref="GearInstance"/>s, one <see cref="HeroLoadout"/> per hero, the persistent Salvage Dust
    /// total, plus disk save/load. A non-static plain C# class held by <c>GameSession</c> — no static singleton.
    ///
    /// Persistence model: disk is the source of truth. The manager LOADS in its constructor and SAVES after every
    /// change (grant/equip/unequip), so gear survives the "Play Again" scene reload while per-run services reset.
    /// Save format is v2 (unique instances). A save below v2 is WIPED on load with a clear log — there is NO
    /// v1→v2 converter, by design (Task 67 locked decision).
    ///
    /// All equip moves go through here so inventory↔loadout stays consistent and replace-not-destroy holds:
    /// equipping consumes the instance from inventory; a displaced instance returns to inventory. SOs are never
    /// mutated — only referenced (CLAUDE.md §3.5).
    /// </summary>
    public sealed class GearManager
    {
        private const int CurrentSaveVersion = 2; // Task 67: bumped from 1 (stacked items) to 2 (unique instances)
        public const string DefaultSaveFileName = "gear_save.json";

        private readonly GearInventory _inventory = new GearInventory();
        private readonly Dictionary<string, HeroLoadout> _loadouts = new Dictionary<string, HeroLoadout>();
        private readonly GearCatalogSO _catalog;
        private readonly string _savePath;

        public GearInventory Inventory => _inventory;

        /// <summary>Persistent salvage material total (Task 67 wires the field; the salvage feature lands later).</summary>
        public int SalvageDust { get; private set; }

        public GearManager(GearCatalogSO catalog, string savePath)
        {
            _catalog = catalog;
            _savePath = savePath;
            Load();
        }

        // --- Loadout access ---------------------------------------------------------------------

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

        /// <summary>Add an owned instance to inventory (debug spawn / future drop generation) and save.</summary>
        public void Grant(GearInstance instance)
        {
            if (instance == null) return;
            _inventory.Add(instance);
            Save();
        }

        /// <summary>
        /// TEMPORARY bridge (Task 67): the legacy <c>LootService</c> drop path still rolls a finished
        /// <see cref="LootItemSO"/>; convert it to a minimal instance (the matching slot's base at the item's
        /// rarity, no affixes) so drops keep flowing until real drop generation (a later task) replaces this.
        /// Drops thus temporarily carry the base IMPLICIT, not the legacy item's exact stat. Remove with that task.
        /// </summary>
        public void Grant(LootItemSO legacyItem)
        {
            if (legacyItem == null) return;
            if (_catalog == null) return;

            var baseTemplate = _catalog.FindBaseForSlot(legacyItem.Slot);
            if (baseTemplate == null)
            {
                Debug.LogWarning($"[GearManager] Legacy drop '{legacyItem.ItemName}' has no GearBase for slot " +
                                 $"{legacyItem.Slot}; drop skipped (temporary bridge until drop generation lands).");
                return;
            }

            Grant(GearInstance.Create(baseTemplate, legacyItem.Rarity, null));
        }

        /// <summary>
        /// Equip an owned instance onto a hero. Consumes it from inventory; any instance already in that slot is
        /// RETURNED to inventory (never destroyed). Returns false (no change) if the player doesn't currently own
        /// it unequipped.
        /// </summary>
        public bool Equip(HeroDefinitionSO hero, GearInstance instance) => Equip(HeroKey(hero), instance);

        public bool Equip(string heroId, GearInstance instance)
        {
            if (instance == null) return false;
            if (!_inventory.Remove(instance)) return false; // must own an unequipped copy of THIS instance

            var previous = GetLoadout(heroId).Equip(instance);
            if (previous != null) _inventory.Add(previous); // replace, don't destroy
            Save();
            return true;
        }

        /// <summary>Unequip a slot, returning the instance to inventory. Returns the removed instance (or null).</summary>
        public GearInstance Unequip(HeroDefinitionSO hero, GearSlot slot) => Unequip(HeroKey(hero), slot);

        public GearInstance Unequip(string heroId, GearSlot slot)
        {
            var removed = GetLoadout(heroId).Unequip(slot);
            if (removed != null)
            {
                _inventory.Add(removed);
                Save();
            }
            return removed;
        }

        private static string HeroKey(HeroDefinitionSO hero) => hero != null ? hero.HeroName : "";

        // --- Persistence ------------------------------------------------------------------------

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

        private void Load()
        {
            _inventory.Clear();
            _loadouts.Clear();
            SalvageDust = 0;

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
                    // Task 67 locked decision: no v1→v2 converter — any older/other version is WIPED with a clear
                    // log so the new instance format can never mis-load a stale stacked-item save.
                    Debug.LogWarning($"[GearManager] Gear save version {data.saveVersion} != {CurrentSaveVersion}; " +
                                     "wiping gear data (no migration by design — fresh start under the new instance format).");
                    return;
                }

                if (_catalog == null)
                {
                    Debug.LogError("[GearManager] No GearCatalog wired; cannot resolve saved instances. Starting empty.");
                    return;
                }

                ApplySaveData(data);
            }
            catch (Exception e)
            {
                Debug.LogError($"[GearManager] Failed to load gear from '{_savePath}': {e.Message}. Starting empty.");
                _inventory.Clear();
                _loadouts.Clear();
                SalvageDust = 0;
            }
        }

        private GearSaveData BuildSaveData()
        {
            var data = new GearSaveData { saveVersion = CurrentSaveVersion, salvageDust = SalvageDust };

            // Every instance — inventory + equipped — is serialized in full (instances are unique, not shared).
            foreach (var item in _inventory.Items)
                if (item != null) data.instances.Add(ToInstanceData(item));

            foreach (var pair in _loadouts)
            {
                var entry = new LoadoutEntry { heroId = pair.Key };
                foreach (GearSlot slot in Enum.GetValues(typeof(GearSlot)))
                {
                    var item = pair.Value.GetEquipped(slot);
                    if (item == null) continue;
                    data.instances.Add(ToInstanceData(item));
                    entry.slots.Add(new EquippedSlotEntry { slot = slot.ToString(), instanceId = item.ItemId });
                }
                if (entry.slots.Count > 0) data.loadouts.Add(entry);
            }

            return data;
        }

        private void ApplySaveData(GearSaveData data)
        {
            SalvageDust = Mathf.Max(0, data.salvageDust);

            // 1) Reconstruct every instance into a map, resolving base + affixes against the catalog.
            var byId = new Dictionary<string, GearInstance>();
            if (data.instances != null)
            {
                for (int i = 0; i < data.instances.Count; i++)
                {
                    var inst = FromInstanceData(data.instances[i]);
                    if (inst != null) byId[inst.ItemId] = inst;
                }
            }

            // 2) Place equipped instances into loadouts (by instanceId); track which ids are equipped.
            var equipped = new HashSet<string>();
            if (data.loadouts != null)
            {
                for (int i = 0; i < data.loadouts.Count; i++)
                {
                    var le = data.loadouts[i];
                    if (le.slots == null) continue;
                    var loadout = GetLoadout(le.heroId);
                    for (int s = 0; s < le.slots.Count; s++)
                    {
                        if (!byId.TryGetValue(le.slots[s].instanceId, out var inst) || inst == null) continue;
                        loadout.Equip(inst); // placed by its own Slot
                        equipped.Add(inst.ItemId);
                    }
                }
            }

            // 3) Everything not equipped is owned-and-unequipped → inventory.
            foreach (var pair in byId)
                if (!equipped.Contains(pair.Key)) _inventory.Add(pair.Value);
        }

        private static GearInstanceData ToInstanceData(GearInstance instance)
        {
            var d = new GearInstanceData
            {
                instanceId = instance.ItemId,
                baseId = instance.Base != null ? instance.Base.BaseId : "",
                rarity = (int)instance.Rarity
            };
            var affixes = instance.Affixes;
            for (int i = 0; i < affixes.Count; i++)
            {
                var a = affixes[i];
                if (a?.Definition == null) continue;
                d.affixes.Add(new RolledAffixData { affixId = a.Definition.AffixId, value = a.Value });
            }
            return d;
        }

        private GearInstance FromInstanceData(GearInstanceData d)
        {
            if (d == null) return null;
            var baseTemplate = _catalog.FindBase(d.baseId);
            if (baseTemplate == null)
            {
                Debug.LogWarning($"[GearManager] Saved instance references unknown base '{d.baseId}'; dropping it.");
                return null;
            }

            var affixes = new List<RolledAffix>();
            if (d.affixes != null)
            {
                for (int i = 0; i < d.affixes.Count; i++)
                {
                    var def = _catalog.FindAffix(d.affixes[i].affixId);
                    if (def != null) affixes.Add(new RolledAffix(def, d.affixes[i].value));
                    // Unknown affix id (e.g. a removed affix) is dropped — surviving affixes are unaffected.
                }
            }

            return new GearInstance(d.instanceId, baseTemplate, (Rarity)d.rarity, affixes);
        }
    }
}
