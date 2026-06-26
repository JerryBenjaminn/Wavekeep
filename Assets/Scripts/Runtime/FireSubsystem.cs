using System.Collections.Generic;
using UnityEngine;
using Wavekeep.Abilities;

namespace Wavekeep.Runtime
{
    /// <summary>
    /// Task 48 (Pyromancer): the run-scoped reaction service for two Burn-driven Fireball lines —
    /// <b>Spreading Flame</b> (a Burning target's DEATH spreads a fresh Burn to nearby enemies) and
    /// <b>Combustion</b> (a Burn that expires NATURALLY may detonate in an AoE). It is a plain C# object owned
    /// per-run by <see cref="HeroRuntime"/> (no static singleton), mirroring <see cref="GroundZoneManager"/>.
    ///
    /// Like <see cref="GroundZone"/>'s "death inside" detection, it works by POLLING rather than enemy-side
    /// callbacks: each tick it compares the set of enemies that were Burning last tick against their state now.
    /// An enemy that was burning and is now dead → it died burning (Spreading Flame); one that is now alive but
    /// no longer burning → its Burn ran out (Combustion). This keeps EnemyRuntime free of any ability/upgrade
    /// coupling, and is reliable because <c>WaveSpawner</c> creates a NEW <see cref="EnemyRuntime"/> per spawn,
    /// so a dead enemy's <c>IsAlive</c>/<c>IsBurning</c> stay false.
    ///
    /// All behaviour is driven by held upgrade data read from the <see cref="UpgradeInventory"/> each tick
    /// (never a hero identity) — so it is inert for a run that holds no Spreading Flame / Combustion upgrade.
    /// </summary>
    public sealed class FireSubsystem
    {
        // The Burn potency captured WHILE an enemy was burning, so Spreading Flame can copy "the original Burn
        // potency" even though the dying enemy's own burn state is already gone by the time we react.
        private struct BurnSnapshot
        {
            public float PerTick;
            public float Duration;
        }

        private Dictionary<EnemyRuntime, BurnSnapshot> _burningLastTick = new Dictionary<EnemyRuntime, BurnSnapshot>();
        private Dictionary<EnemyRuntime, BurnSnapshot> _burningThisTick = new Dictionary<EnemyRuntime, BurnSnapshot>();

        // Reusable target buffer for spread/detonation searches (no per-tick allocation).
        private readonly List<EnemyRuntime> _buffer = new List<EnemyRuntime>();

        public void Tick(float deltaTime, IReadOnlyList<EnemyRuntime> enemies,
            UpgradeInventory upgrades, float basicDamage, IAbilityFeedback feedback = null)
        {
            int spreadTargets = 0;
            float spreadRange = 0f, spreadPotency = 0f;
            float combChance = 0f, combRadius = 0f, combFraction = 0f;
            bool hasSpread = upgrades != null &&
                upgrades.TryGetSpreadingFlame(out spreadTargets, out spreadRange, out spreadPotency);
            bool hasCombustion = upgrades != null &&
                upgrades.TryGetCombustion(out combChance, out combRadius, out combFraction);

            // React to every enemy that WAS burning last tick, based on its state now.
            foreach (var kv in _burningLastTick)
            {
                var enemy = kv.Key;
                if (enemy == null) continue;

                if (!enemy.IsAlive)
                {
                    // Died while burning → Spreading Flame (copies the original Burn's potency × spread fraction).
                    if (hasSpread)
                        SpreadBurn(enemy, enemies, spreadTargets, spreadRange,
                            kv.Value.PerTick * spreadPotency, kv.Value.Duration, feedback);
                }
                else if (!enemy.IsBurning)
                {
                    // Burn lapsed naturally (still alive, no longer burning) → Combustion roll.
                    if (hasCombustion && Random.value < combChance)
                        Detonate(enemy.Transform.position, enemies, combRadius, combFraction * basicDamage, feedback);
                }
            }

            // Rebuild the currently-burning snapshot for next tick's comparison.
            _burningThisTick.Clear();
            if (enemies != null)
            {
                for (int i = 0; i < enemies.Count; i++)
                {
                    var e = enemies[i];
                    if (e == null || !e.IsAlive || !e.IsBurning) continue;
                    _burningThisTick[e] = new BurnSnapshot { PerTick = e.BurnPerTick, Duration = e.BurnDuration };
                }
            }

            // Swap buffers (this tick becomes last tick) without reallocating either dictionary.
            var swap = _burningLastTick;
            _burningLastTick = _burningThisTick;
            _burningThisTick = swap;
        }

        // Spreading Flame: apply a fresh Burn to the nearest `targets` alive OTHER enemies within `range` of the
        // dying enemy. A plain single-instance Burn (maxStacks 0) — the spec calls it a fresh instance.
        private void SpreadBurn(EnemyRuntime origin, IReadOnlyList<EnemyRuntime> enemies,
            int targets, float range, float perTick, float duration, IAbilityFeedback feedback)
        {
            if (targets <= 0 || range <= 0f || perTick <= 0f || duration <= 0f || enemies == null) return;

            _buffer.Clear();
            float rangeSqr = range * range;
            var originPos = origin.Transform.position;
            for (int i = 0; i < enemies.Count; i++)
            {
                var e = enemies[i];
                if (e == null || e == origin || !e.IsAlive) continue;
                if ((e.Transform.position - originPos).sqrMagnitude <= rangeSqr) _buffer.Add(e);
            }
            if (_buffer.Count == 0) return;

            if (_buffer.Count > targets)
            {
                _buffer.Sort((a, b) =>
                    (a.Transform.position - originPos).sqrMagnitude.CompareTo(
                    (b.Transform.position - originPos).sqrMagnitude));
                _buffer.RemoveRange(targets, _buffer.Count - targets);
            }

            var originPosVfx = origin.Transform.position;
            for (int i = 0; i < _buffer.Count; i++)
            {
                _buffer[i].ApplyBurn(perTick, duration, maxStacks: 0, perStackBonus: 0f);
                // Task 51: a brief travelling ember from the dying enemy to each newly-ignited target.
                feedback?.OnSpreadingFlame(originPosVfx, _buffer[i].Transform.position);
            }

            Debug.Log($"[FireSubsystem] Spreading Flame: Burn spread to {_buffer.Count} enemy(ies).");
            _buffer.Clear();
        }

        // Combustion: instant unmitigated AoE damage (consistent with GroundZone pulses) to every alive enemy
        // within `radius` of the detonation point.
        private void Detonate(Vector3 center, IReadOnlyList<EnemyRuntime> enemies, float radius, float damage,
            IAbilityFeedback feedback)
        {
            if (radius <= 0f || damage <= 0f || enemies == null) return;

            // Task 51: a distinct Combustion fire-burst at the detonation point, sized to the ACTUAL tier radius.
            feedback?.OnCombustion(center, radius);

            _buffer.Clear();
            float radiusSqr = radius * radius;
            for (int i = 0; i < enemies.Count; i++)
            {
                var e = enemies[i];
                if (e == null || !e.IsAlive) continue;
                if ((e.Transform.position - center).sqrMagnitude <= radiusSqr) _buffer.Add(e);
            }

            for (int i = 0; i < _buffer.Count; i++)
                if (_buffer[i].IsAlive) _buffer[i].TakeDamage(damage);

            if (_buffer.Count > 0)
                Debug.Log($"[FireSubsystem] Combustion detonated for {damage:0.#} on {_buffer.Count} enemy(ies).");
            _buffer.Clear();
        }

        public void Clear()
        {
            _burningLastTick.Clear();
            _burningThisTick.Clear();
        }
    }
}
