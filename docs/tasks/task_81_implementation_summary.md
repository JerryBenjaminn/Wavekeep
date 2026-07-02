# Task 81 — Implementation Summary: Full-Run Balance Validation & Tuning (Post-Task-80)

> Analysis-first task. The §2 report is **`docs/balance/balance_validation_003.md`** (written before any
> tuning; read it first — §6 there is the recommendation list this summary implements). This file records
> what was changed, the §3a/§3b dispositions, and what needs Jerry's playtest validation.

---

## Headline finding (from the report)

The Task 81 hypothesis ("waves 30–60 may have flipped to unbeatable") **understated the problem**. Enemy
scaling at wave 60 is **×49** (per-wave column × milestone — analysis_002's "×7" ignored the per-wave
column), and **ContactDamage rides the same multiplier as HP against a fixed 300-HP wall**. A wave-10
boss already hits for 60 (5 swings = run over) against a fresh-save solo hero who cannot kill it in time;
a wave-60 boss hits for 735 — 2.4 walls per swing — so even a full-Epic run was a coin flip on boss
walk-time, not a DPS check. Separately: all 60 waves are Skeleton-only (PlaceholderGrunt fell out of the
game in the Task 63 rewrite), and crit damage is now a dead stat (see flags).

## §3a — Card-picker pool exhaustion: **no fix needed (verified, documented)**

`LevelUpCardPicker.ShowCard` already implements the task's acceptable fallback (2): an empty draw logs a
warning, decrements the queued pick, and resumes via the shared drain step — **no crash, no empty cards,
no soft-lock, pause always balanced** (`LevelUpCardPicker.cs:251-263`). Partial pools also degrade
correctly (spare card slots hidden). Exhaustion is additionally near-unreachable in practice: a full run
yields ~8,400 XP ≈ 21 picks vs 24 needed to max a solo hero's 8 lines (48 for a duo). Per the task's own
trigger condition ("if it crashes / presents empty cards / has undefined behavior") no code change was
made; upgrading skip → filler-stat-card is flagged as optional future polish (it's also arguably "new
upgrade content", which §3a forbids).

## §3b — Task 36 shared `UpgradeInventory`: **documented, verdict = BUG (fix deferred)**

One `UpgradeInventory` per run is shared by all active heroes; per-hero line **tiers** and apex unlocks
are correctly independent, but the picked tier **effects** pool together and every hero's
`AbilityRuntime` resolves against the whole pool. Concretely: role-scoped numeric modifiers
(Basic/Ultimate damage, cooldown, radius, duration — `ComputeStats:1502-1513`) apply to BOTH heroes'
same-role abilities; any Basic picks up a held Chain Lightning (`ResolveChain:537`); every hero's crit
roll adds Overcharge's crit chance (`RollCrit:1390`); any status-applying ability applies ALL held on-hit
status upgrades (`:1455`); and `HasTag` tag-rule resolution is pooled. Verdict: **known bug** — Task 36
flagged it as a leak at introduction and CLAUDE.md §3.8 was written for a single active hero; the net
effect is a hidden team-wide stack that silently widens the solo-vs-duo power gap. Not changed in this
task (per §3b); the clean future fix is a per-hero inventory (or hero-tagged entries) resolved per
caster at the `AbilityExecutionContext` build site. **Task 81's tuning deliberately does NOT compensate
for this leak** — tuning to a bug would bake it in.

## §3c — Tuning implemented (report §6, R1–R5)

### Code (3 files + 1 new editor script)

| File | Change |
|---|---|
| `DifficultyTierSO.cs` | NEW serialized `_contactDamageScaling` (Range 0–1, **default 1 = exactly pre-Task-81 behavior**) + `GetContactDamageMultiplier(m) = 1 + (m−1)×scaling`. The tunable lives in the SO per the task's no-hardcoding rule. |
| `WaveSpawner.cs` | Computes the damped contact multiplier once per wave and threads it through `SpawnBosses`/`SpawnEnemy` → `EnemyRuntime.Initialize`. Scaling debug log now prints both multipliers. |
| `EnemyRuntime.cs` | `Initialize` gains optional `contactDamageMultiplier = -1` (negative → falls back to the HP multiplier, so any other/older caller is unaffected). `ContactDamage` uses it; HP/Armor/MR handling unchanged. |
| `WallRuntime.cs` | Code default `_maxHP` 300 → 1200 (the SCENE's serialized value wins — see setup below). |
| `Task81BalanceSetup.cs` (new) | Menu **`Wavekeep/Setup Task 81 (Balance Tuning)`** — idempotently applies every data value below + sets the open scene's wall to 1200. |

**This is the one structural code change** (flagged per instructions): contact damage and HP now scale on
separate curves. Precedent: Task 63's XP-quadratic code change inside a tuning task. Setting the factor
to 1 restores the old behavior exactly.

### Data (already applied to the committed assets; the setup menu re-derives them)

| Asset | Field | Old → New |
|---|---|---|
| `GameTier.asset` | `_contactDamageScaling` | (n/a) → **0.12** — wave-60 contact ×6.76 instead of ×49 |
| `EvilGod.asset` | `_contactDamage` | 15 → **30** (reverses the Task 64 stopgap against the new wall: wave-5 boss ≈33/hit vs 1200 = pressure, not panic; wave-60 ≈203/hit = 6 swings, still lethal if ignored) |
| `Wave_10..60.asset` | `_statMultiplier` | 2.0→7.0 (+0.1/wave) → **1.5→7.0 (+0.11/wave)** — kills the ×1.9 wave-10 double-jump (now ×1.43); waves 1–9 and the ×49 wave-60 endpoint unchanged |
| SampleScene `WallRuntime` | `_maxHP` | 300 → **1200** (**menu-only** — scenes are never hand-edited per project convention; RUN THE SETUP + save the scene) |
| `WallRepairKit.asset` | `_effectValue` | 150 → **300** (desc updated) |
| `ReinforcedRepair.asset` | `_effectValue` | 400 → **800** (desc updated) |
| `AegisShield.asset` | `_effectValue`/`_duration` | 250 → **500** / 30 → **40s** (desc updated) |
| `ReinforcedBarricade.asset` | `_duration` | 30 → **40s** |
| `TarField.asset` | `_duration` | 25 → **40s** |
| Glacial Choke / Flash Freeze | — | unchanged (Choke already flagged strong; Flash Freeze flagged weakest — revisit after playtest) |

Both assemblies compile clean (runtime + editor, verified via `dotnet build`). Not touched, per scope:
ability values, upgrade-line values, gear affix ranges, XP curve, currency, drop rates.

## Setup / how to finalize

1. Let Unity import (new file `Assets/Scripts/Editor/Task81BalanceSetup.cs` needs a `.meta`).
2. Open **SampleScene**, run **`Wavekeep/Setup Task 81 (Balance Tuning)`**, **save the scene** — this is
   what applies the 1200 wall HP (all other values are already committed in the assets; the menu is
   idempotent re-derivation).

## Needs Jerry's playtest validation (the model's edge cases)

1. **Fresh-save solo to wave 30** — the projection lands this *on the edge* (~120 DPS ceiling + good
   utility picks). Knob order if too hard: `_contactDamageScaling` 0.12 → 0.10, then wall 1200 → 1500.
   Both are pure data now. (Note: a literal fresh save is ONE hero — slot 2 unlocks at wave 15.)
2. **Full-Epic clear of wave 60 without steamrolling** — projected ~35s wave clears with the boss dying
   inside its ~12.5s walk; single-target-only teams will queue-overflow at wave 55+ (judged acceptable —
   comp should matter — but needs a real verdict).
3. **Shop pick texture at waves 15–30** — repair vs arena-control should now be a real decision (boss
   walk-slowing ≈ hundreds of effective wall HP). Watch whether Flash Freeze ever gets picked.

## Flags for review

1. **Contact-damage decoupling is a code change** inside a tuning task (see §3c rationale + default-1
   back-compat). If unwanted, set GameTier's `_contactDamageScaling` to 1 — data-only revert.
2. **Re-running `Setup Task 63` would clobber Task 81's wave column** (its formula also no longer matches
   the pre-81 committed assets — it writes 1+0.05(w−1) → wave 60 = 3.95, not the 7.0 the assets held).
   If Task 63's setup is ever re-run, re-run Task 81 afterwards.
3. **Crit damage is a dead stat post-Task-80** (`RollCrit` reads crit-damage bonus ONLY from consumables,
   all deleted) — crits multiply by ×1.0; Bolt Striker's Overcharge crit-chance half is a damage no-op
   (its spike payload still works). Out of Task 81 scope (line/hero balance); belongs to the future
   hero-balance task. Also restated: currency has no sink (~1,860/run accrues), Range/Farsight gear stat
   still dead, Wave_05's Skeleton count (23) predates Task 64's boss-escort halving (cosmetic
   inconsistency).
4. **Wall buff timers remain real-time** (Task 80 flag) — the 40s durations include padding for the
   level-up pause drain; a PauseState-gated timer is still the eventual clean fix.
