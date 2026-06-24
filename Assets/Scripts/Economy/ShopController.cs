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

        // Task 24: Luck-driven tier weighting for the offer draw. Both optional — when either is null the
        // draw falls back to a uniform distinct subset (the Task 09 behaviour), so older scenes still work.
        private readonly LuckState _luck;
        private readonly TierWeightingConfigSO _weightingConfig;
        private static readonly int ConsumableTierCount = System.Enum.GetValues(typeof(ConsumableTier)).Length;

        private readonly List<ConsumableDefinitionSO> _offer = new List<ConsumableDefinitionSO>();
        private readonly List<int> _drawIndices = new List<int>(); // scratch for the distinct draw
        private readonly List<float> _drawWeights = new List<float>(); // scratch parallel weights (Task 24)

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
            int offerSize,
            LuckState luck = null,
            TierWeightingConfigSO weightingConfig = null)
        {
            _currency = currency;
            _inventory = inventory;
            _wall = wall;
            _reroll = reroll;
            _pool = pool;
            _offerSize = Mathf.Max(1, offerSize);
            _luck = luck;
            _weightingConfig = weightingConfig;
        }

        /// <summary>The items currently offered this visit (read-only). Re-populated by
        /// <see cref="GenerateOffer"/> / <see cref="TryReroll"/>.</summary>
        public IReadOnlyList<ConsumableDefinitionSO> CurrentOffer => _offer;

        /// <summary>Reroll points available this run (separate resource from currency).</summary>
        public int RerollPoints => _reroll != null ? _reroll.CurrentPoints : 0;

        /// <summary>True while a reroll can be spent (used to enable/disable the reroll button).</summary>
        public bool CanReroll => _reroll != null && _reroll.CanReroll;

        /// <summary>Draw a fresh offer (FREE — does not touch reroll points). Called on each shop open.
        ///
        /// Task 24: the draw is now TIER-WEIGHTED — each pool item's draw weight is its configured base
        /// tier odds × the shared <see cref="TierWeighting"/> multiplier for current Luck + wave progress,
        /// so higher tiers grow progressively more likely as Luck/wave rise (Luck the stronger factor).
        /// The lowest tier's weight is never reduced, so it always remains reachable (no zero odds). When
        /// no Luck/config is wired the weights collapse to the base odds (still tier-weighted, but static),
        /// and a missing config means uniform — i.e. the original Task 09 behaviour.</summary>
        public void GenerateOffer()
        {
            _offer.Clear();
            // A freshly rolled offer has nothing purchased yet (Task 17) — this single reset covers both
            // the per-visit open AND a reroll (which calls through here), so no redundant reset is needed.
            _purchasedThisOffer.Clear();
            if (_pool == null) return;

            // Collect valid pool indices + their Luck-adjusted draw weights (parallel lists).
            _drawIndices.Clear();
            _drawWeights.Clear();
            for (int i = 0; i < _pool.Count; i++)
            {
                if (_pool[i] == null) continue;
                _drawIndices.Add(i);
                _drawWeights.Add(OfferWeight(_pool[i]));
            }

            // Weighted sampling WITHOUT replacement: pick by weight, then swap the chosen entry out of the
            // remaining range (mirrors the old partial Fisher–Yates, but weighted instead of uniform).
            int want = Mathf.Min(_offerSize, _drawIndices.Count);
            for (int k = 0; k < want; k++)
            {
                int picked = PickWeighted(k);
                (_drawIndices[k], _drawIndices[picked]) = (_drawIndices[picked], _drawIndices[k]);
                (_drawWeights[k], _drawWeights[picked]) = (_drawWeights[picked], _drawWeights[k]);
                _offer.Add(_pool[_drawIndices[k]]);
            }
        }

        // Draw weight for one item: base tier odds × Luck/wave tier multiplier. Always > 0 so every item
        // stays reachable. Falls back to uniform (1) when no weighting config is wired.
        private float OfferWeight(ConsumableDefinitionSO item)
        {
            if (_weightingConfig == null) return 1f;

            int ordinal = (int)item.Tier;
            float baseWeight = _weightingConfig.ShopBaseTierWeight(ordinal);
            float normTier = ConsumableTierCount > 1 ? (float)ordinal / (ConsumableTierCount - 1) : 0f;
            float multiplier = _luck != null ? _luck.ShopTierMultiplier(normTier) : 1f;
            return baseWeight * multiplier;
        }

        // Weighted pick over the still-available range [start.._drawIndices.Count). Returns the chosen
        // index into the scratch lists. Degrades to a uniform pick if all remaining weights are zero.
        private int PickWeighted(int start)
        {
            float total = 0f;
            for (int i = start; i < _drawWeights.Count; i++) total += Mathf.Max(0f, _drawWeights[i]);

            if (total <= 0f) return Random.Range(start, _drawIndices.Count);

            float roll = Random.value * total;
            for (int i = start; i < _drawWeights.Count; i++)
            {
                roll -= Mathf.Max(0f, _drawWeights[i]);
                if (roll < 0f) return i;
            }
            return _drawIndices.Count - 1;
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
                // Task 23: the new potion effects are all ongoing ability modifiers too — they take the
                // SAME AddEffect path and are read by AbilityRuntime's existing pipeline (crit roll,
                // frost/zone resolvers, role-aware damage). No new purchase or calculation path.
                case ConsumableEffectType.CritChanceBoost:
                case ConsumableEffectType.CritDamageBoost:
                case ConsumableEffectType.FrostPotency:
                case ConsumableEffectType.ElementalLightning:
                case ConsumableEffectType.UltimateDurationBoost:
                case ConsumableEffectType.BasicDamageBoost:
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

                case ConsumableEffectType.LuckBoost:
                    // Task 24: Luck Potion — adds an in-run, non-persistent Luck bonus through the SAME
                    // purchase→effect flow as any other consumable. LuckState clamps the total to 0–100 and
                    // resets the potion portion at run end. Not an AbilityRuntime modifier (Luck is non-combat).
                    if (_luck != null) _luck.AddPotionBonus(item.EffectValue);
                    else Debug.LogWarning("[ShopController] LuckBoost purchased but no LuckState is wired.");
                    break;
            }
        }
    }
}
