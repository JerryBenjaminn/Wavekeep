using System;
using UnityEngine;
using Wavekeep.Core;
using Wavekeep.Core.Events;
using Wavekeep.Data;

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
    /// </summary>
    public sealed class LootService : IDisposable
    {
        private readonly EventBus _events;
        private readonly GearManager _gear;

        public LootService(EventBus events, GearManager gear)
        {
            _events = events;
            _gear = gear;
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

        // Overall drop-chance gate, then weighted-random selection over the table's entries.
        private static LootItemSO Roll(LootTableSO table)
        {
            if (table == null) return null;
            if (UnityEngine.Random.value >= table.DropChance) return null;

            int totalWeight = table.TotalWeight;
            if (totalWeight <= 0) return null;

            int roll = UnityEngine.Random.Range(0, totalWeight);
            var entries = table.Entries;
            for (int i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (entry == null || entry.Item == null || entry.Weight <= 0) continue;

                roll -= entry.Weight;
                if (roll < 0) return entry.Item;
            }
            return null;
        }

        public void Dispose()
        {
            _events.Unsubscribe<EnemyKilledEvent>(OnEnemyKilled);
        }
    }
}
