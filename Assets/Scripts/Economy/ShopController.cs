using System.Collections.Generic;
using UnityEngine;
using Wavekeep.Data;
using Wavekeep.Runtime;

namespace Wavekeep.Economy
{
    /// <summary>
    /// Between-wave shop purchase + offer logic (Task 06 §3, extended in Task 09). A non-static plain
    /// C# class (no <c>Instance</c>), constructed by the shop UI from <c>GameSession</c> services plus
    /// the scene's <see cref="WallRuntime"/>. Holds no UI — the UI renders <see cref="CurrentOffer"/>
    /// and calls <see cref="TryPurchase"/> / <see cref="TryReroll"/>.
    ///
    /// Task 09 adds a per-visit OFFER: a fixed-size random subset (<see cref="_offerSize"/>, = Task 06's
    /// display count) drawn from the full consumable pool, re-drawable via <see cref="TryReroll"/> at the
    /// cost of one reroll point. Offer draw is uniform random across all tiers (tier-weighted probability
    /// is a documented follow-up that hooks into the separate wave-scaling task). Generating a new offer
    /// on shop open is FREE — only the explicit reroll spends a point, so opening the shop never resets
    /// the reroll pool.
    ///
    /// Effect routing (Task 06 §2) goes through EXISTING systems, never a parallel path:
    /// <list type="bullet">
    /// <item><see cref="ConsumableEffectType.FlatDamageBoost"/> / <see cref="ConsumableEffectType.CooldownReduction"/>
    ///   are added to <see cref="ConsumableInventory"/>, read by <c>AbilityRuntime.ComputeStats</c>.</item>
    /// <item><see cref="ConsumableEffectType.HealWall"/> calls <see cref="WallRuntime.Heal"/>.</item>
    /// <item><see cref="ConsumableEffectType.GainRerollPoints"/> calls <see cref="RerollManager.Add"/> —
    ///   the Reroll Potion is just another consumable on the same <see cref="TryPurchase"/> path.</item>
    /// </list>
    /// </summary>
    public sealed class ShopController
    {
        private readonly CurrencyManager _currency;
        private readonly ConsumableInventory _inventory;
        private readonly WallRuntime _wall;
        private readonly RerollManager _reroll;
        private readonly IReadOnlyList<ConsumableDefinitionSO> _pool;
        private readonly int _offerSize;

        private readonly List<ConsumableDefinitionSO> _offer = new List<ConsumableDefinitionSO>();
        private readonly List<int> _drawIndices = new List<int>(); // scratch for the distinct draw

        // Task 17: which items of the CURRENT offer have already been bought this offer. Keyed by the SO
        // reference — an offer holds distinct items (GenerateOffer draws a distinct subset), so this is
        // exactly per-item-in-offer tracking, NOT a single global purchase counter. Cleared by
        // GenerateOffer, so both a reroll and a fresh shop visit naturally start with nothing purchased.
        private readonly HashSet<ConsumableDefinitionSO> _purchasedThisOffer = new HashSet<ConsumableDefinitionSO>();

        public ShopController(
            CurrencyManager currency,
            ConsumableInventory inventory,
            WallRuntime wall,
            RerollManager reroll,
            IReadOnlyList<ConsumableDefinitionSO> pool,
            int offerSize)
        {
            _currency = currency;
            _inventory = inventory;
            _wall = wall;
            _reroll = reroll;
            _pool = pool;
            _offerSize = Mathf.Max(1, offerSize);
        }

        /// <summary>The items currently offered this visit (read-only). Re-populated by
        /// <see cref="GenerateOffer"/> / <see cref="TryReroll"/>.</summary>
        public IReadOnlyList<ConsumableDefinitionSO> CurrentOffer => _offer;

        /// <summary>Reroll points available this run (separate resource from currency).</summary>
        public int RerollPoints => _reroll != null ? _reroll.CurrentPoints : 0;

        /// <summary>True while a reroll can be spent (used to enable/disable the reroll button).</summary>
        public bool CanReroll => _reroll != null && _reroll.CanReroll;

        /// <summary>Draw a fresh offer (FREE — does not touch reroll points). Called on each shop open.</summary>
        public void GenerateOffer()
        {
            _offer.Clear();
            // A freshly rolled offer has nothing purchased yet (Task 17) — this single reset covers both
            // the per-visit open AND a reroll (which calls through here), so no redundant reset is needed.
            _purchasedThisOffer.Clear();
            if (_pool == null) return;

            // Collect valid pool indices, then partial Fisher–Yates to pick a distinct random subset.
            _drawIndices.Clear();
            for (int i = 0; i < _pool.Count; i++)
            {
                if (_pool[i] != null) _drawIndices.Add(i);
            }

            int want = Mathf.Min(_offerSize, _drawIndices.Count);
            for (int k = 0; k < want; k++)
            {
                int swap = Random.Range(k, _drawIndices.Count);
                (_drawIndices[k], _drawIndices[swap]) = (_drawIndices[swap], _drawIndices[k]);
                _offer.Add(_pool[_drawIndices[k]]);
            }
        }

        /// <summary>Spend one reroll point and re-draw the offer (same generation as <see cref="GenerateOffer"/>).
        /// Does NOT touch currency. Returns false and changes nothing when no reroll points remain.</summary>
        public bool TryReroll()
        {
            if (_reroll == null || !_reroll.TrySpend()) return false;
            GenerateOffer();
            return true;
        }

        /// <summary>True if the item can be bought right now (stackable rule + affordability). The UI
        /// uses this to enable/grey buttons; <see cref="TryPurchase"/> re-checks before spending.</summary>
        public bool CanPurchase(ConsumableDefinitionSO item)
        {
            if (item == null) return false;
            if (_purchasedThisOffer.Contains(item)) return false; // Task 17: at most once per offer
            if (!item.Stackable && _inventory.Owns(item)) return false;
            return _currency.CurrentCurrency >= item.Price;
        }

        /// <summary>True if this offered item was already bought this offer (UI shows it as Purchased).
        /// Cleared on the next reroll or shop visit via <see cref="GenerateOffer"/> (Task 17).</summary>
        public bool WasPurchasedThisOffer(ConsumableDefinitionSO item) =>
            item != null && _purchasedThisOffer.Contains(item);

        /// <summary>
        /// Validate the stackable rule, then attempt to spend via <see cref="CurrencyManager.TrySpend"/>.
        /// Currency is only deducted by TrySpend, which never lets the total go negative. On success the
        /// purchase is recorded and the effect applied through the existing systems (per §2). Returns
        /// false and changes nothing on a blocked/unaffordable purchase.
        /// </summary>
        public bool TryPurchase(ConsumableDefinitionSO item)
        {
            if (item == null) return false;

            // Task 17: each offered item is buyable at most once per offer (blocks draining currency into
            // one stacked effect). Reset when a new offer is rolled (reroll or next visit).
            if (_purchasedThisOffer.Contains(item)) return false;

            // Non-stackable items can only be owned once per run.
            if (!item.Stackable && _inventory.Owns(item)) return false;

            // TrySpend is the single source of truth for the spend; it validates funds first.
            if (!_currency.TrySpend(item.Price)) return false;

            _purchasedThisOffer.Add(item);
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

                case ConsumableEffectType.GainRerollPoints:
                    // Task 09: Reroll Potion — adds reroll points via the same purchase→effect flow as any
                    // other consumable (no special-cased path). Value carries the per-tier amount (+1/2/3).
                    if (_reroll != null) _reroll.Add(Mathf.RoundToInt(item.EffectValue));
                    else Debug.LogWarning("[ShopController] GainRerollPoints purchased but no RerollManager is wired.");
                    break;
            }
        }
    }
}
