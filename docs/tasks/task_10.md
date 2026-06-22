# Task 10 — Wave Scaling & Boss Waves

> Read `CLAUDE.md` in full, and review the Task 01–09 implementations before starting. This task extends the existing Task 02 `WaveConfigSO`/`DifficultyTierSO` system with progressive difficulty scaling every 5 waves and boss enemies appearing alongside normal waves every 10 waves.

## Goal

By the end of this task: every 5th wave, regular enemy stats step up noticeably (not just Task 02's existing per-wave/per-tier multiplier, but a clear progressive milestone); every 10th wave, a boss enemy spawns alongside the normal wave's enemies (not replacing them); the boss has meaningfully more HP/damage than regular enemies and is visually distinguishable (placeholder-tier, e.g. a larger/differently-tinted capsule).

## Scope

### 1. Progressive Difficulty Milestones
- Extend `WaveConfigSO`/`DifficultyTierSO` (Task 02) with a milestone-scaling rule: every 5th wave (5, 10, 15, 20...) applies an additional stat multiplier on top of whatever per-wave/per-tier multiplier already exists. Implement this as a formula/curve (e.g. `milestoneMultiplier = 1 + (waveNumber / 5) * milestoneStep`, with `milestoneStep` as a configurable value), not a manually-authored multiplier on every 5th `WaveConfigSO` — the system should generalize to wave 50, 100, etc. without manually authoring that many assets.
- Confirm this layers correctly with Task 02's existing per-wave `statMultiplier` and `DifficultyTierSO`'s global multiplier (multiplicative stacking, document the exact order of operations).
- Apply this to `EnemyRuntime`'s stat computation at spawn time (same pattern as Task 02's existing multiplier application — never mutate `EnemyDefinitionSO`).

### 2. Boss Enemy Definition
- Add `BossDefinitionSO` (or extend `EnemyDefinitionSO` with an `isBoss` flag plus boss-specific fields — your call, document the choice and reasoning) with notably higher `maxHealth`/`contactDamage` than regular enemies, and a distinct `prefab` reference (reuse the capsule convention but scaled up and/or differently tinted, per the project's placeholder-first approach).
- Boss should use the same `EnemyRuntime`/movement/wall-attack logic as regular enemies (no special-cased boss behavior code) — it's just a bigger, tougher `EnemyDefinitionSO` instance. If you find a compelling reason a boss needs unique behavior beyond stats (e.g. a special attack), flag it as a future task rather than building it now — out of scope for this task.

### 3. Boss Wave Integration
- Every 10th wave (10, 20, 30...): `WaveSpawner` spawns one boss (or a configurable count, default 1) **alongside** that wave's normal enemy composition — not replacing it. Add a `bossSpawn` reference (optional, nullable) to `WaveConfigSO` or a separate boss-wave-override mechanism — document your approach, but prefer extending the existing `WaveConfigSO` data shape over introducing a parallel wave-definition system.
- Boss spawns through the same `EnemyPoolManager` pipeline as regular enemies (no `Instantiate` exception for bosses).
- `WaveCompletedEvent` for a boss wave must correctly account for the boss as part of "all enemies resolved" (per Task 02's existing rule) — a boss wave isn't complete until the boss is also dead, not just the regular enemies.

### 4. Test Content
- Author enough `WaveConfigSO`/`DifficultyTierSO` content to demonstrate waves 1 through at least 12 (covering one 5-wave milestone at wave 5, and one boss wave at wave 10, plus wave 11-12 to confirm normal flow resumes after a boss wave) — extend the existing Task 02 test `DifficultyTierSO` rather than creating a disconnected new one.

## Out of Scope (do not implement)
- Unique boss attack patterns/abilities beyond stat differences (flag as a future task if compelling)
- Multiple simultaneous bosses with different types (one boss type, repeated, is enough for this task)
- Visual telegraph/warning UI before a boss wave starts (a simple text cue like "Boss Wave!" in the existing wave-info UI is fine if trivial; full telegraph polish is not required)
- Tier-weighted shop offers tied to wave number (Task 09 documented this as a follow-up — only pick it up now if genuinely trivial given this task's new milestone data; otherwise leave it for later)

## Acceptance Criteria
- [ ] Every 5th wave applies a clear additional milestone stat multiplier on top of existing per-wave/tier multipliers, generalized via formula rather than per-wave authored values
- [ ] Multiplier stacking order (per-wave × tier × milestone) is documented and behaves predictably (verify with a debug log of final computed stats at a few sample waves)
- [ ] `BossDefinitionSO` (or equivalent) exists with notably higher stats and a visually distinct placeholder appearance
- [ ] Every 10th wave spawns a boss alongside (not instead of) that wave's normal enemies, via the existing `EnemyPoolManager`
- [ ] Boss uses the same `EnemyRuntime` movement/attack/death logic as regular enemies — no special-cased boss code path
- [ ] `WaveCompletedEvent` for a boss wave correctly waits for the boss to die, not just the regular enemies
- [ ] Test content demonstrates waves 1–12+ with correct scaling and one boss wave
- [ ] No SO asset is mutated at runtime
- [ ] No static `Instance` patterns introduced
- [ ] Full playtest: Play → progress through wave 5 (visible stat jump) → reach wave 10 (boss appears alongside regular enemies, noticeably tougher) → boss dies → wave completes → wave 11 starts normally

## Reviewer Notes
Flag as blocking if:
- Milestone scaling is hardcoded per-wave (e.g. a switch statement checking `if (wave == 5)`) instead of a generalized formula
- Boss spawning bypasses `EnemyPoolManager` or introduces `Instantiate`/`Destroy` for bosses specifically
- Boss has hardcoded special-case behavior in `EnemyRuntime` or `WaveSpawner` rather than being driven purely by its `EnemyDefinitionSO`/`BossDefinitionSO` data
- `WaveCompletedEvent` fires while the boss is still alive
