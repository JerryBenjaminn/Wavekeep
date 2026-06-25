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
    /// <b>Future slot-gate plug point (intentionally NOT implemented this task):</b> the only cap enforced
    /// today is the <see cref="MinSelectableHeroes"/> floor (you need ≥1 to start). A later progression
    /// task will add a <c>MaxSelectableHeroes</c> value (raised by wave milestones) and have
    /// <see cref="CanSelect"/> return <c>IsSelected(hero) || SelectedCount &lt; MaxSelectableHeroes</c>.
    /// Because the UI already routes every toggle through <see cref="CanSelect"/> and <see cref="Toggle"/>,
    /// that gate slots in here without reworking the screen.
    /// </summary>
    public sealed class TeamSelectionModel
    {
        /// <summary>You must bring at least one hero into a run. (The matching MAX cap is the deferred gate.)</summary>
        public const int MinSelectableHeroes = 1;

        private readonly List<HeroDefinitionSO> _available = new List<HeroDefinitionSO>();
        private readonly HashSet<HeroDefinitionSO> _selected = new HashSet<HeroDefinitionSO>();

        /// <param name="available">All heroes the player may choose from (nulls are dropped).</param>
        public TeamSelectionModel(IEnumerable<HeroDefinitionSO> available)
        {
            if (available == null) return;
            foreach (var hero in available)
                if (hero != null && !_available.Contains(hero)) _available.Add(hero);
        }

        /// <summary>Every selectable hero, in roster order. The UI iterates this — no hardcoded slots.</summary>
        public IReadOnlyList<HeroDefinitionSO> Available => _available;

        /// <summary>How many heroes are currently toggled into the team.</summary>
        public int SelectedCount => _selected.Count;

        public bool IsSelected(HeroDefinitionSO hero) => hero != null && _selected.Contains(hero);

        /// <summary>
        /// Whether <paramref name="hero"/> may be turned ON right now. Always true today (no max cap);
        /// the future slot-gate adds the upper bound here (already-selected heroes always pass so they can
        /// be toggled OFF). See the class summary.
        /// </summary>
        public bool CanSelect(HeroDefinitionSO hero) => hero != null && _available.Contains(hero);

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

        /// <summary>Force a hero's membership (used to seed a sensible default selection on open).</summary>
        public void SetSelected(HeroDefinitionSO hero, bool selected)
        {
            if (hero == null || !_available.Contains(hero)) return;
            if (selected) _selected.Add(hero);
            else _selected.Remove(hero);
        }
    }
}
