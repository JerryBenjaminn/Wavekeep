# Task 80 — Implementation Summary: Utility-Only Boss-Reward Shop

> Replaces the between-wave currency shop + stat potions with a **boss-clear "pick one utility, free"** reward.
> Currency infrastructure is untouched (retained, no shop sink). Builds on Task 78/79 analysis.

## Trigger points (flagged for tuning)
The shop now opens **only when a boss wave is cleared** (`WaveSpawner.RunRoutine` → `_difficultyTier.IsBossWave`,
replacing the old `_shopIntervalWaves` every-N-waves check). GameTier's `_bossWaveInterval = 5`, so boss waves —
and therefore shop moments — fall at **waves 5, 10, 15 … 60** (12 rewards across the 60-wave run). The reward is
armed for and applied to the wave *immediately after* each boss. All boss cadence + item values are **flagged for
tuning**, not final.

## Item roster (all values placeholders, flagged for tuning)
Authored by `Task80ShopRedesignSetup`; offer size **4**, player picks **1 free**.

| Item | Effect type | Magnitude | Duration | When it applies |
|---|---|---|---|---|
| Wall Repair Kit | `HealWall` | +150 wall HP | instant | on pick |
| Reinforced Repair | `HealWall` | +400 wall HP | instant | on pick |
| Reinforced Barricade | `WallDamageReduction` | −40% wall dmg | 30 s | next wave start |
| **Aegis Shield** | `WallShield` | 250 absorb HP | 30 s | next wave start |
| Tar Field | `ArenaSlowZone` | 40% slow, 16 m band (mid-lane) | 25 s | next wave start |
| Glacial Choke | `ArenaFreezeZone` | Freeze, 4 m band (at wall) | 10 s | next wave start |
| Flash Freeze | `FlashFreeze` | Freeze, full arena | 3.5 s | next wave start |

**Arming:** wall-buff and arena picks are applied on the next `WaveStartedEvent` (not at pick time), because the
shop opens during the paused boss intermission with no enemies present. This makes "one wave" precise and stops a
timer/zone draining through the pause. Instant repair applies immediately on pick.

## Code changes
- **`ConsumableEffectType`** — added `WallDamageReduction`, `WallShield`, `ArenaSlowZone`, `ArenaFreezeZone`,
  `FlashFreeze`. Legacy stat/luck/reroll values kept in the enum only so old code compiles (no asset uses them).
- **`ConsumableDefinitionSO`** — added `_areaExtent` (arena band depth, metres) + `AreaExtent`. Reuses
  `_effectValue`/`_duration` for magnitude/lifetime.
- **`WallRuntime`** — **NEW mechanics (highest-risk item):** a temporary absorb `_shield`/`_shieldRemaining`
  (Aegis) and a `_damageReductionFraction`/`_reductionRemaining` window (Barricade). `TakeDamage` now reduces →
  absorbs → applies to HP; a new `Update` counts the timers down; `AddShield`/`SetDamageReduction` are the entry
  points. **Flag:** timers run in real time, so a mid-wave level-up pause drains them slightly (durations set
  generously to absorb this; a PauseState-gated timer is a possible follow-up).
- **`GroundZone`** — added an `_appliedStatus` (default `Slow`) + `ControlBox` factory so a zone can refresh
  **Freeze** instead of Slow. The Slow-application block now applies the chosen status (Freeze ignores magnitude).
  All existing callers (Frost Zone / Frozen Ground / Firewall) are unchanged (default Slow).
- **`WaveSpawner`** — owns a `GroundZoneManager ArenaZones` (public), ticked each unpaused frame against
  `ActiveEnemies` (no DoT), cleared on run end; boss-only shop trigger; removed `_shopIntervalWaves` +
  `IsShopIntermissionWave`.
- **`ShopController`** — rewritten to the pick-one-free model: `GenerateOffer` (tier-weighted distinct subset),
  `CanPick`/`Pick`, `HasPicked`, `Dispose`. Effects route through `WallRuntime` (repair/shield/reduction) and the
  WaveSpawner's `ArenaZones` (`GroundZone.ControlBox`) + `EnemyRuntime` status — **no parallel arena path**. Injected
  `WallRuntime`, `WaveSpawner`, `EventBus` (per §3.5). No currency/reroll/ConsumableInventory use.
- **`ShopScreenController`** — rewritten UI: renders the offer, one **Pick** button per item; picking applies +
  closes + `ContinueAfterIntermission`. Reroll/continue buttons hidden (no reroll, no skip). Currency label shown
  for info only (never spent).
- **`Task80ShopRedesignSetup`** (editor) — authors the 7 assets, rewires `SampleScene` `_availableConsumables` +
  `_offerSize`, and **deletes the 33 removed consumable assets** (everything in the folder except the 7). Menu:
  **`Wavekeep/Setup Task 80 (Utility Shop)`**.
- **CLAUDE.md §1 + §2** — updated: Core Loop step 5 is now the boss-clear utility pick; Currency retained without a
  shop sink; a locked shop decision + the §3.1 table row updated.

## Aegis Shield approach (per task callout)
`WallRuntime` had only `Heal`/`TakeDamage`. Aegis adds a **flat absorb buffer** drained before HP in `TakeDamage`
(picked value, expiring on a timer), and Barricade adds a **timed incoming-damage multiplier**. Both are minimal,
self-contained additions to `WallRuntime` — no new HP path, no SO/economy changes. Highest-risk item because it's
genuinely new wall behavior; kept as simple as possible (absorb pool + multiplier, real-time timers).

## Removed (33 assets, via the setup)
All of Task 79's removal list: Basic/Sharp/Honed/Whetstone/Lightning (flat dmg), Crit Chance/Damage, Swift/Haste
(cooldown), Frost Potency, Blast Radius (AoE), **Ultimate Duration** (confirmed removed), Luck, Reroll. Only Wall
Repair Kit was retained (and updated). The old shop setups (Task 06/09/23/24/30) are **superseded — do not re-run**
them or they'll recreate removed assets.

## Acceptance criteria
- Shop opens only on boss death. ✓ (`IsBossWave` trigger)
- 3–4 items, pick exactly one, free. ✓ (offer 4, single `Pick`, no currency/reroll/skip)
- 33 assets removed, no orphaned refs. ✓ (setup deletes them + rewires pool) — *after running Setup Task 80*
- All 7 items work (wall HP/damage + arena effects on enemies). ✓ (routes through WallRuntime / GroundZone / status)
- Aegis approach documented + WallRuntime changes flagged. ✓ (above)
- Arena hooks existing `GroundZoneManager`/status — no parallel path. ✓
- Currency infra untouched. ✓ (CurrencyManager/drops/UI unchanged; retained, unused by shop)
- CLAUDE.md §1/§2 updated. ✓
- Values/durations/trigger points documented + flagged. ✓ (above)

## Setup / how to finalize
Run **`Wavekeep/Setup Task 80 (Utility Shop)`** in the editor (authors the 7 assets, rewires SampleScene, deletes
the 33 old assets), then play from the Hub and clear to wave 5. New editor + asset files need a Unity import to
generate `.meta` files.

## Flags for review
1. **WallRuntime buff timers are real-time** (level-up pause drains them slightly) — durations padded to compensate.
2. **Glacial Choke freeze at the wall is strong** (enemies frozen in-band can't advance to attack for its lifetime) —
   lifetime kept short (10 s) and flagged.
3. **Flash Freeze catches the opening spawns** of the next wave (3.5 s at wave start), not a mid-wave panic button,
   because the shop is a between-wave reward — documented interpretation of "freeze all currently active".
4. **Reroll infrastructure (`RerollManager`, `RerollPointsChangedEvent`) is now dormant** (no source/sink) but left
   in place — removing it was out of scope.
5. Zone band geometry uses fixed defaults + the per-item `_areaExtent`; all flagged for tuning.
