# Task 02 — Enemy Definitions, Wave Spawner & Difficulty Scaling

> Read `CLAUDE.md` in full, and review the Task 01 implementation before starting. This task builds the first real gameplay loop piece: enemies spawning in waves and moving toward the defended point. No hero abilities, no shop, no XP/currency rewards yet — just enemies existing, spawning, and approaching.

## Goal

Implement `EnemyDefinitionSO`, `WaveConfigSO`, `DifficultyTierSO` (fleshed out from Task 01's stubs), the `WaveSpawner`, and `EnemyRuntime` movement — all running on top of Task 01's `EnemyPoolManager`. By the end of this task, a placeholder capsule enemy should spawn in waves on an open arena and walk toward a defended point, recycling through the pool on death/despawn.

## Scope

### 1. Arena Setup (Single-Direction Approach)
- **Correction from initial implementation:** enemies do NOT spawn from a perimeter/ring around the player. They spawn from **one direction only** — the far side of the arena, opposite the player/hero — and advance toward the player. The arena is open *width-wise* across that single approach side, not open in all directions.
- Add 3–6 `Transform` spawn markers spaced across the far edge of the arena (a horizontal line/arc facing the player), not around a full perimeter.
- **Wall, not abstract defended point:** place a wall object between the spawn side and the player/hero. Enemies path toward the wall and stop at it (not toward the hero, not past the wall). The wall has HP (see §2 below) — when an enemy reaches the wall, it deals its `contactDamage` to the wall's HP on an interval (basic attack-the-wall behavior), not a one-shot "reached point, despawn" event.

### 2. Wall HP & Lose Condition
- Add a simple `WallRuntime` (or similar) component holding current/max HP, sourced from a `maxWallHP` value (placeholder constant or simple SO field — your call, document the choice).
- Enemies that reach the wall attack it repeatedly (using their `contactDamage` and a basic attack interval — reuse the enemy's existing stats, no new SO fields needed beyond what's already on `EnemyDefinitionSO`) rather than instantly despawning.
- When wall HP reaches 0, publish a `RunEndedEvent` with a "defeat"/"wall destroyed" result and stop spawning further waves.
- Replace the originally-planned `EnemyReachedDefendedPointEvent` (one-shot arrival event) with this attack-the-wall behavior — an enemy reaching the wall starts attacking, it doesn't instantly resolve and despawn. The enemy is removed from the pool only when killed (`TakeDamage` → 0 HP), not when it reaches the wall.

### 2. `EnemyDefinitionSO` (flesh out from Task 01 stub)
Fields:
- `enemyName`, `prefab` (reference to the pooled capsule prefab)
- `maxHealth`, `moveSpeed`, `contactDamage` (damage dealt to defended point on arrival, even though defended-point-health logic itself is out of scope this task — just store the value)
- `currencyReward`, `xpReward` (store the values now; CurrencyManager/XPManager consuming them is Task 03 — do not implement reward distribution yet, just make sure the data exists on the enemy so Task 03 can read it)

### 3. `EnemyRuntime` (Scripts/Runtime)
- Wraps `EnemyDefinitionSO` + current runtime state (current health, pooled GameObject reference).
- Movement: simple direct movement toward the wall (straight-line `Vector3.MoveTowards` or `NavMeshAgent` — your choice; arena is open width-wise with no obstacles, so no pathfinding/obstacle avoidance needed).
- On reaching the wall: stop moving, switch to an attack state, and deal `contactDamage` to `WallRuntime` on a repeating interval until the enemy dies or the wall is destroyed.
- On taking lethal damage (no damage-dealing system exists yet this task, so just expose a `TakeDamage(float amount)` method and a `Die()` path that publishes `EnemyKilledEvent` — Task 03 will be what actually calls `TakeDamage`, e.g. from a hero ability test stub. For Task 02 acceptance, it's enough that the method exists and correctly transitions to `Die()` → pool release when health reaches 0; you may add a temporary debug trigger, e.g. a key press in a test scene, to manually verify this path works, including against an enemy that's already in its attack-the-wall state).

### 4. `WaveConfigSO` (flesh out from Task 01 stub)
Fields:
- `waveNumber` (or index)
- list of `EnemySpawnEntry`: `{ EnemyDefinitionSO enemyType, int count, float spawnInterval }`
- optional `statMultiplier` override for this specific wave (defaults to 1.0, layered on top of the DifficultyTier's multiplier)

### 5. `DifficultyTierSO` (flesh out from Task 01 stub)
Fields:
- `tierName` (e.g. "Easy", "Normal", "Hard")
- ordered list of `WaveConfigSO` references
- global `statMultiplier` (applied to all enemies spawned under this tier, multiplied with any per-wave override)

### 6. `WaveSpawner` (Scripts/Core or a new Scripts/Waves folder — your call, document the choice)
- Reads the active `DifficultyTierSO`, processes its `WaveConfigSO` list sequentially.
- For each wave: spawns the configured enemies at `spawnInterval` timing from the perimeter spawn markers (round-robin or random marker selection — either is fine for MVP), applying the tier's and wave's stat multipliers to the spawned `EnemyRuntime`'s stats.
- Publishes `WaveStartedEvent` when a wave begins and `WaveCompletedEvent` when all enemies from that wave have been spawned AND killed/resolved (i.e. wave isn't "completed" just because spawning finished — track active enemy count from that wave).
- After the last configured wave, publish `RunEndedEvent` with a simple "victory"/"waves cleared" result (full `RunResult` data structure can stay minimal — just enough to not be empty).
- All enemy creation goes through `EnemyPoolManager.Get()` — no `Instantiate` calls for enemies in this system.

### 7. Test Setup
- Author one `DifficultyTierSO` asset with 2–3 simple `WaveConfigSO` entries (e.g. wave 1: 5 enemies, wave 2: 8 enemies, wave 3: 10 enemies) using the placeholder capsule `EnemyDefinitionSO` from Task 01.
- Wire it into the Task 01 test scene so pressing Play spawns waves visibly walking toward the defended point.

## Out of Scope (do not implement)
- Hero abilities or any damage-dealing system (TakeDamage exists but nothing calls it yet except a manual debug trigger)
- Currency/XP actually being granted (data exists on EnemyDefinitionSO, but no manager consumes it yet)
- Wall repair/upgrade mechanics, victory screen polish, or any UI beyond a basic wall HP readout if trivial
- Shop, level-up UI, ability upgrades
- Enemy variety beyond the placeholder capsule (visual variety can wait)
- Pathfinding around obstacles (arena is open width-wise, not needed yet)

## Acceptance Criteria
- [ ] `EnemyDefinitionSO`, `WaveConfigSO`, `DifficultyTierSO` fully fleshed out with fields listed above
- [ ] Enemies spawn exclusively from the far-side spawn markers (single direction), never from a perimeter/ring around the player
- [ ] `EnemyRuntime` moves enemies from far-side spawn markers toward the wall, then stops and attacks the wall on an interval rather than despawning on arrival
- [ ] `WallRuntime` tracks wall HP, takes damage from attacking enemies, and triggers `RunEndedEvent` ("defeat") when HP reaches 0
- [ ] Enemies spawn exclusively via `EnemyPoolManager.Get()`, released via `Release()` only on death (TakeDamage → 0 HP) — not on reaching the wall
- [ ] `WaveSpawner` correctly sequences multiple waves from a `DifficultyTierSO`, respecting per-enemy spawn intervals
- [ ] `WaveStartedEvent`, `WaveCompletedEvent`, `EnemyKilledEvent`, `RunEndedEvent` are all published at the correct points via the Task 01 `EventBus`
- [ ] Stat multipliers (tier-level and wave-level) are correctly applied to spawned enemy stats — verify with a debug log or visible health difference between waves
- [ ] A manual debug trigger proves `TakeDamage` → `Die()` → pool release works correctly, including for an enemy currently attacking the wall
- [ ] Test scene demonstrates the full sequence: Play → waves spawn from one direction → enemies reach and attack the wall → either wall is destroyed (defeat) or last wave clears (victory) → `RunEndedEvent` fires with the correct result
- [ ] No SO asset is mutated at runtime (stat multipliers must be applied to the `EnemyRuntime` instance's working stats, never written back into `EnemyDefinitionSO`)

## Reviewer Notes
Flag as blocking if:
- Enemies spawn from anywhere other than the single far-side approach direction
- Any enemy spawn/despawn bypasses `EnemyPoolManager`
- `EnemyDefinitionSO` (or any other SO) has its fields written to at runtime instead of values being copied into `EnemyRuntime`
- `WaveCompletedEvent` fires based on spawn-finished rather than all-enemies-resolved
- An enemy is released back to the pool upon reaching the wall instead of continuing to attack it
