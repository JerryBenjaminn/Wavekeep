# Balance Validation 003 — Post-Task-80 Full-Run Audit (Task 81 §2)

> Analysis deliverable for Task 81. Written BEFORE any tuning was applied; §6's recommendations are what
> Task 81 §3c then implements. Every number cites the asset/code it was read from. Companion to
> `audit_001.md` (Task 61) and `shop_balance_analysis_002.md` (Task 78).

---

## 0. Corrections to prior assumptions (read first)

Two inputs the Task 81 brief inherited from analysis_002 are wrong against current data:

1. **Enemy scaling at wave 60 is ×49, not ×7.** analysis_002 quoted "milestone curve only" (×7 at
   wave 60), but the live formula (`WaveSpawner.cs:293`) is
   `total = Global(1.0) × WaveConfig.StatMultiplier × MilestoneMultiplier`. Post-Task-63 the per-wave
   column ramps 1.0→7.0 (`Wave_01..60.asset`) and milestone reaches ×7.0 (`1 + floor(60/5)·0.5`), so
   **wave 60 total = 7 × 7 = ×49**, scaling **both HP and ContactDamage** (`EnemyRuntime.cs:235-237`).
   Armor/MagicResist are deliberately NOT scaled (`EnemyRuntime.cs:239-240`, Task 64 follow-up).
2. **PlaceholderGrunt is out of the game.** Every one of the 60 waves spawns **Skeleton only**
   (all `_spawnEntries` reference guid `a3a96c…` = Skeleton.asset); EvilGod is the sole boss
   (GameTier `_bossDefinition`), 1 per boss wave, every 5th wave. Counts ramp 19→39, spawn interval
   0.45s→0.18s.

Baseline facts used throughout: Skeleton HP 10 / contact 5 / speed 3 / 5 XP / 1 currency.
EvilGod HP 200 / contact 15 / speed 1.6 / Armor+MR 50 (= takes ×0.667 damage → effective HP ×1.5) /
50 XP / 25 currency. **Wall = 300 HP flat** (`WallRuntime._maxHP`, SampleScene), no between-wave repair,
enemies attack every 1.0s (`WaveSpawner._attackInterval`). Arena ≈ 20 m spawn-line→wall: Skeleton walks
it in ~6.7s, EvilGod in ~12.5s. XP threshold = `10 + 5L + 2L²` (bootstrap values confirmed in scene).

---

## 1. Post-Task-80 DPS vs enemy HP curve (§2.1)

### 1.1 Player damage model

**Fresh save (no gear), solo hero** (a truly fresh save has ONE hero slot — slots 2/3/4 unlock
persistently at waves 15/30/50, Task 42; a first-ever run is single-hero):

| Stage | Approx. picks | Representative effective DPS (Bolt Striker, best ST) |
|---|---|---|
| Wave 1–4 | 0–5 | 20–30 (basic 10/0.5s + Voltaic Nuke 50/6s) |
| Wave 5–10 | 6–10 | 35–60 |
| Wave 15–20 | 12–15 | 70–90 |
| Wave 25–30 | 17–19 (≈6 of 8 lines T3, 1–2 apexes) | **~100–130 ceiling** |

XP arithmetic: ~1,560 trash kills + 12 bosses over a full run ≈ **8,400 XP ≈ level 21–22**, i.e. ~21
picks. A solo hero needs 24 picks to max all 8 lines — so a fresh solo run's DPS **plateaus around
wave 30–35 at roughly 100–130** and stays there.

**Gear multipliers** (implicits from `GearBase_*.asset`, per-rarity affix mids from Task 76,
affix counts 0/1/2/3/4 for Common→Legendary per `GearAffixCountConfig`): gear multiplies and flat-adds
AFTER lines (`AbilityRuntime.ComputeStats`), and cooldown gear divides cast time — the growth is
**multiplicative**:

| Loadout (6 slots) | Net effect on the maxed-line solo baseline |
|---|---|
| Full Common (implicits only) | ×1.15–1.2 → ~120–150 DPS |
| Full Rare (2 affixes/item) | ×4–5 → **~400–600 DPS** |
| Full Epic (3 affixes/item) | ×6–12 depending on affix luck (Empowered/Sharpened/Swift-heavy ≈ ×12; mixed with dead Farsight/Luck ≈ ×6) → **~600–1,400 DPS** |

### 1.2 Enemy side at the checkpoints

| Wave | Total mult | Skeleton HP | Wave trash HP (count×HP) | EvilGod eff. HP (×1.5) | Skeleton hit | EvilGod hit |
|---|---|---|---|---|---|---|
| 1 | 1.0 | 10 | 190 | — | 5 | — |
| 5 | 1.8 | 18 | 414 | 540 | 9 | **27** |
| 10 | 4.0 | 40 | 560 | 1,200 | 20 | **60** |
| 15 | 6.25 | 62.5 | 1,000 | 1,875 | 31 | 94 |
| 30 | 16.0 | 160 | 3,840 | 4,800 | 80 | **240** |
| 45 | 30.25 | 302.5 | 9,680 | 9,075 | 151 | 454 |
| 60 | 49.0 | 490 | 19,110 | 14,700 | 245 | **735** |

### 1.3 Verdict — where it flips

The **HP curve is roughly fine**; the **wall curve is broken everywhere past wave ~8**:

- **Fresh solo dies at wave 10, not wave 30.** Wave 10's boss hits the 300-HP wall for 60/hit
  (5 hits, ~5 seconds at the wall) while carrying 1,200 effective HP against a ~55-DPS hero —
  the boss cannot be killed before it lands ~20 hits. Wave 5 is already a knife-edge
  (27/hit → 11 hits vs a 540-eff-HP boss at ~25–40 DPS). Wave 10 also stacks the curve's worst
  wave-over-wave jump: total mult 2.1→4.0 (**×1.9** — per-wave column jumps 1.4→2.0 exactly when a
  milestone step lands, the same double-jump audit_001 flagged at TestTier's wave 10).
- **A full-Epic save fails the same way at the far end.** ~900 DPS clears wave 60's 34k effective HP
  in ~35s — but a single boss hit is 735 vs a 300 wall. If the boss isn't dead within its 12.5 s walk
  (needs ~1,200+ boss-focused DPS through 39 escorting Skeletons), the run ends in **one hit**. Trash
  alone breaks it too: at 245/hit, two Skeleton swings end the run.
- Conclusion: the hypothesis ("waves 30–60 flipped to unbeatable") **understates it**. Because
  ContactDamage rides the full ×49 HP multiplier against a fixed 300-HP wall, the game is currently
  a *wall-one-shot check*, not a DPS check, from mid-game on — for fresh AND geared saves. No
  checkpoint-multiplier or wall-HP number alone fixes both ends, because HP needs steep growth
  (to survive Epic-gear DPS) while contact damage needs shallow growth (to keep the wall a
  pressure gauge instead of a coin flip). **HP and contact damage must scale on separate curves.**

---

## 2. Wall pressure assessment (§2.2)

With the stat potions gone, wall pressure is the ONLY fail pressure — and it's binary:

- **Waves 1–7:** effectively zero pressure (5–9 dmg hits vs 300 HP; trash dies in <1s). Trivial.
- **Waves 8–14 (fresh):** pressure goes from "none" to "fatal" in one boss. There is no attrition
  band where the player watches the wall drop to 60% and starts valuing repair picks.
- **Waves 15+:** every enemy that touches the wall removes 10–80% of it per swing. Bosses one-to-five-shot
  it from wave 20 (fresh) / wave 45 (geared). "Meaningful pressure at checkpoints" does not exist —
  it's instant-loss roulette on whether the boss gets touched by the wall at all.
- The Task 80 items were authored against this 300-HP wall (Repair +150 = 50%, Aegis 250 ≈ one
  mid-game boss hit), and its summary self-flagged all values as placeholders. They are correctly
  sized ONLY for waves ~5–15.

---

## 3. Shop utility relevance (§2.3)

At current values the 12 pick-moments (waves 5,10,…,60) split into three bands:

| Band | Current experience |
|---|---|
| Waves 5–10 | **Repair is near-mandatory** (only meaningful item vs the boss-hit math); arena items are luxuries |
| Waves 15–30 (fresh) | **No pick matters** — the wall dies to 2–5 hits regardless; +150 HP, a 250 absorb, or −40% for 30s don't change the outcome |
| Waves 30–60 (geared) | Same: one boss hit ≥ 245–735 vs 300 HP. Every pick is irrelevant; Flash Freeze (3.5s) is worth less than half a boss walk-second |

So the answer to "interesting tactical moments or afterthought?" is currently **afterthought by
arithmetic** — not because the item design is wrong (repair-vs-control is a genuinely good decision
shape) but because wall HP and contact scaling leave nothing for the items to protect. After §6's
re-scaling, the intended play emerges naturally: arena control (Tar/Choke/Flash) extends the boss's
walk time — each second bought ≈ 100+ effective HP of wall saved — while Barricade/Aegis convert to
raw wall endurance, and Repair converts banked safety into recovery. Those become genuinely different
answers to "how do I survive the next boss window."

---

## 4. Card-picker pool exhaustion (§2.4)

**Traced: `LevelUpCardPicker` (Assets/Scripts/UI/LevelUpCardPicker.cs). The exhaustion case is already
handled — no crash, no empty cards, no soft-lock.** `ShowCard()` (lines 251–263): when
`DrawCandidates` returns zero eligible lines (all lines of all active heroes at max tier), it logs a
warning, decrements the pending-pick queue, and `AdvanceQueueOrResume()` either shows the next queued
pick or hides the panel and releases the reference-counted pause. A level-up past exhaustion is a
~1-frame pause/resume + a console warning; the run continues normally. Fewer-than-3 eligible lines
also degrades correctly (spare card slots are hidden, lines 265–278).

**When does it trigger?** Practically almost never. Full-run XP ≈ 8,400 → level ~21–22 (~21 picks).
Solo needs 24 picks to max 8 lines; a 2-hero team needs 48 for 16. So exhaustion requires a **solo**
run that reaches roughly wave 55+ AND favourable pick routing — the boundary case, not the norm.
(The Task 81 brief's "wave 45+" guess assumed a smaller pool.)

**Disposition (per §3a):** current behavior = the task's acceptable fallback (2) ("skip the level-up
screen entirely"), already implemented with the queue/pause drained correctly. No code change made.
Upgrading to fallback (1) (a flat-stat filler card so late levels never feel wasted) is possible but
was NOT done: the trigger condition ("crashes / empty cards / undefined behavior") is false, and a
filler card is content the task forbids adding. Flagged as an optional future polish item.

---

## 5. Task 36 shared `UpgradeInventory` — current behavior (§2.5, document only)

**Facts (traced, not changed):** there is exactly ONE `UpgradeInventory` per run, owned by
`GameSession`. Each hero's `HeroRuntime.TryUpgradeLine` pushes its picked tier's
`UpgradeDefinitionSO` into that shared inventory (replace-semantics per line), and every
`AbilityRuntime` of every hero resolves against the whole pool. What IS correctly per-hero: line
tiers, apex unlock state, and pick routing (`LevelUpCardPicker` applies a card to its owning hero —
Task 36's `Candidate` struct). What is NOT per-hero — these leak team-wide:

- **Role-scoped numeric modifiers** (`AbilityRuntime.ComputeStats:1502-1513`): a `BasicDamage` /
  `BasicCooldown` / `BasicRadius` / `UltimateDamage` / `UltimateDuration` modifier from Hero A's line
  applies to Hero B's same-role ability. Role-gating exists ("can't leak across the basic/ultimate
  boundary", line 1501); hero-gating does not.
- **Mechanic getters consumed in shared code paths**: any Basic-role ability picks up a held Chain
  Lightning (`ResolveChain:537` — Frost Warden's basic gains Bolt's chain jumps), every hero's crit
  roll adds the held Overcharge crit chance (`RollCrit:1390`), and any ability with
  `AppliesStatusEffects` applies ALL held on-hit status upgrades (`ApplyStatusEffectsFromUpgrades:1455`).
- **Tag presence** (`HasTag`) for `TagInteractionRule` resolution is pooled: Hero A picking an
  AoE-tagged line can activate Hero B's "+X% when holding AoE" rule.
- Effects that are *functionally* hero-scoped survive only because their consuming code path is gated
  on ability DATA (e.g. `FrostMaxStacks` only matters to an ability that applies Frost stacks;
  Firewall tick modifiers only in the `AppliesFireWall` path).

**Verdict: known BUG (unintended emergent behavior), not a design decision.** Task 36 flagged it as a
leak at introduction and nothing since has ratified it; CLAUDE.md §3.8 was written for a single active
hero ("the player's currently-held selections") and doesn't contemplate dual-hero pooling. Net effect
today: dual-hero runs get hidden team-wide stacking (each hero's generic damage/cooldown lines buff
both heroes), which silently widens the solo-vs-team power gap beyond the intended 2×. **Fix deferred
per Task 81 §3b** — the clean shape is a per-hero inventory (or hero-tagged entries) resolved per
caster, which is a contained change at the `AbilityExecutionContext` build site. Balance work below
does NOT compensate for this leak (tuning to a bug would bake it in).

**Bonus orphan found while tracing (flag only, out of Task 81 scope):** `RollCrit:1396` reads crit
DAMAGE bonus **exclusively from consumables** — which Task 80 deleted. Crits now multiply damage by
×(1+0) = **×1.0**. The crit-chance half of Bolt Striker's Overcharge line is currently a no-op for
damage (its separate spike payload still works). Belongs to the future hero-balance task.

---

## 6. Concrete tuning recommendations (§2.6 — implemented by Task 81 §3c)

**R1 — Decouple contact-damage scaling from HP scaling (the structural fix).** Add a serialized
damping factor to `DifficultyTierSO` (`_contactDamageScaling`, default 1.0 = exactly today's
behavior) and have the spawner pass a damped contact multiplier:
`contactMult = 1 + (totalMult − 1) × factor`. **Set GameTier's factor to 0.12.** Wave 60's contact
multiplier becomes ×6.76 (Skeleton 33.8/hit, EvilGod 202.8/hit) while HP keeps its full ×49.
This is the one code change (mirrors Task 63's XP-quadratic precedent); the value lives in the SO.

**R2 — Wall HP 300 → 1,200** (`WallRuntime._maxHP`, scene value + code default). Creates an attrition
band: the wall can absorb 30–90 trash swings / 6–37 boss swings depending on wave, instead of 1–5.

**R3 — EvilGod base contact 15 → 30.** Task 64 cut 30→15 because compounding made wave 5 lethal vs a
300 wall; with R1 capping late compounding and R2 quadrupling the wall, 30 restores boss identity:
wave 5 ≈ 33/hit (36 swings to kill the wall — pressure, not panic), wave 30 ≈ 80, wave 60 ≈ 203
(6 swings — a geared player who ignores a boss for ~6 seconds at the wall still loses).

**R4 — Smooth the wave-10 double-jump.** Per-wave `StatMultiplier` for waves 10–60 becomes a linear
ramp **1.5 → 7.0 (+0.11/wave)** instead of 2.0 → 7.0 (+0.1). Waves 1–9 and the wave-60 endpoint are
unchanged. Wave 10's wave-over-wave total jump drops from ×1.90 to ×1.43 — still clearly a checkpoint
spike (milestone step lands there), no longer the run-ending cliff. Mid-game eases ~8–25%
(w15 6.25→5.13, w20 9→7.8, w30 16→14.8) converging back to ×49 at 60.

**R5 — Re-scale the Task 80 shop items to the new wall (SO value edits):**

| Item | Field | Old → New | Rationale vs 1,200 wall |
|---|---|---|---|
| Wall Repair Kit | `_effectValue` | 150 → **300** | 25% of wall (was 50% of 300 — keep the fraction meaningful) |
| Reinforced Repair | `_effectValue` | 400 → **800** | 67% — the "big" repair stays big |
| Aegis Shield | `_effectValue`, `_duration` | 250 → **500**, 30 → **40s** | ≈2.5 late boss hits; duration covers a typical wave + pause drain (Task 80 flag) |
| Reinforced Barricade | `_duration` | 30 → **40s** | −40% unchanged; duration now covers the boss window |
| Tar Field | `_duration` | 25 → **40s** | 40% slow unchanged; a slowed boss walk = ~8 extra seconds of DPS |
| Glacial Choke / Flash Freeze | — | unchanged | Choke already flagged strong; Flash Freeze stays the cheap-feeling pick — flagged as the roster's weakest, revisit after playtest |

**Projected result** (the table Jerry should validate in playtest):

| Wave | Total mult (new) | Skeleton hit | Boss hit | Wall swings to die (boss) | Fresh solo (~DPS) | Full Epic (~DPS) |
|---|---|---|---|---|---|---|
| 5 | 1.8 | 5.5 | 33 | 37 | OK (25–40) | trivial |
| 10 | 3.0 | 6.2 | 37 | 32 | OK (~55) — was fatal | trivial |
| 15 | 5.13 | 7.5 | 45 | 27 | tense (~70), repair matters | easy |
| 20 | 7.8 | 9.1 | 55 | 22 | hard — arena pick ~required | easy |
| 30 | 14.8 | 13.3 | 80 | 15 | **edge of reachable** (~120 + good picks) | comfortable |
| 45 | 29.4 | 22 | 132 | 9 | out of reach solo-fresh (intended) | tense |
| 60 | 49.0 | 33.8 | 203 | 6 | — | **challenging**: boss must die in its ~12.5s walk or be CC'd; AoE needed vs the 39-trash queue |

**Explicit uncertainty for playtest:** the fresh-solo wave-30 acceptance criterion lands *on the
edge* by this model (a literal fresh save is one hero; the 2-hero comparisons in earlier audits don't
apply to it). If playtest says wave 30 solo is out of reach, the intended knob order is: factor
0.12 → 0.10, then wall 1,200 → 1,500 — both now pure data. Late-game geared difficulty is
comp-sensitive (single-target-only teams will queue-overflow at wave 55+); that is judged acceptable
(kit variety should matter) but needs a real playtest verdict.

**Not touched (out of Task 81 scope, restated):** ability/line/affix values; the crit-damage orphan
(§5 flag); currency still has zero sinks (~1,860/run accrues unspent); Range/Farsight dead stat;
enemy variety (Task 82).
