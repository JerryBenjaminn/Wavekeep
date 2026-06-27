# Balance Audit 001 — Current Numeric State

> Read-only audit (Task 061). No values changed. Every number cites the asset/code it was read from.
> Active content set: **TestTier** difficulty (the tier actually wired into `SampleScene`'s WaveSpawner).
> Conventions: HP is currently **cosmetic** (enemies attack the wall, not heroes — `HeroDefinitionSO._baseHealth` is unused at runtime).

---

## 0. What's actually live (important context)

- **Active difficulty tier = `TestTier`** (`Assets/Data/DifficultyTiers/TestTier.asset`), the only tier referenced by `SampleScene`. `GameTier` ("Normal") exists but has an **empty `_waves` list** and **0 scene references** → it is dead/unused.
- Active waves use only three enemies: **Skeleton** (early), **PlaceholderGrunt** (wave ~10+), and **EvilGod** as the boss. `Goblin` and `BossGrunt` exist but aren't in the active waves.
- This means much of the "designed" data (GameTier, BossGrunt boss, Goblin) is **not what the game currently plays** — a recurring theme below.

---

## 1. Hero Stats

### 1.1 Base stats (`Assets/Data/Heroes/*.asset`)

| Hero | Base HP | Base Luck | Basic (dmg / cd / range / type) | Ultimate (cd) |
|---|---|---|---|---|
| Frost Warden | 120 | 5 | Frost Bolt Burst — **8** / **1.2s** / 28 / TargetedAoE 2.5m, Magical | Frost Zone — **35s** |
| Bolt Striker | 100 | 5 | Lightning Bolt — **10** / **0.5s** / 28 / SingleTarget, Magical | Voltaic Nuke — **6s** |
| Pyromancer | 110 | 5 | Fireball — **9** / **0.6s** / 28 / SingleTarget, Magical + Burn 3/tick×3s | Firewall — **14s** |
| Marksman | 95 | 5 | Marksman Shot — **8** / **0.5s** / 28 / PiercingLine, Physical | Minigun — **18s** |

Sources: `Hero_*.asset` (`_baseHealth`, `_baseLuck`), `Ability_*.asset` / `BasicFrostNova.asset` / `UltimateIcicle.asset` (`_baseDamage`, `_baseCooldown`, `_range`, `_targetingType`, `_damageType`).

**At-a-glance basic DPS (no upgrades, single-target):** Bolt 20 · Marksman 16 (×pierce count) · Pyro 15 +DoT · Frost 6.7 (×AoE count). Bolt Striker is the clear single-target leader; Frost trades raw DPS for AoE + CC.

**Opinion:** Base HP (95–120) is a near-flat spread and currently does nothing (no hero-damage system). The four basics are reasonably differentiated. The two outliers worth noting: **Frost Warden's ultimate cooldown is 35s** while every other ultimate is 6–18s — a 2–6× gap (see §7). Pyromancer's Fireball cd 0.6 vs the others' 0.5/1.2 is oddly precise.

### 1.2 Ultimate base payloads

- **Voltaic Nuke** (Bolt): 50 dmg, cd 6 (`Ability_BoltNuke`). The only ultimate with high flat damage + a short cd.
- **Frost Zone** (Frost): 0 damage, 25% slow, 6s duration, cd 35, full-arena band (`UltimateIcicle`). Pure control.
- **Firewall** (Pyro): 6 dmg/tick × 0.5s for 5s + entry-burn 4/tick×3s, cd 14 (`Ability_Firewall`).
- **Minigun** (Marksman): 6 dmg/shot, 5s channel, 0.15s/shot ≈ 33 shots → ~198 raw damage/channel, cd 18 (`Ability_Minigun`).

### 1.3 Upgrade lines (8 per hero, 3 tiers each — `Assets/Data/UpgradeLines/UpgradeLine_*.asset`)

Tiers use **absolute replace-semantics** (Tier 3 *is* the final value, not additive). Values live in each tier's `_description` + referenced `Upgrades/*` effect. There are **34 line assets**; heroes reference **8 each (32 used)** → **2 orphan lines** (see §7). The dominant scaling shape across damage-modifier lines is **+15% / +30% / +50%** (T1/T2/T3). Representative lines:

| Line (hero) | T1 → T2 → T3 |
|---|---|
| ChargedFinisher (Frost ult) | +15% / +30% / +50% ult dmg |
| DeepeningFrost (Frost) | slow 30% / 40% / 50% (overrides base 25%) |
| HardFreeze (Frost) | 10%@0.5s / 20%@0.75s / 30%@1s freeze-on-hit |
| StaticCharge (Bolt) | +5%/stk max3 (+15%) / +7% max4 (+28%) / +10% max5 (+50%) |
| ChainLightning (Bolt) | jump 1@40% / 1@55% / 2@55% |
| Overcharge (Bolt) | +5% crit & 5%@+50% spike / +10%&10%@+75% / +15%&15%@+100% |
| StackingEmbers (Pyro) | +10%/stk max3 / +15% max4 / +20% max5 |
| SmolderingWound (Pyro) | +20%dmg+1s / +35%+2s / +50%+3s Burn |
| HeavyRounds (Marksman) | +20% / +35% / +50% Minigun dmg |
| ArmorShredder (Marksman) | -3/stk max5 / -5 max6 / -7 max8 Armor, 3s |
| PiercingRounds (Marksman) | pierce 2 / 4 / unlimited |

**Opinion:** The lines are the **most internally consistent** part of the whole dataset — almost everything follows a clean +15/30/50 or stepped pattern with sensible T3 caps. This area looks deliberately designed, not test-tuned.

### 1.4 Apex talents (`ApexTalent_*.asset`) and combo apexes (`ComboApexes/*.asset`)

- **9 apex-talent assets**, each requires **2 lines maxed** (`_requiredLines` = 2). Heroes reference **2 apexes each (8 used)** → **1 orphan apex** (Frost has 3 apex assets: AbsoluteZero / PermafrostEruption / RemorselessWinter, but only references 2).
- Apexes are **whole extra auto-firing abilities** (own cooldown), e.g. Absolute Zero (40 dmg, cd 6), Permafrost Eruption (50% of basic, cd 10), Thunderstorm (50% basic ×2 hits + 1 chain, cd 9), Executioner's Volley (80% basic at most-shredded target, cd 10), Bullet Storm (50% basic, 1.5s burst), Cataclysm (60% basic, cd 11), Wildfire Apocalypse (Burn 4.5/tick×6s, cd 9).
- **5 combo apexes**: Shatter, Frostburn, Chain Combustion, Incendiary Rounds (`_effectType` 1–4), Frozen Lightning (original Task 38 schema). **Flag:** the per-combo magnitude fields weren't enumerated here (each combo uses a different schema); only the effect-type tags were confirmed.

**Apex vs maxed-line comparison (§2.1):** These aren't proportionally comparable — a maxed line is a *modifier* (+50% to one thing), an apex is *a new damage source you didn't have*. So an apex is a **step-change**, gated behind maxing 2 lines (= 6 card picks). My read: that gating feels right, but it means apex power is "binary" (you have a whole new ability or you don't) rather than a smooth curve — worth deciding deliberately whether that's intended.

---

## 2. Enemy Scaling

### 2.1 Base stats at wave-1 baseline (`Assets/Data/Enemies/*.asset`)

| Enemy | HP | Speed | Contact | Armor | MagicRes | Currency | XP | In active waves? |
|---|---|---|---|---|---|---|---|---|
| Skeleton | 10 | 3 | 5 | 0 | 0 | 5 | 5 | ✅ early |
| PlaceholderGrunt | 25 | 3 | 5 | 0 | 0 | 1 | 5 | ✅ wave ~10+ |
| **EvilGod (boss)** | 200 | 2.5 | **5** | 30 | 30 | **5** | 50 | ✅ boss |
| Goblin | 10 | 3 | 5 | 20 | 20 | 1 | 5 | ❌ unused |
| BossGrunt | 200 | 1.5 | 30 | 60 | 60 | 50 | 50 | ❌ unused (GameTier boss) |

### 2.2 Scaling formula (read from `WaveSpawner.SpawnWaveRoutine` + `DifficultyTierSO.GetMilestoneMultiplier`)

```
spawnMultiplier(wave) = GlobalStatMultiplier × WaveConfig.StatMultiplier × MilestoneMultiplier(wave)
MilestoneMultiplier(wave) = 1 + floor(wave / 5) × 0.5      // milestoneStep 0.5, interval 5
```
TestTier: **GlobalStatMultiplier = 1.2**, milestone step 0.5 / interval 5, **bossWaveInterval = 5** (bosses at 5/10/15/20), bossCount 1, boss = EvilGod.
The multiplier scales **HP, ContactDamage, Armor, MagicResist** only — **NOT** currency/XP rewards (those are read flat from the SO).

Per-wave `StatMultiplier` (`Assets/Data/Waves/Wave_##.asset`):
`1, 1.05, 1.1, 1.15, 1.5, 1.7, 1.3, 1.35, 1.4, 1.45, 2.0, 1.55, 1.6, 1.65, 1.7, 1.75, 1.8, 1.85, 1.9, 2.5`

### 2.3 Projected real curve (multiplier and trash-enemy HP)

| Wave | WaveMult | MilestoneMult | **Total ×** | Trash base HP | **Trash HP** | EvilGod HP (boss waves) |
|---|---|---|---|---|---|---|
| 1 | 1.00 | 1.0 | **1.20** | 10 (Skel) | 12 | — |
| 5 | 1.50 | 1.5 | **2.70** | 10 (Skel) | 27 | 540 |
| 6 | 1.70 | 1.5 | **3.06** | 10 | 31 | — |
| 7 | 1.30 | 1.5 | **2.34** ⚠️ | 10 | 23 | — |
| 9 | 1.40 | 1.5 | **2.52** | 10 | 25 | — |
| 10 | 1.45 | 2.0 | **3.48** | 25 (Grunt) | **87** ⚠️ | 696 |
| 11 | 2.00 | 2.0 | **4.80** | 25 | 120 | — |
| 12 | 1.55 | 2.0 | **3.72** ⚠️ | 25 | 93 | — |
| 15 | 1.70 | 2.5 | **5.10** | 25 | 128 | 1020 |
| 20 | 2.50 | 3.0 | **9.00** | 25 | 225 | 1800 |

⚠️ markers: **the curve is non-monotonic** — wave 7 (2.34×) is *easier* than wave 6 (3.06×) and even wave 5 (2.70×); wave 12 (3.72×) dips below wave 11 (4.80×). And **wave 10 nearly triples trash HP at once** (enemy swaps Skeleton→PlaceholderGrunt *and* a milestone step lands), 31→87.

**Opinion:** The milestone formula itself is clean and reasonable (+50% every 5 waves, compounding gently). The problem is entirely the **hand-set per-wave `StatMultiplier` column** — waves 5/6/11/20 look like manual spikes and 7/12 like un-reverted dips. This is the single most "test-tuned" curve in the project.

---

## 3. Currency & XP

### 3.1 Yields (flat — do **not** scale with wave)

- Per kill: Skeleton **5 currency / 5 XP**; PlaceholderGrunt **1 / 5**; EvilGod boss **5 / 50**. (`EnemyDefinitionSO._currencyReward/_xpReward`, applied unscaled in `CurrencyManager`/`XPManager`.)
- Wave 1 (30 Skeletons) → **~150 currency, ~150 XP** in the first wave alone.

### 3.2 XP curve (`XPManager.ComputeThreshold`, params from `SampleScene`)

```
threshold(level) = baseXP(10) + level × increment(5)     // LINEAR
```
L1→2 = 15, L2→3 = 20, L4→5 = 30, L9→10 = 60. Cumulative to L5 ≈ 90 XP; to L8 ≈ 210 XP.

**Opinion:** With ~150 XP in wave 1, the player reaches **~level 6–7 during the first wave**, and the linear curve never catches up to the kill-rate → **leveling is extremely fast / front-loaded**. Either XP yield is too high or the curve should be super-linear.

### 3.3 Shop affordability (`Assets/Data/Consumables/*.asset`)

Prices are cleanly tiered: **T1 ≈ 12–20, T2 ≈ 25–35, T3 ≈ 45–50** (plus a few un-tiered one-offs: SharpElixir 10, DamageElixirT2 25, GreaterWhetstone 40, SwiftTonic 20, WallRepairKit 15, RerollPotion 10/20/35).

Income vs price: wave 1 yields ~150 currency; a **T3 potion is ~45–50**, so you can afford **~3 T3 potions after a single wave**. Currency doesn't scale, but neither do prices, so affordability stays roughly flat — and it's **very generous throughout**.

**Opinion:** Currency income is ~5–10× shop prices from wave 1. Either Skeleton's 5-currency yield is high (note PlaceholderGrunt gives only 1) or prices are placeholder-low.

---

## 4. Gear & Loot

### 4.1 Gear stat budget per rarity (`Assets/Data/Gear/*.asset`)

Every gear piece is a **single damage modifier (`_modifierType: 0`)**, and **the value depends only on rarity, not slot**:

| Rarity | Gear value | Artifact value | Luck bonus |
|---|---|---|---|
| Common (0) | ×1.1 | ×1.15 | 1 |
| Uncommon (1) | ×1.2 | ×1.25* | 2* |
| Rare (2) | ×1.3 | ×1.35* | 3* |
| Epic (3) | ×1.4 | ×1.45* | 4* |
| Legendary (4) | ×1.5 | ×1.55 | 5 |
| Unique (5) | ×1.6* | ×1.65* | 6* |

(* interpolated from the Common/Legendary anchors + the +0.1/tier pattern; Common & Legendary read directly.)

- **Slots are fully interchangeable** — Helm/Body/Hands/Legs/Feet all give the same value at a given rarity. So §2.4's question "do slots have different budgets?" → **no, they're identical**.
- Tier-to-tier jump is a flat **+0.1 multiplier (~+10% of base) per rarity** — *linear*, so Rare (1.3) over Uncommon (1.2) is only ~+8% relative, a small jump.
- A full 6-slot Legendary loadout = 5×1.5 + 1.55 artifact. **If multiplicative**, that's ≈ **1.5⁵ × 1.55 ≈ 11.8× damage**; if additive, far less. *I could not confirm the stacking rule from the data alone — flagged.*

### 4.2 Loot drop rates (`Assets/Data/Loot/*.asset`)

- **`LootTable_Regular`: dropChance 0.028 (2.8%)** with **all entry `_weight: 0`** ⚠️ — with every weight at zero, the weighted pick has nothing to select, so regular enemies effectively **drop nothing** even on the 2.8% roll. Looks broken / placeholder.
- **`LootTable_BossEarly`**: dropChance 1.0 (100%), 8 entries, weights `2,3,2,2,2,2,2,2` (near-uniform).
- **`LootTable_BossLate`**: dropChance 1.0, 4 entries, weights `3,3,2,1` (biased to lower tiers).

### 4.3 Luck weighting (`Assets/Data/Config/TierWeightingConfig.asset`)

`maxLuck 100, luckWeight 0.75, waveWeight 0.25, waveProgressMaxWave 20, shopStrength 4, lootStrengthMultiplier 0.25`. Shop base tier weights `[6, 3, 1]` (T1/T2/T3). So tier reweighting is **75% Luck-driven, 25% wave-driven**, and Luck's effect on *loot* is damped to 0.25× of its effect on the *shop*.

**Opinion:** The gear curve is a tidy linear +10%/tier but **rarity feels under-differentiated** (Common→Unique is only 1.1→1.6, a 1.45× total spread across six tiers — a Unique is barely 45% better than a Common). Combined with all-identical slots, gear is currently "more of the same number" rather than meaningful choices. And the **regular loot table being all-zero-weight is almost certainly a bug**, not balance.

---

## 5. Meta-Progression

- **Hero-slot unlocks at waves 15 / 30 / 50 → slots 2 / 3 / 4** (`SampleScene._heroSlotWaveMilestones = 15,30,50`; confirmed in `HeroSlotUnlockManager`).
- **Active content (TestTier) only defines ~20 distinct waves** (waves 21–29 are 9 duplicate references to one wave asset — see §7).

**Opinion / flag:** Slot 2 (wave 15) is reachable. **Slots 3 (wave 30) and 4 (wave 50) are currently *unreachable*** — there isn't enough wave content to get there. So two-thirds of the meta-progression can never be earned in the live build. Relative to the difficulty curve, wave-15 slot 2 also lands exactly on a boss + milestone wave (5.1× scaling), which is a sensible "you survived a spike → reward" moment; 30/50 just don't exist yet.

---

## 6. Cross-System Sanity Checks

**Assumptions:** 2 active heroes (dual-hero), single-target basic DPS only, no gear/upgrades for the floor case, trash enemies have 0 armor/MR (true for Skeleton & PlaceholderGrunt).

- **Combined no-upgrade basic DPS** ≈ 20 (Bolt) + ~15 (other hero) ≈ **35 DPS**, before AoE/pierce multipliers (which favor Frost/Marksman) and before ultimates/apexes.
- **Time-to-kill one trash enemy:** wave 1 (12 HP) ≈ 0.3s; wave 10 (87 HP) ≈ 2.5s; wave 20 (225 HP) ≈ 6.4s.
- With ~45 enemies/wave at wave 20 streaming in over ~45×0.2s = 9s, a 35-DPS floor **cannot** clear them (needs ~290 enemy-seconds of single-target). **Conclusion: enemy HP outruns the *un-upgraded* floor somewhere around wave 12–15.**
- **But** by then the player realistically has: line modifiers (+50% several places), gear (×1.1–1.6/slot), and 1–2 apexes (whole new abilities) — easily a 3–10× DPS multiplier. So the system **roughly keeps pace *if the player engages upgrades*,** and **massively overshoots at low waves** (everything dies in <0.5s waves 1–5).
- **Boss check:** EvilGod at wave 20 = 1800 HP, 30 armor (magical mitigation ≈ ×0.77). Bolt's Voltaic Nuke (50, cd 6) and basics chew it down over time; with upgrades/apexes it's fine, without it's a long fight. Boss **contact damage is only 5 base** (→ 45 at wave 20 after scaling) — the boss barely threatens the wall harder than trash. This is the clearest "boss isn't tuned as a boss" signal.

**Disconnected-value flags** (numbers that fit no nearby pattern): the per-wave StatMultiplier dips (§2.3), the all-zero regular loot weights (§4.2), EvilGod's currency 5 / contact 5 (§7).

---

## 7. Notable Inconsistencies — "looks like leftover test data"

Ranked by how strongly it reads as un-deliberate:

1. **Per-wave `StatMultiplier` is non-monotonic** (waves 5/6/11/20 spike, 7/12 dip below earlier waves). Difficulty literally goes *down* from wave 6→7 and 11→12. — `Wave_05..20.asset`. **Most likely hand-test values.**
2. **`LootTable_Regular` has dropChance 2.8% but all entry weights = 0** → regular enemies drop nothing. — `LootTable_Regular.asset`. **Likely a bug, not balance.**
3. **TestTier wave list padded with 9 duplicate references** to a single wave asset (entries 21–29 all the same GUID). — `TestTier.asset`. Test padding.
4. **EvilGod boss: contact damage 5 and currency reward 5** — identical to a basic Skeleton, despite 200 HP / 50 XP. A boss that hits and pays like trash. — `EvilGod.asset`.
5. **Meta-unlocks at wave 30 & 50 are unreachable** (content stops ~wave 20). — `SampleScene` vs `TestTier`.
6. **`PlaceholderGrunt` (HP 25, currency 1) is the live wave-10+ trash enemy** — a literally-named placeholder doing real duty, and its currency (1) is lower than Skeleton's (5), so income *drops* when enemies get harder. — `Wave_10/20.asset`, `PlaceholderGrunt.asset`.
7. **Two parallel "damage potion" families:** `SharpElixir/DamageElixirT2/GreaterWhetstone` (`_effectType 0`) vs `BasicDamagePotionT1–3` (`_effectType 9`), plus un-tiered names breaking the T1/T2/T3 convention. — `Consumables/`. Possible redundant/legacy set.
8. **Orphan assets:** 3 unused abilities (`BasicAutoBolt`, `Lightningbolt` [lowercase, dmg 5 / range 10], `UltimateNova`), ~2 unused upgrade lines (34 exist, 32 referenced), 1 unused apex talent (9 exist, 8 referenced), and the entire **`GameTier`** difficulty + `BossGrunt`/`Goblin` enemies. — `Abilities/`, `UpgradeLines/`, `DifficultyTiers/`.
9. **Frost Warden ultimate cd 35s** vs all other ultimates 6–18s (2–6× longer) — possibly intended (Frost Zone is strong control) but stands out hard. — `UltimateIcicle.asset`.
10. **XP front-loading:** ~level 6–7 in wave 1 from a linear `10 + 5·level` curve vs ~150 XP/wave income. — `XPManager` + `EnemyDefinitionSO`.
11. **Gear rarity spread is shallow** (Common 1.1 → Unique 1.6 = 1.45× across six tiers) and **slots are identical** — rarity & slot choice carry little weight. — `Gear/*.asset`.

### Could-not-fully-extract (flagged, not guessed)
- **Gear modifier stacking rule** (multiplicative vs additive) — not determinable from the SO values alone; changes the "full Legendary loadout" power by an order of magnitude.
- **Combo-apex magnitudes** — only `_effectType` tags read; per-combo multipliers/durations use per-combo schema fields not enumerated here (`ComboApexes/*.asset`).
- **Full per-tier numeric magnitudes of every upgrade line** — taken from each tier's `_description` (which embeds the numbers) rather than resolving all 100+ `Upgrades/*` effect assets individually; the descriptions are authoritative per the line authors but I did not cross-check each against its `_effect` SO.

---

## Summary — where the inconsistencies cluster

The **upgrade-line system (§1.3) and consumable price tiers (§3.3) look deliberately designed** (clean +15/30/50 and T1/T2/T3 patterns). Almost everything else carries test residue, concentrated in three areas:

- **Wave scaling (§2.3):** the per-wave multiplier column is the worst offender — non-monotonic, with a giant wave-10 spike from the Skeleton→PlaceholderGrunt swap.
- **Loot/gear (§4):** regular loot table is effectively disabled (all-zero weights), rarity differentiation is shallow, and slots are interchangeable.
- **Enemy/boss & meta identity (§2.1, §5, §7):** EvilGod hits/pays like trash, PlaceholderGrunt is doing real work, GameTier/BossGrunt/Goblin are dead, and 2 of 3 hero-slot unlocks are unreachable.

If you do one rebalancing pass first, my vote is the **wave `StatMultiplier` curve + the enemy roster (real mid-game enemy, real boss tuning)** — those define the difficulty the player actually feels, and they're the least "designed" right now.
