using System;
using UnityEngine;
using Wavekeep.Core;
using Wavekeep.Core.Events;
using Wavekeep.Data;
using Wavekeep.Economy;

namespace Wavekeep.Gear
{
    /// <summary>
    /// Rolls gear drops on enemy death (Task 13; reworked Task 68). A non-static plain C# class owned by
    /// <see cref="GameSession"/>; subscribes to <see cref="EnemyKilledEvent"/> and runs AFTER the
    /// currency/XP reward handlers (which subscribe in their own constructors), so loot is purely an
    /// additional consumer of the same kill event — it never touches the death/currency/XP/pool path.
    ///
    /// Task 68: a drop is now a freshly GENERATED <see cref="GearInstance"/> rather than a reference to a
    /// pre-made finished item. This service stays thin — it reads the kill event's already-resolved
    /// <see cref="LootTableSO"/> (the enemy's own for regulars, the wave's boss table for bosses), hands it to
    /// <see cref="GearGenerator"/> (drop gate → slot pick → Luck-weighted rarity roll → implicit + affixes),
    /// grants the result via <see cref="GearManager.Grant(GearInstance)"/> — which persists (Task 12/67) so
    /// drops survive a restart — and publishes a <see cref="GearDroppedEvent"/> with the generated instance.
    ///
    /// The Luck weighting lives in the generator and reuses the SAME shared <c>TierWeighting</c> step the shop
    /// uses (at the weaker loot strength); the boss-exclusive rarity lock is still purely data-driven (which
    /// rarities a table lists). There is no second weighting model.
    /// </summary>
    public sealed class LootService : IDisposable
    {
        private readonly EventBus _events;
        private readonly GearManager _gear;
        private readonly GearGenerator _generator;

        public LootService(EventBus events, GearManager gear, LuckState luck, GearAffixCountConfigSO affixConfig)
        {
            _events = events;
            _gear = gear;
            _generator = new GearGenerator(affixConfig, luck);
            _events.Subscribe<EnemyKilledEvent>(OnEnemyKilled);
        }

        private void OnEnemyKilled(EnemyKilledEvent evt)
        {
            var instance = _generator.TryGenerate(evt.LootTable);
            if (instance == null) return;

            _gear.Grant(instance); // → GearInventory.Add + save (Task 67)
            // Task 69: forward the enemy's death position so the visual loot-drop layer can place a marker there.
            _events.Publish(new GearDroppedEvent(instance, evt.DeathPosition));
            Debug.Log($"[LootService] Dropped [{instance.Rarity}] {instance.ItemName} " +
                      $"(slot {instance.Slot}, {instance.Affixes.Count} affix(es)).");
        }

        public void Dispose()
        {
            _events.Unsubscribe<EnemyKilledEvent>(OnEnemyKilled);
        }
    }
}
