using UnityEngine;
using Wavekeep.Data;

namespace Wavekeep.Core
{
    /// <summary>
    /// Tiny cross-scene carrier (Task 14) that survives the Hub → gameplay scene load via
    /// <c>DontDestroyOnLoad</c>, holding just the hero the player chose in the Hub. The gameplay scene
    /// reads it (found via <c>FindFirstObjectByType</c>, NOT a static <c>Instance</c> field — §3.5) and
    /// auto-starts that hero, so the player never picks a hero twice.
    ///
    /// Only the HERO rides here; the chosen <c>HeroLoadout</c>/<c>GearInventory</c> flow through the
    /// existing Task 12 DISK save (the Hub equips → GearManager saves → the gameplay bootstrap loads),
    /// so no parallel state path is introduced. A runtime-mutated ScriptableObject blackboard would
    /// violate §3.5, so this plain MonoBehaviour is used instead.
    /// </summary>
    public sealed class RunLaunchContext : MonoBehaviour
    {
        public HeroDefinitionSO SelectedHero { get; private set; }

        public void SetHero(HeroDefinitionSO hero) => SelectedHero = hero;

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
