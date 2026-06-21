using UnityEngine;

namespace Wavekeep.Data
{
    /// <summary>
    /// Designer-authored ability template (CLAUDE.md §3.1). Abilities are shared/global assets,
    /// referenced by heroes rather than duplicated (§3.5). Read-only at runtime — live state
    /// (current level, cooldowns) belongs in AbilityRuntime (Task 04).
    ///
    /// Task 01 stubs identity only. The ordered list of per-level upgrade modifiers
    /// (AbilityUpgradeLevel) is added in Task 04.
    /// </summary>
    [CreateAssetMenu(fileName = "AbilityDefinition", menuName = "Wavekeep/Ability Definition")]
    public sealed class AbilityDefinitionSO : ScriptableObject
    {
        [SerializeField] private string _displayName;
        [SerializeField] private Sprite _icon;

        // TODO (Task 04): ordered list of AbilityUpgradeLevel (damage/cooldown/range modifiers per level).

        public string DisplayName => _displayName;
        public Sprite Icon => _icon;
    }
}
