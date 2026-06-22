using UnityEngine;
using Wavekeep.Data;
using Wavekeep.Runtime;

namespace Wavekeep.Economy
{
    /// <summary>
    /// Between-wave shop purchase logic (Task 06 §3). A non-static plain C# class (no <c>Instance</c>),
    /// constructed by the shop UI from <c>GameSession</c> services plus the scene's
    /// <see cref="WallRuntime"/>. Holds no UI — the UI calls <see cref="TryPurchase"/> and renders the
    /// result.
    ///
    /// Effect routing (Task 06 §2) goes through EXISTING systems, never a parallel path:
    /// <list type="bullet">
    /// <item><see cref="ConsumableEffectType.FlatDamageBoost"/> / <see cref="ConsumableEffectType.CooldownReduction"/>
    ///   are added to <see cref="ConsumableInventory"/>, which <c>AbilityRuntime.ComputeStats</c> reads
    ///   alongside its tag-interaction modifiers.</item>
    /// <item><see cref="ConsumableEffectType.HealWall"/> calls <see cref="WallRuntime.Heal"/> directly.</item>
    /// </list>
    /// </summary>
    public sealed class ShopController
    {
        private readonly CurrencyManager _currency;
        private readonly ConsumableInventory _inventory;
        private readonly WallRuntime _wall;

        public ShopController(CurrencyManager currency, ConsumableInventory inventory, WallRuntime wall)
        {
            _currency = currency;
            _inventory = inventory;
            _wall = wall;
        }

        /// <summary>True if the item can be bought right now (stackable rule + affordability). The UI
        /// uses this to enable/grey buttons; <see cref="TryPurchase"/> re-checks before spending.</summary>
        public bool CanPurchase(ConsumableDefinitionSO item)
        {
            if (item == null) return false;
            if (!item.Stackable && _inventory.Owns(item)) return false;
            return _currency.CurrentCurrency >= item.Price;
        }

        /// <summary>
        /// Validate the stackable rule, then attempt to spend via <see cref="CurrencyManager.TrySpend"/>.
        /// Currency is only deducted by TrySpend, which never lets the total go negative. On success the
        /// purchase is recorded and the effect applied through the existing systems (per §2). Returns
        /// false and changes nothing on a blocked/unaffordable purchase.
        /// </summary>
        public bool TryPurchase(ConsumableDefinitionSO item)
        {
            if (item == null) return false;

            // Non-stackable items can only be owned once per run.
            if (!item.Stackable && _inventory.Owns(item)) return false;

            // TrySpend is the single source of truth for the spend; it validates funds first.
            if (!_currency.TrySpend(item.Price)) return false;

            _inventory.RegisterPurchase(item);
            ApplyEffect(item);
            Debug.Log($"[ShopController] Purchased '{item.DisplayName}' for {item.Price}. Currency now {_currency.CurrentCurrency}.");
            return true;
        }

        private void ApplyEffect(ConsumableDefinitionSO item)
        {
            switch (item.EffectType)
            {
                case ConsumableEffectType.FlatDamageBoost:
                case ConsumableEffectType.CooldownReduction:
                    // Ongoing ability modifier — read by AbilityRuntime's existing ComputeStats pipeline.
                    _inventory.AddEffect(item.EffectType, item.EffectValue, item.Duration);
                    break;

                case ConsumableEffectType.HealWall:
                    // Instant effect routed through the existing wall, not a parallel HP path.
                    if (_wall != null) _wall.Heal(item.EffectValue);
                    else Debug.LogWarning("[ShopController] HealWall purchased but no WallRuntime is wired.");
                    break;
            }
        }
    }
}
