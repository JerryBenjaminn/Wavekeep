using UnityEngine;

namespace Wavekeep.Data
{
    /// <summary>
    /// Designer-authored hero template (CLAUDE.md §3.1 / §3.8). Read-only at runtime — mutable
    /// state lives in a parallel HeroRuntime (Task 05). Heroes are data-defined: adding one
    /// should require new assets, not new code.
    ///
    /// Task 01 stubs structural fields only. Later tasks specialise the ability slots into
    /// dedicated Basic/Ultimate SO types and add the optional TagInteractionRule list (§3.8).
    /// </summary>
    [CreateAssetMenu(fileName = "HeroDefinition", menuName = "Wavekeep/Hero Definition")]
    public sealed class HeroDefinitionSO : ScriptableObject
    {
        [SerializeField] private string _displayName;
        [SerializeField] private Sprite _icon;

        [Header("Base Stats (placeholders — tuned in later tasks)")]
        [SerializeField] private float _baseHealth = 100f;

        // TODO (Task 04/05): replace with dedicated BasicAbilityDefinitionSO / UltimateAbilityDefinitionSO
        // (each unique to the hero, §3.8). Referenced as AbilityDefinitionSO for now to stay in Task 01 scope.
        [Header("Abilities")]
        [SerializeField] private AbilityDefinitionSO _basicAbility;
        [SerializeField] private AbilityDefinitionSO _ultimateAbility;

        // TODO (Task 05): optional List<TagInteractionRule> for hero/upgrade tag interactions (§3.8).

        public string DisplayName => _displayName;
        public Sprite Icon => _icon;
        public float BaseHealth => _baseHealth;
        public AbilityDefinitionSO BasicAbility => _basicAbility;
        public AbilityDefinitionSO UltimateAbility => _ultimateAbility;
    }
}
