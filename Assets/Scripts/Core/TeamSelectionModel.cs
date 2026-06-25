using System.Collections.Generic;
using Wavekeep.Data;

namespace Wavekeep.Core
{
    /// <summary>
    /// Task 37: the data model behind the Hub's team-selection screen. Holds the list of heroes that
    /// currently EXIST/are available, plus which of them the player has toggled into the next run. The UI
    /// (<c>HubController</c>) only ever reads <see cref="Available"/> and pokes <see cref="Toggle"/>/
    /// <see cref="IsSelected"/> — it never hardcodes "show exactly these heroes", so a new hero asset added
    /// to the Hub roster appears automatically with no UI code change.
    ///
    /// A plain C# object (NOT a MonoBehaviour, NOT a static singleton — CLAUDE.md §3.5). It deliberately
    /// does not assume exactly two heroes.
    ///
    /// <b>Task 42 — slot-unlock gate (the plug point Task 37 reserved is now filled):</b> the model takes a
    /// <see cref="MaxSelectableHeroes"/> cap (the persistent <c>HeroSlotUnlockManager.MaxUnlockedHeroSlots</c>).
    /// A hero is UNLOCKED only while its roster position is below that cap — heroes "beyond the unlocked slot
    /// count" stay visible but <see cref="IsUnlocked"/> = false, and <see cref="CanSelect"/> rejects them so a
    /// locked hero can never enter the team. The matching wave requirement for a still-locked hero is exposed
    /// via <see cref="UnlockWaveRequirement"/> so the UI can render a "Reach wave N to unlock" label. The UI
    /// still routes every toggle through <see cref="CanSelect"/>/<see cref="Toggle"/>, so nothing else changed.
    /// </summary>
    public sealed class TeamSelectionModel
    {
        /// <summary>You must bring at least one hero into a run.</summary>
        public const int MinSelectableHeroes = 1;

        private readonly List<HeroDefinitionSO> _available = new List<HeroDefinitionSO>();
        private readonly HashSet<HeroDefinitionSO> _selected = new HashSet<HeroDefinitionSO>();

        // Task 42: how many of the leading roster slots are unlocked (>= 1), and the wave needed to unlock each
        // EXTRA slot beyond slot 1 (index 0 → slot 2). Both supplied by the caller from the persistent unlock
        // manager, keeping this a pure data model with no dependency on persistence/event plumbing.
        private readonly int _maxSelectableHeroes;
        private readonly int[] _unlockWaveMilestones;

        /// <param name="available">All heroes the player may choose from (nulls are dropped).</param>
        /// <param name="maxSelectableHeroes">Persistent unlocked-slot ceiling (Task 42). Clamped to ≥ the
        /// minimum so a run is always startable. Defaults high enough to keep old call sites uncapped.</param>
        /// <param name="unlockWaveMilestones">Wave needed to unlock each extra slot (index 0 → slot 2). May be
        /// null/empty when no gate is supplied — locked rows then simply show no wave number.</param>
        public TeamSelectionModel(IEnumerable<HeroDefinitionSO> available, int maxSelectableHeroes = int.MaxValue,
            int[] unlockWaveMilestones = null)
        {
            _unlockWaveMilestones = unlockWaveMilestones ?? System.Array.Empty<int>();
            if (available != null)
                foreach (var hero in available)
                    if (hero != null && !_available.Contains(hero)) _available.Add(hero);

            _maxSelectableHeroes = System.Math.Max(MinSelectableHeroes, maxSelectableHeroes);
        }

        /// <summary>The persistent cap on how many heroes may be brought into a run (Task 42). Equal to the
        /// number of leading roster slots that are currently unlocked.</summary>
        public int MaxSelectableHeroes => _maxSelectableHeroes;

        /// <summary>True when <paramref name="hero"/> sits within the unlocked slot range (its roster position
        /// is below <see cref="MaxSelectableHeroes"/>). Locked heroes stay visible but cannot be selected.</summary>
        public bool IsUnlocked(HeroDefinitionSO hero)
        {
            int index = hero != null ? _available.IndexOf(hero) : -1;
            return index >= 0 && index < _maxSelectableHeroes;
        }

        /// <summary>For a still-locked hero, the wave a single run must clear to unlock its slot (0 if unknown
        /// or already unlocked). Drives the Hub's "Reach wave N to unlock" label.</summary>
        public int UnlockWaveRequirement(HeroDefinitionSO hero)
        {
            int index = hero != null ? _available.IndexOf(hero) : -1;
            if (index < 0) return 0;
            int milestoneIndex = index - 1; // slot (index+1); slot 2 → milestone[0]
            if (milestoneIndex < 0 || milestoneIndex >= _unlockWaveMilestones.Length) return 0;
            return _unlockWaveMilestones[milestoneIndex];
        }

        /// <summary>Every selectable hero, in roster order. The UI iterates this — no hardcoded slots.</summary>
        public IReadOnlyList<HeroDefinitionSO> Available => _available;

        /// <summary>How many heroes are currently toggled into the team.</summary>
        public int SelectedCount => _selected.Count;

        public bool IsSelected(HeroDefinitionSO hero) => hero != null && _selected.Contains(hero);

        /// <summary>
        /// Whether <paramref name="hero"/> may be turned ON right now: it must be available, UNLOCKED
        /// (Task 42 — within the unlocked slot range), and either already selected (so it can be toggled OFF)
        /// or below the <see cref="MaxSelectableHeroes"/> cap. See the class summary.
        /// </summary>
        public bool CanSelect(HeroDefinitionSO hero)
        {
            if (hero == null || !_available.Contains(hero)) return false;
            if (IsSelected(hero)) return true; // already in the team → always allowed (lets it be toggled off)
            return IsUnlocked(hero) && _selected.Count < _maxSelectableHeroes;
        }

        /// <summary>Flip a hero's membership. Honors <see cref="CanSelect"/> when turning one ON.</summary>
        public void Toggle(HeroDefinitionSO hero)
        {
            if (hero == null) return;
            if (_selected.Contains(hero)) _selected.Remove(hero);
            else if (CanSelect(hero)) _selected.Add(hero);
        }

        /// <summary>True once enough heroes are selected to legally start a run (≥ the minimum).</summary>
        public bool CanStartRun => _selected.Count >= MinSelectableHeroes;

        /// <summary>The selected heroes, returned in <see cref="Available"/> order so team/spawn order is
        /// stable run-to-run rather than hash-set order.</summary>
        public List<HeroDefinitionSO> GetSelectedTeam()
        {
            var team = new List<HeroDefinitionSO>(_selected.Count);
            for (int i = 0; i < _available.Count; i++)
                if (_selected.Contains(_available[i])) team.Add(_available[i]);
            return team;
        }

        /// <summary>Force a hero's membership (used to seed a sensible default selection on open). Turning a
        /// hero ON respects the Task 42 unlock gate, so a locked hero can never be seeded into the team.</summary>
        public void SetSelected(HeroDefinitionSO hero, bool selected)
        {
            if (hero == null || !_available.Contains(hero)) return;
            if (selected)
            {
                if (IsUnlocked(hero)) _selected.Add(hero);
            }
            else _selected.Remove(hero);
        }
    }
}
