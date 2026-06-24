using System;
using UnityEngine;
using Wavekeep.Core;
using Wavekeep.Core.Events;
using Wavekeep.Data;
using Wavekeep.Economy;

namespace Wavekeep.Gear
{
    /// <summary>
    /// Rolls gear drops on enemy death (Task 13). A non-static plain C# class owned by
    /// <see cref="GameSession"/>; subscribes to <see cref="EnemyKilledEvent"/> and runs AFTER the
    /// currency/XP reward handlers (which subscribe in their own constructors), so loot is purely an
    /// additional consumer of the same kill event — it never touches the death/currency/XP/pool path.
    ///
    /// Resolution: read the kill event's already-resolved <see cref="LootTableSO"/> (the enemy's own for
    /// regulars, the wave's boss table for bosses); roll its overall drop chance; on a hit, weighted-pick
    /// one entry and grant it via <see cref="GearManager.Grant"/> — which routes through
    /// <c>GearInventory.Add</c> AND persists (Task 12), so drops survive a restart. Then publish a
    /// <see cref="GearDroppedEvent"/> for the minimal pickup notification. Rarity restriction is entirely
    /// data-driven (which items a table lists) — there is no code-level rarity check here.
    ///
    /// Task 24: drop-tier odds are reweighted by the player's current Luck via the SAME shared
    /// <c>TierWeighting</c> step the shop uses, at a deliberately weaker strength (the loot multiplier).
    /// This ONLY reshuffles odds among the entries the table already lists — it never adds an item, so
    /// the boss-exclusive rarity lock (enforced purely by table contents) is untouched.
    /// </summary>
    public sealed class LootService : IDisposable
    {
        private readonly EventBus _events;
        private readonly GearManager _gear;
        private readonly LuckState _luck;

        public LootService(EventBus events, GearManager gear, LuckState luck)
        {
            _events = events;
            _gear = gear;
            _luck = luck;
            _events.Subscribe<EnemyKilledEvent>(OnEnemyKilled);
        }

        private void OnEnemyKilled(EnemyKilledEvent evt)
        {
            var item = Roll(evt.LootTable);
            if (item == null) return;

            _gear.Grant(item); // → GearInventory.Add + save (Task 12)
            _events.Publish(new GearDroppedEvent(item));
            Debug.Log($"[LootService] Dropped [{item.Rarity}] {item.ItemName}.");
        }

        // Overall drop-chance gate, then Luck-weighted-random selection over the table's entries.
        private LootItemSO Roll(LootTableSO table)
        {
            if (table == null) return null;
            if (UnityEngine.Random.value >= table.DropChance) return null;

            var entries = table.Entries;
            if (entries.Count == 0) return null;

            // Task 24: rarity span of THIS table's eligible entries, so the weighting normalises across only
            // the tiers actually droppable here (the boss-exclusive lock = which entries the table lists).
            FindRarityRange(entries, out int minRarity, out int maxRarity, out bool any);
            if (!any) return null;
            int span = maxRarity - minRarity;

            // Build the Luck-adjusted weights, summing as we go (floats — multipliers are fractional).
            float totalWeight = 0f;
            for (int i = 0; i < entries.Count; i++)
            {
                totalWeight += AdjustedWeight(entries[i], minRarity, span);
            }
            if (totalWeight <= 0f) return null;

            float roll = UnityEngine.Random.value * totalWeight;
            for (int i = 0; i < entries.Count; i++)
            {
                float w = AdjustedWeight(entries[i], minRarity, span);
                if (w <= 0f) continue;

                roll -= w;
                if (roll < 0f) return entries[i].Item;
            }
            return null;
        }

        // Base entry weight × the (weaker) loot tier multiplier for this entry's rarity within the table.
        private float AdjustedWeight(LootEntry entry, int minRarity, int span)
        {
            if (entry == null || entry.Item == null || entry.Weight <= 0) return 0f;
            // normTier 0 = the table's lowest listed rarity (never reduced); 1 = its highest.
            float normTier = span > 0 ? (float)((int)entry.Item.Rarity - minRarity) / span : 0f;
            float multiplier = _luck != null ? _luck.LootTierMultiplier(normTier) : 1f;
            return entry.Weight * multiplier;
        }

        private static void FindRarityRange(
            System.Collections.Generic.IReadOnlyList<LootEntry> entries,
            out int minRarity, out int maxRarity, out bool any)
        {
            minRarity = int.MaxValue;
            maxRarity = int.MinValue;
            any = false;
            for (int i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (entry == null || entry.Item == null || entry.Weight <= 0) continue;
                int r = (int)entry.Item.Rarity;
                if (r < minRarity) minRarity = r;
                if (r > maxRarity) maxRarity = r;
                any = true;
            }
        }

        public void Dispose()
        {
            _events.Unsubscribe<EnemyKilledEvent>(OnEnemyKilled);
        }
    }
}
