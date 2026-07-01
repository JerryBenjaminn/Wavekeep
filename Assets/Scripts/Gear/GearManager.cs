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
        // Task 71: drops that arrive while the inventory is at capacity wait here (resolved at the Hub), instead of
        // interrupting the run. Persisted, so it survives the run→Hub scene reload. Holds only unequipped instances.
        private readonly List<GearInstance> _overflow = new List<GearInstance>();
        private readonly Dictionary<string, HeroLoadout> _loadouts = new Dictionary<string, HeroLoadout>();
        private readonly GearCatalogSO _catalog;
        private readonly GearEconomyConfigSO _economy;
        private readonly GearGenerator _generator; // Task 71: forge-only generation (chosen base+rarity)
        private readonly string _savePath;

        public GearInventory Inventory => _inventory;

        /// <summary>Task 71: drops held because the inventory was full when they arrived (resolve at the Hub).</summary>
        public IReadOnlyList<GearInstance> Overflow => _overflow;

        /// <summary>Task 71: hard drop-capacity. From the economy config; unlimited when none is wired (older scenes).
        /// Note: this gates DROPS only — deliberate actions (forge, unequip-return) may exceed it.</summary>
        public int Capacity => _economy != null ? _economy.InventoryCapacity : int.MaxValue;

        public bool InventoryFull => _inventory.Count >= Capacity;

        /// <summary>Persistent salvage material total (Task 67 field; spent/earned by Task 71 salvage + forge).</summary>
        public int SalvageDust { get; private set; }

        public GearManager(GearCatalogSO catalog, string savePath,
            GearAffixCountConfigSO affixConfig = null, GearEconomyConfigSO economy = null)
        {
            _catalog = catalog;
            _savePath = savePath;
            _economy = economy;
            // Task 71: the forge picks base+rarity deterministically, so Luck is irrelevant here (null).
            _generator = new GearGenerator(affixConfig, null);
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

        /// <summary>Add an owned instance to inventory (drop generation / debug spawn) and save. Task 71: when the
        /// inventory is already at capacity, the drop is held in the overflow buffer (resolved at the Hub) rather
        /// than interrupting the run or being lost.</summary>
        public void Grant(GearInstance instance)
        {
            if (instance == null) return;
            if (InventoryFull)
            {
                _overflow.Add(instance);
                Debug.Log($"[GearManager] Inventory full ({_inventory.Count}/{Capacity}); '{instance.ItemName}' " +
                          "held in overflow — resolve it at the Hub.");
            }
            else
            {
                _inventory.Add(instance);
            }
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

        // --- Task 71: salvage + overflow resolution + Artifact Forge ----------------------------

        /// <summary>Salvage an owned, UNEQUIPPED instance (from inventory or the overflow buffer) into Salvage Dust
        /// scaled to its rarity. Equipped instances live in loadouts, NOT here, so they can never be found/salvaged
        /// — that structurally enforces the "unequip first" rule. Returns the Dust awarded (0 if not found). Only
        /// the one salvaged instance is removed; no other item's affixes are touched. Persists.</summary>
        public int Salvage(string instanceId)
        {
            if (string.IsNullOrEmpty(instanceId)) return 0;

            var instance = _inventory.FindById(instanceId);
            bool fromInventory = instance != null;
            if (instance == null) instance = FindOverflow(instanceId);
            if (instance == null) return 0;

            if (fromInventory) _inventory.Remove(instance);
            else _overflow.Remove(instance);

            int yield = _economy != null ? _economy.SalvageYield(instance.Rarity) : 0;
            SalvageDust += yield;
            Save();
            Debug.Log($"[GearManager] Salvaged [{instance.Rarity}] {instance.ItemName} → +{yield} Dust (total {SalvageDust}).");
            return yield;
        }

        /// <summary>Move an overflow-buffered instance into the main inventory when there's room. Returns false if
        /// it isn't in overflow or the inventory is full. Persists on success.</summary>
        public bool ClaimOverflow(string instanceId)
        {
            var instance = FindOverflow(instanceId);
            if (instance == null || InventoryFull) return false;
            _overflow.Remove(instance);
            _inventory.Add(instance);
            Save();
            return true;
        }

        private GearInstance FindOverflow(string instanceId)
        {
            for (int i = 0; i < _overflow.Count; i++)
                if (_overflow[i] != null && _overflow[i].ItemId == instanceId) return _overflow[i];
            return null;
        }

        /// <summary>Artifact Forge (Task 71): deterministically craft an Artifact of the CHOSEN rarity, spending the
        /// scaled Dust cost (Dust only — no persistent currency exists per CLAUDE.md §2). Affixes roll exactly like a
        /// drop of that rarity (Unique → the base's fixed/hand-authored set) — there is NO RNG on the result's
        /// rarity. The craft goes straight to inventory (a deliberate action bypasses the drop cap rather than being
        /// trapped in overflow). Returns the new instance, or null if unaffordable / misconfigured. Creating it never
        /// touches any other instance's affixes. Persists.</summary>
        public GearInstance ForgeArtifact(Rarity rarity)
        {
            int cost = _economy != null ? _economy.ForgeCost(rarity) : 0;
            if (cost <= 0)
            {
                Debug.LogWarning("[GearManager] No GearEconomyConfig wired (or zero forge cost); forge unavailable.");
                return null;
            }
            if (SalvageDust < cost)
            {
                Debug.Log($"[GearManager] Forge {rarity} needs {cost} Dust; have {SalvageDust}.");
                return null;
            }
            if (_catalog == null) { Debug.LogError("[GearManager] No catalog; cannot forge."); return null; }

            var artifactBase = _catalog.FindBaseForSlot(GearSlot.Artifact);
            if (artifactBase == null)
            {
                Debug.LogError("[GearManager] No Artifact GearBase registered in the catalog; cannot forge.");
                return null;
            }

            var instance = _generator.GenerateForBase(artifactBase, rarity);
            if (instance == null) return null;

            SalvageDust -= cost;
            _inventory.Add(instance); // deliberate craft → straight to inventory (may exceed the drop cap)
            Save();
            Debug.Log($"[GearManager] Forged [{rarity}] {instance.ItemName} for {cost} Dust " +
                      $"({instance.Affixes.Count} affix(es)). Dust left {SalvageDust}.");
            return instance;
        }

        // --- Task 75: reroll-affix + upgrade-rarity (Dust sinks) --------------------------------

        /// <summary>Task 75: reroll ONE affix's VALUE (by index) on an owned instance, within that affix type's
        /// per-rarity range (Task 76) for the item's rarity from its <c>AffixDefinitionSO</c>. The affix TYPE never changes, and no other
        /// affix (or the rarity) is touched. Spends the configured Dust cost and persists; returns false (no change)
        /// if the item isn't owned, the index is invalid, the item is Unique (not rerollable), or the player can't
        /// afford it. The instance id is unchanged.</summary>
        public bool RerollAffix(string instanceId, int affixIndex)
        {
            var instance = FindOwnedInstance(instanceId);
            if (instance == null) { Debug.LogWarning("[GearManager] RerollAffix: instance not owned; ignored."); return false; }

            if (instance.Rarity == Rarity.Unique)
            {
                Debug.Log("[GearManager] RerollAffix: Unique affixes are hand-authored and not rerollable.");
                return false;
            }

            var affixes = instance.Affixes;
            if (affixIndex < 0 || affixIndex >= affixes.Count) return false;
            var def = affixes[affixIndex]?.Definition;
            if (def == null) return false;

            int cost = _economy != null ? _economy.RerollAffixCost(instance.Rarity) : 0;
            if (cost <= 0)
            {
                Debug.LogWarning("[GearManager] No GearEconomyConfig wired (or zero reroll cost); reroll unavailable.");
                return false;
            }
            if (SalvageDust < cost)
            {
                Debug.Log($"[GearManager] Reroll needs {cost} Dust; have {SalvageDust}.");
                return false;
            }

            float oldValue = affixes[affixIndex].Value;
            // Task 76: reroll within the per-rarity range for the item's CURRENT rarity (same type, no overlap).
            float newValue = UnityEngine.Random.Range(def.MinValueFor(instance.Rarity), def.MaxValueFor(instance.Rarity));
            instance.ReplaceAffix(affixIndex, new RolledAffix(def, newValue));
            SalvageDust -= cost;
            Save();
            Debug.Log($"[GearManager] Rerolled '{def.DisplayName}' on [{instance.Rarity}] {instance.ItemName}: " +
                      $"{oldValue:0.##} → {newValue:0.##} for {cost} Dust. Dust left {SalvageDust}.");
            return true;
        }

        /// <summary>Task 75: raise an owned instance's rarity by ONE tier (Common→Uncommon…Epic→Legendary). Rolls the
        /// extra affix slot(s) the higher rarity adds (via the same generation logic as drops/forge) and APPENDS them
        /// — every existing affix is preserved verbatim. Spends the configured Dust cost and persists. Hard-capped at
        /// Legendary: returns false (no change) on a Legendary (already at cap) or a Unique (Forge-only, never
        /// upgradeable via this sink), on an unowned instance, or when unaffordable. Can NEVER produce Unique.</summary>
        public bool UpgradeRarity(string instanceId)
        {
            var instance = FindOwnedInstance(instanceId);
            if (instance == null) { Debug.LogWarning("[GearManager] UpgradeRarity: instance not owned; ignored."); return false; }

            if (instance.Rarity == Rarity.Unique)
            {
                Debug.Log("[GearManager] UpgradeRarity: Unique items are Forge-only and cannot be upgraded.");
                return false;
            }
            if (instance.Rarity >= Rarity.Legendary)
            {
                Debug.Log("[GearManager] UpgradeRarity: item is already Legendary (upgrade cap).");
                return false;
            }

            Rarity next = instance.Rarity + 1;
            if (next > Rarity.Legendary) return false; // belt-and-suspenders: this sink never reaches Unique

            int cost = _economy != null ? _economy.UpgradeRarityCost(instance.Rarity) : 0;
            if (cost <= 0)
            {
                Debug.LogWarning("[GearManager] No GearEconomyConfig wired (or zero upgrade cost); upgrade unavailable.");
                return false;
            }
            if (SalvageDust < cost)
            {
                Debug.Log($"[GearManager] Upgrade to {next} needs {cost} Dust; have {SalvageDust}.");
                return false;
            }

            // Roll the new slots BEFORE changing rarity (uses the current affixes to keep types distinct), then
            // append. Existing affixes are never removed/replaced (reviewer-blocking rule).
            var additions = _generator.RollAdditionalAffixes(instance.Base, next, instance.Affixes);
            instance.SetRarity(next);
            instance.AppendAffixes(additions);
            SalvageDust -= cost;
            Save();
            Debug.Log($"[GearManager] Upgraded {instance.ItemName} to [{next}] for {cost} Dust " +
                      $"(+{additions.Count} new affix(es), {instance.Affixes.Count} total). Dust left {SalvageDust}.");
            return true;
        }

        /// <summary>Find an owned instance anywhere it can live: inventory, the overflow buffer, or equipped in any
        /// hero's loadout. Reroll/upgrade mutate the instance in place (it's the same reference the loadout holds),
        /// so an equipped item's stats update live and persist. Returns null if not owned.</summary>
        private GearInstance FindOwnedInstance(string instanceId)
        {
            if (string.IsNullOrEmpty(instanceId)) return null;

            var inst = _inventory.FindById(instanceId);
            if (inst != null) return inst;

            inst = FindOverflow(instanceId);
            if (inst != null) return inst;

            foreach (var pair in _loadouts)
            {
                foreach (GearSlot slot in Enum.GetValues(typeof(GearSlot)))
                {
                    var equipped = pair.Value.GetEquipped(slot);
                    if (equipped != null && equipped.ItemId == instanceId) return equipped;
                }
            }
            return null;
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
            _overflow.Clear();
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
                _overflow.Clear();
                _loadouts.Clear();
                SalvageDust = 0;
            }
        }

        private GearSaveData BuildSaveData()
        {
            var data = new GearSaveData { saveVersion = CurrentSaveVersion, salvageDust = SalvageDust };

            // Every instance — inventory + equipped + overflow — is serialized in full (instances are unique).
            foreach (var item in _inventory.Items)
                if (item != null) data.instances.Add(ToInstanceData(item));

            // Task 71: overflow-buffered instances are serialized too, tagged by id so load restores them to the
            // buffer (not the inventory).
            foreach (var item in _overflow)
                if (item != null)
                {
                    data.instances.Add(ToInstanceData(item));
                    data.overflowInstanceIds.Add(item.ItemId);
                }

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

            // 3) Route the rest: overflow ids → overflow buffer (Task 71); everything else owned-unequipped → inventory.
            var overflowIds = data.overflowInstanceIds != null
                ? new HashSet<string>(data.overflowInstanceIds)
                : new HashSet<string>();
            foreach (var pair in byId)
            {
                if (equipped.Contains(pair.Key)) continue;
                if (overflowIds.Contains(pair.Key)) _overflow.Add(pair.Value);
                else _inventory.Add(pair.Value);
            }
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
