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

        [Header("Abilities (unique per hero, §3.8)")]
        [SerializeField] private AbilityDefinitionSO _basicAbility;
        [SerializeField] private AbilityDefinitionSO _ultimateAbility;

        public string HeroName => _heroName;
        public Sprite Icon => _icon;
        public GameObject Prefab => _prefab;
        public Color Tint => _tint;
        public float BaseHealth => _baseHealth;
        public AbilityDefinitionSO BasicAbility => _basicAbility;
        public AbilityDefinitionSO UltimateAbility => _ultimateAbility;
    }
}
