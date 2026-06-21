using UnityEngine;

namespace Wavekeep.Data
{
    /// <summary>
    /// Designer-authored shop item template (CLAUDE.md §3.1). Read-only at runtime.
    /// Task 01 stubs identity + price only; effect type/magnitude/duration are added in Task 06.
    /// </summary>
    [CreateAssetMenu(fileName = "ConsumableDefinition", menuName = "Wavekeep/Consumable Definition")]
    public sealed class ConsumableDefinitionSO : ScriptableObject
    {
        [SerializeField] private string _displayName;
        [SerializeField] private Sprite _icon;

        [Header("Shop")]
        [SerializeField] private int _price;

        // TODO (Task 06): effect type, magnitude, duration.

        public string DisplayName => _displayName;
        public Sprite Icon => _icon;
        public int Price => _price;
    }
}
