using UnityEngine;

namespace Wavekeep.Data
{
    /// <summary>
    /// Designer-authored shop item template (CLAUDE.md §3.1). Read-only at runtime — never mutated
    /// (mutable run state lives in <c>ConsumableInventory</c>). Task 01 stubbed identity + price;
    /// Task 06 fleshes out the effect data.
    ///
    /// Duration convention (Task 06 §1): <see cref="Duration"/> &lt;= 0 means the effect is permanent
    /// for the remainder of the run; &gt; 0 means it lasts that many seconds before expiring. Instant
    /// effects (e.g. <see cref="ConsumableEffectType.HealWall"/>) ignore duration entirely.
    /// </summary>
    [CreateAssetMenu(fileName = "ConsumableDefinition", menuName = "Wavekeep/Consumable Definition")]
    public sealed class ConsumableDefinitionSO : ScriptableObject
    {
        [SerializeField] private string _displayName;
        [SerializeField] private Sprite _icon;
        [TextArea, SerializeField] private string _description;

        [Header("Shop")]
        [SerializeField, Min(0)] private int _price;
        [Tooltip("If false, the player may own only one of this item per run (e.g. a unique elixir).")]
        [SerializeField] private bool _stackable = true;
        [Tooltip("Power tier (Task 09): T1 weakest → T3 strongest. Shown as a [T#] label in the shop.")]
        [SerializeField] private ConsumableTier _tier = ConsumableTier.Tier1;

        [Header("Effect")]
        [SerializeField] private ConsumableEffectType _effectType;
        [Tooltip("Magnitude of the effect (flat damage added, cooldown multiplier, or wall HP healed).")]
        [SerializeField] private float _effectValue;
        [Tooltip("Seconds the effect lasts. <= 0 means permanent for the run; instant effects ignore this.")]
        [SerializeField] private float _duration;

        public string DisplayName => _displayName;
        public Sprite Icon => _icon;
        public string Description => _description;
        public int Price => _price;
        public bool Stackable => _stackable;
        public ConsumableTier Tier => _tier;
        public ConsumableEffectType EffectType => _effectType;
        public float EffectValue => _effectValue;
        public float Duration => _duration;

        /// <summary>True when this effect persists for the whole run rather than expiring on a timer.</summary>
        public bool IsPermanent => _duration <= 0f;
    }
}
