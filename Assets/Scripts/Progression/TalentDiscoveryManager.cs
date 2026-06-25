using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Wavekeep.Core;
using Wavekeep.Core.Events;
using Wavekeep.Data;

namespace Wavekeep.Progression
{
    /// <summary>
    /// Owns the PERSISTENT apex/combo-apex discovery state (Task 43), the Vampire-Survivors "you've now learned
    /// this" codex layer. Disk-backed like the Task 12 gear save and Task 42 hero-slot save (its own file; load
    /// in the constructor, save on change). A non-static plain C# class held by <see cref="GameSession"/> — no
    /// static singleton (CLAUDE.md §3.5).
    ///
    /// <para>Detection: it subscribes to <see cref="ApexUnlockedEvent"/> (published by <c>HeroRuntime</c> the
    /// first time an apex unlocks in a run). On each, it (1) discovers that apex, and (2) re-evaluates the run's
    /// cross-hero combos via the shared <see cref="ComboApexState"/> — a combo can only newly satisfy its
    /// "both apexes unlocked" rule right after an apex unlocks, so this is the exact moment to check. Every
    /// FIRST-ever discovery (id not already in the persistent set) is recorded permanently and announced via a
    /// single <see cref="TalentDiscoveredEvent"/>; subsequent unlocks of an already-discovered talent are
    /// silent (only the Task 32 cooldown/active indicator applies).</para>
    ///
    /// <para>Discovery is keyed by the talent SO's asset <c>name</c> — a stable id that needs no new authored
    /// field, matching how the Codex pairs each id back to its SO. Reconstructed per scene load, so the Hub
    /// Codex (built on the next scene) reads the freshly-loaded set with no restart.</para>
    /// </summary>
    public sealed class TalentDiscoveryManager : IDisposable
    {
        private const int CurrentSaveVersion = 1;
        public const string DefaultSaveFileName = "talent_discovery.json";

        private readonly EventBus _events;
        private readonly ComboApexState _comboApex;
        private readonly string _savePath;
        private readonly HashSet<string> _discovered = new HashSet<string>();

        public TalentDiscoveryManager(EventBus events, ComboApexState comboApex, string savePath)
        {
            _events = events;
            _comboApex = comboApex;
            _savePath = savePath;

            Load();

            // Apex unlocks arrive via the bus (HeroRuntime publishes). Inert in the Hub scene (none fire there).
            _events?.Subscribe<ApexUnlockedEvent>(OnApexUnlocked);
        }

        /// <summary>The stable discovery id for a talent SO (apex or combo): its asset name. Null/empty for a
        /// missing asset. Centralised so the manager and the Codex agree on the key.</summary>
        public static string TalentId(ScriptableObject talent) => talent != null ? talent.name : null;

        /// <summary>True if the given talent has ever been discovered (persisted). Drives the Codex's
        /// detail-vs-"???" decision.</summary>
        public bool IsDiscovered(ScriptableObject talent) => IsDiscovered(TalentId(talent));

        public bool IsDiscovered(string talentId) =>
            !string.IsNullOrEmpty(talentId) && _discovered.Contains(talentId);

        // --- Discovery detection ----------------------------------------------------------------

        private void OnApexUnlocked(ApexUnlockedEvent evt)
        {
            // 1) The apex itself.
            TryDiscover(evt.Apex, TalentId(evt.Apex), isCombo: false);

            // 2) Any combo that just became unlocked by this apex completing its pair. The combo resolver reads
            //    the live hero registry, so by the time this event fires (HeroRuntime already marked the apex
            //    unlocked before publishing) IsUnlocked is accurate.
            var combos = _comboApex != null ? _comboApex.Combos : null;
            if (combos == null) return;
            for (int i = 0; i < combos.Count; i++)
            {
                var combo = combos[i];
                if (combo != null && _comboApex.IsUnlocked(combo))
                    TryDiscover(combo, TalentId(combo), isCombo: true);
            }
        }

        // Records a first-ever discovery (persisting) and announces it. No-op (and silent) if already known —
        // this is what keeps the first-discovery notification firing ONLY the first time ever.
        private void TryDiscover(ScriptableObject talent, string id, bool isCombo)
        {
            if (talent == null || string.IsNullOrEmpty(id)) return;
            if (!_discovered.Add(id)) return; // already discovered → no save, no notification

            Save();

            string displayName = DisplayName(talent, isCombo);
            Debug.Log($"[TalentDiscoveryManager] FIRST DISCOVERY: '{displayName}' ({(isCombo ? "combo" : "apex")}) " +
                      "— recorded permanently.");
            _events?.Publish(new TalentDiscoveredEvent(displayName, isCombo));
        }

        private static string DisplayName(ScriptableObject talent, bool isCombo)
        {
            if (isCombo && talent is ComboApexTalentDefinitionSO combo && !string.IsNullOrEmpty(combo.ComboName))
                return combo.ComboName;
            if (!isCombo && talent is ApexTalentDefinitionSO apex && !string.IsNullOrEmpty(apex.ApexName))
                return apex.ApexName;
            return talent.name;
        }

        // --- Persistence ------------------------------------------------------------------------

        /// <summary>Serialize the discovered set to disk (versioned JSON). Failures are logged, not thrown.</summary>
        public void Save()
        {
            try
            {
                var data = new TalentDiscoverySaveData { saveVersion = CurrentSaveVersion };
                data.discoveredTalentIds.AddRange(_discovered);
                File.WriteAllText(_savePath, JsonUtility.ToJson(data, prettyPrint: true));
            }
            catch (Exception e)
            {
                Debug.LogError($"[TalentDiscoveryManager] Failed to save to '{_savePath}': {e.Message}");
            }
        }

        // Loads the discovered set. Missing file = fresh empty set (acceptable per Task 43 — no retroactive
        // crediting of pre-system unlocks). Corrupt/unreadable or wrong-version = log + empty, never a crash.
        private void Load()
        {
            _discovered.Clear();

            try
            {
                if (!File.Exists(_savePath))
                {
                    Debug.Log($"[TalentDiscoveryManager] No save at '{_savePath}'; starting with nothing discovered.");
                    return;
                }

                var data = JsonUtility.FromJson<TalentDiscoverySaveData>(File.ReadAllText(_savePath));
                if (data == null)
                {
                    Debug.LogWarning("[TalentDiscoveryManager] Save file empty/unparseable; starting empty.");
                    return;
                }

                if (data.saveVersion != CurrentSaveVersion)
                {
                    Debug.LogWarning($"[TalentDiscoveryManager] Save version {data.saveVersion} != " +
                                     $"{CurrentSaveVersion}; ignoring old save.");
                    return;
                }

                if (data.discoveredTalentIds != null)
                    for (int i = 0; i < data.discoveredTalentIds.Count; i++)
                        if (!string.IsNullOrEmpty(data.discoveredTalentIds[i]))
                            _discovered.Add(data.discoveredTalentIds[i]);
            }
            catch (Exception e)
            {
                Debug.LogError($"[TalentDiscoveryManager] Failed to load from '{_savePath}': {e.Message}. Starting empty.");
                _discovered.Clear();
            }
        }

        public void Dispose()
        {
            _events?.Unsubscribe<ApexUnlockedEvent>(OnApexUnlocked);
        }
    }
}
