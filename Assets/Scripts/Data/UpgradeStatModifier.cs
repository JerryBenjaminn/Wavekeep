using System;
using UnityEngine;

namespace Wavekeep.Data
{
    /// <summary>
    /// One parametric effect an <see cref="UpgradeDefinitionSO"/> applies while held (Task 19): "adjust
    /// <see cref="Target"/> by <see cref="Value"/> via <see cref="Op"/>". Authored as data on the
    /// upgrade asset; resolved generically by <c>UpgradeInventory.ResolveModifier</c> against held
    /// upgrades. This is how hero-exclusive upgrade numbers (damage, cooldown, radius, frost max stacks,
    /// zone duration/slow, …) reach the runtime without any per-upgrade hardcoding.
    /// </summary>
    [Serializable]
    public sealed class UpgradeStatModifier
    {
        [SerializeField] private UpgradeModifierTarget _target;
        [SerializeField] private UpgradeModifierOp _op = UpgradeModifierOp.Add;
        [SerializeField] private float _value;

        public UpgradeModifierTarget Target => _target;
        public UpgradeModifierOp Op => _op;
        public float Value => _value;
    }
}
