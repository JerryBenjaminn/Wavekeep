using System.Collections.Generic;
using UnityEngine;
using Wavekeep.Data;

namespace Wavekeep.Core
{
    /// <summary>
    /// Tiny cross-scene carrier (Task 14, generalized to a TEAM in Task 37) that survives the Hub →
    /// gameplay scene load via <c>DontDestroyOnLoad</c>, holding the set of heroes the player chose in the
    /// Hub. The gameplay scene reads it (found via <c>FindFirstObjectByType</c>, NOT a static
    /// <c>Instance</c> field — §3.5) and auto-starts that whole team, so the player never picks twice.
    ///
    /// Task 37: this no longer assumes a single hero. The list may hold one hero (today's original
    /// single-hero behavior, just arrived at through the new selection flow) or several.
    ///
    /// Only the HEROES ride here; each chosen <c>HeroLoadout</c>/<c>GearInventory</c> flows through the
    /// existing Task 12 DISK save (the Hub equips → GearManager saves → the gameplay bootstrap loads), so
    /// no parallel state path is introduced. A runtime-mutated ScriptableObject blackboard would violate
    /// §3.5, so this plain MonoBehaviour is used instead.
    /// </summary>
    public sealed class RunLaunchContext : MonoBehaviour
    {
        private readonly List<HeroDefinitionSO> _selectedHeroes = new List<HeroDefinitionSO>();

        /// <summary>The heroes chosen for the next run, in selection order (one or more).</summary>
        public IReadOnlyList<HeroDefinitionSO> SelectedHeroes => _selectedHeroes;

        /// <summary>Replace the carried team with <paramref name="heroes"/> (nulls dropped).</summary>
        public void SetTeam(IEnumerable<HeroDefinitionSO> heroes)
        {
            _selectedHeroes.Clear();
            if (heroes == null) return;
            foreach (var hero in heroes)
                if (hero != null && !_selectedHeroes.Contains(hero)) _selectedHeroes.Add(hero);
        }

        /// <summary>Find the existing persistent carrier or create one (DontDestroyOnLoad). No static
        /// instance is cached — callers always look it up, so nothing leaks as a global singleton.</summary>
        public static RunLaunchContext GetOrCreate()
        {
            var existing = Object.FindFirstObjectByType<RunLaunchContext>();
            if (existing != null) return existing;

            var go = new GameObject("RunLaunchContext");
            var ctx = go.AddComponent<RunLaunchContext>();
            DontDestroyOnLoad(go);
            return ctx;
        }
    }
}
