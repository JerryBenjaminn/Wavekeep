using System.Collections.Generic;
using UnityEngine;

namespace Wavekeep.Data
{
    /// <summary>
    /// Designer-authored hero template (CLAUDE.md §3.1 / §3.8). Read-only at runtime — mutable state
    /// lives in <c>HeroRuntime</c>. Heroes are data-defined: a new hero is purely a new asset (this SO
    /// + its two ability assets) added to the select roster — no code changes (verified in Task 05 §5).
    ///
    /// Per §3.8, a hero's Basic and Ultimate are two <see cref="AbilityDefinitionSO"/> instances unique
    /// to the hero. Tag-interaction rules live on those ability assets (as Task 04 implemented), so no
    /// hero-level rule list is needed here.
    /// </summary>
    [CreateAssetMenu(fileName = "HeroDefinition", menuName = "Wavekeep/Hero Definition")]
    public sealed class HeroDefinitionSO : ScriptableObject
    {
        [SerializeField] private string _heroName;
        [SerializeField] private Sprite _icon;

        [Header("Visual (placeholder)")]
        [Tooltip("Shared capsule prefab for now; heroes are distinguished by Tint until real art exists.")]
        [SerializeField] private GameObject _prefab;
        [SerializeField] private Color _tint = Color.white;

        [Header("Base Stats")]
        [Tooltip("Placeholder hero HP. Not yet consumed — enemies attack the wall, not the hero (Task 02). " +
                 "Heroes differ mechanically via their ability assets, not a hero-level damage scalar, " +
                 "since applying such a scalar would require modifying the Task 04 AbilityRuntime.")]
        [SerializeField] private float _baseHealth = 100f;

        [Tooltip("Task 24: per-hero starting Luck (placeholder magnitude, designer-tunable). One of the three " +
                 "Luck sources combined by HeroRuntime/LuckState (base + equipped gear + in-run potions), " +
                 "clamped to 0–100. Luck reshapes shop offer tiers and, weakly, loot drop tiers — never combat.")]
        [SerializeField, Min(0f)] private float _baseLuck = 5f;

        [Header("Abilities (unique per hero, §3.8)")]
        [SerializeField] private AbilityDefinitionSO _basicAbility;
        [SerializeField] private AbilityDefinitionSO _ultimateAbility;

        [Header("Exclusive Upgrade Pool (§3.8 — only this hero can draw these, alongside the generic pool)")]
        [SerializeField] private List<UpgradeDefinitionSO> _exclusiveUpgrades = new List<UpgradeDefinitionSO>();

        public string HeroName => _heroName;
        public Sprite Icon => _icon;
        public GameObject Prefab => _prefab;
        public Color Tint => _tint;
        public float BaseHealth => _baseHealth;
        public float BaseLuck => _baseLuck;
        public AbilityDefinitionSO BasicAbility => _basicAbility;
        public AbilityDefinitionSO UltimateAbility => _ultimateAbility;

        /// <summary>Upgrades only this hero can be offered by the level-up card picker (Task 11).</summary>
        public IReadOnlyList<UpgradeDefinitionSO> ExclusiveUpgrades => _exclusiveUpgrades;
    }
}
