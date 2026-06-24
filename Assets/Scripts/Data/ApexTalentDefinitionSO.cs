using System.Collections.Generic;
using UnityEngine;

namespace Wavekeep.Data
{
    /// <summary>
    /// An Apex Talent (Task 29): a new, independent, AUTOMATICALLY-triggering ability that unlocks once
    /// all of its required <see cref="UpgradeLineDefinitionSO"/>s reach their max tier. It is NOT a passive
    /// modifier on an existing skill — it is its own <c>IAbility</c> instance with its own cooldown, wired
    /// into <c>HeroRuntime</c>'s tick/execute path alongside (but separate from) the player-controlled
    /// Basic and Ultimate. Read-only at runtime (CLAUDE.md §3.5).
    /// </summary>
    [CreateAssetMenu(fileName = "ApexTalent", menuName = "Wavekeep/Apex Talent")]
    public sealed class ApexTalentDefinitionSO : ScriptableObject
    {
        [Tooltip("The hero that can unlock this apex.")]
        [SerializeField] private HeroDefinitionSO _hero;
        [SerializeField] private string _apexName;
        [Tooltip("Every one of these lines must be at its max tier for the apex to unlock (two or more).")]
        [SerializeField] private List<UpgradeLineDefinitionSO> _requiredLines = new List<UpgradeLineDefinitionSO>();
        [Tooltip("The apex's own ability — a NEW IAbility instance with its own cooldown (reuses the " +
                 "existing AbilityDefinitionSO/AbilityRuntime pattern), not a modifier on Basic/Ultimate.")]
        [SerializeField] private AbilityDefinitionSO _ability;

        public HeroDefinitionSO Hero => _hero;
        public string ApexName => _apexName;
        public IReadOnlyList<UpgradeLineDefinitionSO> RequiredLines => _requiredLines;
        public AbilityDefinitionSO Ability => _ability;
    }
}
