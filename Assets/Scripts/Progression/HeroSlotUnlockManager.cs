using System;
using System.IO;
using UnityEngine;
using Wavekeep.Core;
using Wavekeep.Core.Events;

namespace Wavekeep.Progression
{
    /// <summary>
    /// Owns the PERSISTENT hero-slot unlock progression (Task 42). How many hero slots the player may bring
    /// into a run unlock permanently — saved across runs with the SAME model as Task 12's gear save (disk is
    /// the source of truth; load in the constructor, save on change). A non-static plain C# class held by
    /// <see cref="GameSession"/> — no static singleton (CLAUDE.md §3.5).
    ///
    /// <para>Unlock rule: slot 1 is always unlocked; slots 2/3/4 unlock the first time a single run CLEARS the
    /// matching wave milestone (defaults 15/30/50). "Cleared" is keyed off <see cref="WaveCompletedEvent"/> —
    /// a wave counts only once fully survived — so reaching wave 16 and then dying still counts as having
    /// cleared wave 15, while dying mid-wave-15 does not. Milestones are evaluated on
    /// <see cref="RunEndedEvent"/> against the highest wave cleared THIS run, regardless of win/loss, and only
    /// ever RAISE the ceiling (never lower it).</para>
    ///
    /// <para>Because <c>GameSessionBootstrap</c> rebuilds the session on every scene load, a reconstructed
    /// manager reloads identical state from disk — so a slot unlocked during a run is reflected the instant
    /// the player returns to the Hub, with no app restart (the Hub's fresh manager reads the new value).</para>
    /// </summary>
    public sealed class HeroSlotUnlockManager : IDisposable
    {
        private const int CurrentSaveVersion = 1;
        public const string DefaultSaveFileName = "hero_slot_unlocks.json";

        /// <summary>Slot 1 is always available — the floor no progression can drop below.</summary>
        public const int MinHeroSlots = 1;

        private readonly EventBus _events;
        private readonly string _savePath;

        // Wave threshold to unlock each EXTRA slot beyond slot 1: index 0 → slot 2, index 1 → slot 3, etc.
        // Ascending. Sourced from the bootstrap (tunable), defaulting to the task's 15/30/50.
        private readonly int[] _milestones;

        private int _maxUnlockedHeroSlots = MinHeroSlots;
        private int _highestWaveClearedThisRun; // run-scoped; resets to 0 with each fresh manager (scene load)

        /// <summary>Raised whenever the unlocked ceiling increases (so any live listener can refresh). Not
        /// required for the Hub's between-run refresh — that happens via the fresh disk load on scene reload —
        /// but available for any same-scene UI that wants to react immediately.</summary>
        public event Action<int> OnUnlocksChanged;

        public HeroSlotUnlockManager(EventBus events, string savePath, int[] milestones)
        {
            _events = events;
            _savePath = savePath;
            _milestones = SanitizeMilestones(milestones);

            Load();

            // A run's progress feeds in through the bus: track the highest cleared wave, then evaluate the
            // milestones once the run ends. In the Hub scene neither event ever fires, so this is inert there.
            _events?.Subscribe<WaveCompletedEvent>(OnWaveCompleted);
            _events?.Subscribe<RunEndedEvent>(OnRunEnded);
        }

        /// <summary>The permanently-unlocked hero-slot ceiling (1..<see cref="MaxPossibleHeroSlots"/>). The Hub
        /// team-selection panel gates the maximum selectable hero count against this.</summary>
        public int MaxUnlockedHeroSlots => _maxUnlockedHeroSlots;

        /// <summary>Total slots that can ever be unlocked (slot 1 + one per configured milestone).</summary>
        public int MaxPossibleHeroSlots => MinHeroSlots + _milestones.Length;

        /// <summary>The wave a player must clear to unlock a given 1-based <paramref name="slotNumber"/>
        /// (slot 1 returns 0 — always unlocked). Returns 0 for slots with no configured milestone. The Hub
        /// reads this to render a "Reach wave N to unlock" label on still-locked slots.</summary>
        public int WaveToUnlockSlot(int slotNumber)
        {
            int milestoneIndex = slotNumber - 2; // slot 2 → milestone[0]
            if (milestoneIndex < 0 || milestoneIndex >= _milestones.Length) return 0;
            return _milestones[milestoneIndex];
        }

        // --- Run progress tracking --------------------------------------------------------------

        // WaveCompletedEvent carries a 0-based index (Task 02); the player-facing wave number is +1, matching
        // WaveSpawner.CurrentWaveNumber and the milestone thresholds. A wave reaching here means it was fully
        // cleared, which is exactly the "survived past it" semantic the task requires.
        private void OnWaveCompleted(WaveCompletedEvent evt)
        {
            int waveNumber = evt.WaveIndex + 1;
            if (waveNumber > _highestWaveClearedThisRun) _highestWaveClearedThisRun = waveNumber;
        }

        // Evaluate milestones on run end (win OR loss) against the highest wave cleared this run.
        private void OnRunEnded(RunEndedEvent evt) => EvaluateMilestones(_highestWaveClearedThisRun);

        /// <summary>Raise the unlock ceiling for every milestone the given <paramref name="highestWaveCleared"/>
        /// has met, persisting if it changed. Never lowers the ceiling. Public so it can be unit-tested /
        /// debug-driven without faking the event flow. Returns the (possibly unchanged) ceiling.</summary>
        public int EvaluateMilestones(int highestWaveCleared)
        {
            int newMax = _maxUnlockedHeroSlots;
            for (int i = 0; i < _milestones.Length; i++)
            {
                if (highestWaveCleared >= _milestones[i])
                {
                    int slot = i + 2; // milestone i unlocks slot i+2
                    if (slot > newMax) newMax = slot;
                }
            }

            if (newMax > _maxUnlockedHeroSlots)
            {
                _maxUnlockedHeroSlots = newMax;
                Save();
                Debug.Log($"[HeroSlotUnlockManager] Cleared wave {highestWaveCleared} → unlocked hero slots " +
                          $"now {_maxUnlockedHeroSlots}.");
                OnUnlocksChanged?.Invoke(_maxUnlockedHeroSlots);
            }

            return _maxUnlockedHeroSlots;
        }

        // --- Persistence ------------------------------------------------------------------------

        /// <summary>Serialize current state to disk (versioned JSON). Failures are logged, not thrown.</summary>
        public void Save()
        {
            try
            {
                var data = new HeroSlotUnlockSaveData
                {
                    saveVersion = CurrentSaveVersion,
                    maxUnlockedHeroSlots = _maxUnlockedHeroSlots
                };
                File.WriteAllText(_savePath, JsonUtility.ToJson(data, prettyPrint: true));
            }
            catch (Exception e)
            {
                Debug.LogError($"[HeroSlotUnlockManager] Failed to save to '{_savePath}': {e.Message}");
            }
        }

        // Loads state from disk. Missing file = fresh state (slot 1 only). Corrupt/unreadable file or an
        // unrecognised version = log + default, so a bad save can never hard-crash startup or REVOKE unlocks.
        private void Load()
        {
            _maxUnlockedHeroSlots = MinHeroSlots;

            try
            {
                if (!File.Exists(_savePath))
                {
                    Debug.Log($"[HeroSlotUnlockManager] No save at '{_savePath}'; starting with slot 1 only.");
                    return;
                }

                var data = JsonUtility.FromJson<HeroSlotUnlockSaveData>(File.ReadAllText(_savePath));
                if (data == null)
                {
                    Debug.LogWarning("[HeroSlotUnlockManager] Save file empty/unparseable; starting with slot 1 only.");
                    return;
                }

                if (data.saveVersion != CurrentSaveVersion)
                {
                    Debug.LogWarning($"[HeroSlotUnlockManager] Save version {data.saveVersion} != " +
                                     $"{CurrentSaveVersion}; ignoring old save.");
                    return;
                }

                // Clamp into the valid range so a hand-edited/corrupt value can't grant or strip impossible slots.
                _maxUnlockedHeroSlots = Mathf.Clamp(data.maxUnlockedHeroSlots, MinHeroSlots, MaxPossibleHeroSlots);
            }
            catch (Exception e)
            {
                Debug.LogError($"[HeroSlotUnlockManager] Failed to load from '{_savePath}': {e.Message}. " +
                               "Starting with slot 1 only.");
                _maxUnlockedHeroSlots = MinHeroSlots;
            }
        }

        // Drop nulls/non-positive entries and guarantee ascending order, so the slot↔milestone mapping is sane
        // even if the bootstrap field is mis-authored. Empty/null → no extra slots (slot 1 stays the cap).
        private static int[] SanitizeMilestones(int[] milestones)
        {
            if (milestones == null || milestones.Length == 0) return Array.Empty<int>();

            var cleaned = new System.Collections.Generic.List<int>(milestones.Length);
            foreach (int m in milestones)
                if (m > 0) cleaned.Add(m);
            cleaned.Sort();
            return cleaned.ToArray();
        }

        public void Dispose()
        {
            _events?.Unsubscribe<WaveCompletedEvent>(OnWaveCompleted);
            _events?.Unsubscribe<RunEndedEvent>(OnRunEnded);
        }
    }
}
