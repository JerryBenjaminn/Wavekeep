# Shop Balance Analysis 002 — Post-Gear-Redesign Audit (Task 78)

> **Analysis only. No values, assets, or code were changed.** Every number cites the asset/code it was read from.
> Companion to `audit_001.md` (Task 61). This report focuses on **what changed since that audit** and on the
> **shop ↔ gear interaction**, which audit_001 could not evaluate (persistent gear didn't exist yet).

---

## 0. What changed since audit_001 (context — don't re-report old findings)

audit_001 ran against **TestTier**. Several relevant things have moved since:

| Since audit_001 | Then (audit_001) | Now (verified this pass) |
|---|---|---|
| **Live difficulty tier** | TestTier (~20 waves), GlobalStatMult **1.2** | **GameTier** (`SampleScene._difficultyTier` guid `e68eef…`), **60 waves**, GlobalStatMult **1.0**, milestone +0.5/5, boss/5 |
| **Currency per kill** | Skeleton **5**, EvilGod **5** | Skeleton **1**, PlaceholderGrunt **1**, EvilGod **25** (`EnemyDefinitionSO._currencyReward`) |
| **Shop prices** | T1≈12–20, T2≈25–35, T3≈45–50 | **~2× higher** — T1≈28–42, T2≈33–48, T3≈50–64 (see §1) |
| **Gear model** | one damage modifier per rarity, slots identical, spread 1.1→1.6 | **Task 67–76 instance+affix model**, per-rarity affix ranges, multiplicative stacking |

So audit_001's §3.3 ("~150 currency/wave, buy everything") and §4 (gear) are **obsolete**. This report uses the **Task 76 per-rarity affix ranges + current implicit bases** as the gear baseline, as instructed.

**Gear baseline used throughout** (`GearBase_*.asset` implicits + Task 76 `Affix_*` per-rarity ranges):

| Stat axis | Gear implicit (Common→Unique) | Affix range (Common → **Legendary**), Unique fixed |
|---|---|---|
| DamageFlat | Helm `5/8/12/16/22/30`, Boots `6/10/14/18/24/32` | Sharpened `2–4 / 5–8 / 9–13 / 14–20 / **21–30**` |
| Damage× | Body `1.05/1.12/1.20/1.30/1.45/1.65` | Empowered `…/**1.31–1.45**`, Emberforged `…/**1.27–1.38**` |
| Cooldown× | Hands `0.97/0.93/0.88/0.82/0.74/0.62` | Swift `0.90–0.95 / … / **0.55–0.65**` (lower = better) |
| Range× | Legs `1.05/1.10/1.18/1.28/1.42/1.60` | Farsight `…/**1.27–1.40**` |
| Luck | Core `2/4/6/9/13/18` | Lucky `1–2 / … / **12–16**` |
| **Crit chance / Crit damage / AoE radius / Ultimate duration / Basic-only dmg** | **— none —** | **— none —** |

The last row is the crux: **several shop effects have no gear counterpart at all**, so they add axes the enemy-HP curve was never tuned against.

---

## 1. Current shop inventory (facts only)

**Live pool = 28 of 34 consumable assets** (`ShopScreenController._availableConsumables` in `SampleScene`); **offer size = 4** per visit; offer is a Luck/wave tier-weighted random subset.
**Not in the live pool (6):** `BlastRadiusPotionT1/2/3` (AoE radius), `DamageElixirT2`, `HasteTonicT1`, `HasteTonicT3`.

> **Every combat potion has `_duration: 0` → permanent for the whole run** (`IsPermanent`). Nothing is temporary.
> Almost everything is `_stackable: true` (only `GreaterWhetstone` is non-stackable). Effects **aggregate** — flat sums, cooldown multiplies (`ConsumableInventory`).

| Item | Effect (type) | Magnitude | Dur | Price | Tier | In pool? |
|---|---|---|---|---|---|---|
| Sharp Elixir | FlatDamage (all abilities) | **+10** | perm | 50 | T1 | ✅ |
| Honed Elixir (DamageElixirT2) | FlatDamage | +20 | perm | 33 | T2 | ❌ |
| Greater Whetstone | FlatDamage (all) | **+30** | perm | **150** | T3 | ✅ (non-stack) |
| Basic Attack Damage Potion I/II/III | BasicDamage (basic only) | **+5 / +10 / +15** | perm | 36 / 48 / 64 | T1/2/3 | ✅ |
| Lightning Potion I/II/III | ElementalLightning (**flat dmg, all abilities**) | **+4 / +8 / +12** | perm | 32 / 48 / 64 | T1/2/3 | ✅ |
| Crit Chance Potion I/II/III | CritChance | **+5% / +10% / +15%** | perm | 28 / 42 / 64 | T1/2/3 | ✅ |
| Crit Damage Potion I/II/III | CritDamage | **+25% / +40% / +60%** | perm | 32 / 48 / 64 | T1/2/3 | ✅ |
| Swift Tonic | Cooldown× | **×0.70** | perm | 70 | T2 | ✅ |
| Brisk / Alacrity Tonic (Haste T1/T3) | Cooldown× | ×0.85 / ×0.55 | perm | 42 / 64 | T1/T3 | ❌ |
| Frost Potion I/II/III | FrostPotency (per-stack slow) | +0.02 / 0.04 / 0.06 | perm | 32 / 48 / 64 | T1/2/3 | ✅ |
| Ultimate Duration Potion I/II/III | UltimateDuration (zone secs) | **+2 / +4 / +6 s** | perm | 32 / 48 / 64 | T1/2/3 | ✅ |
| Blast Radius Potion I/II/III | AoeRadius (metres) | +1 / +2 / +3 | perm | 30 / 47 / 62 | T1/2/3 | ❌ |
| Luck Potion I/II/III | LuckBoost (non-combat) | +5 / +10 / +15 | perm | 20 / 35 / 50 | T1/2/3 | ✅ |
| Reroll Potion I/II/III | GainRerollPoints | +1 / +2 / +3 | instant | 30 / 40 / 50 | T1/2/3 | ✅ |
| Wall Repair Kit | HealWall | +100 HP | instant | 30 | T1 | ✅ |

**Damage pipeline order** (`AbilityRuntime.ComputeStats` + `RollCrit`), which determines how everything below stacks:

```
dmg = BaseDamage × lineTierMult
    → tag-interaction & role upgrades
    → + FlatDamageBoost + ElementalLightning (+ BasicDamageBoost if basic)   ← CONSUMABLES (added)
    →   × CooldownMult (cooldown) ; + AoeRadius (range)
    → × each gear DamageMultiplier ; + each gear DamageFlatBonus              ← GEAR (applied LAST)
    → × (1 + Σ CritDamage)  when a crit-chance roll (Σ CritChance, clamped 1.0) succeeds   ← CONSUMABLES (final)
```

Two structural facts fall straight out of this: **(a)** consumable flat damage is added *before* the gear multipliers, so it gets multiplied by them; **(b)** crit is the *final* multiplicative step and crit-damage is **uncapped**.

---

## 2. Power comparison vs gear (per consumable)

| Consumable | Same-stat gear reference | Verdict |
|---|---|---|
| **Sharp Elixir +10** | ≈ Rare Sharpened (9–13) / Common-Uncommon Helm implicit | Fair per unit; **permanent + stackable + global** is the problem, not the number |
| **Greater Whetstone +30** | = a **max-roll Legendary** Sharpened (30) — but it's one purchase, non-stackable | Single Legendary affix worth of flat, buyable at wave 1 for 150. Borderline but non-stackable = self-limiting |
| **Basic Dmg Potion +15 (T3)** | Legendary Sharpened is 21–30 — but this only affects the **basic** | On a base-8–10 basic, +15 ≈ **+150–190% before any multiplier**. Large relative to base |
| **Lightning Potion +12 (T3)** | ≈ Rare Sharpened; **functionally identical to FlatDamageBoost** | 🚩 **A third redundant flat-damage family** stacking with Sharp + Whetstone on the same additive term |
| **Damage MULTIPLIER** | Gear: Body 1.45 + Empowered/Emberforged affixes | ✅ **No consumable competes here** — good, the shop stays off the strongest gear axis |
| **Swift Tonic ×0.70** | Hands 0.62 + Swift affix 0.55–0.65 (Legendary) | 🚩 **Multiplies** with gear cooldown → ×0.7·0.62·0.6 ≈ **×0.26 (≈4× fire rate)**; stackable ×0.49 alone |
| **Crit Chance +5/10/15%** | **No gear crit** | 🚩🚩 Full T1+T2+T3 set = **+30%/run**; ~3–4 sets (or fewer with Bolt's Overcharge) → **100% crit** |
| **Crit Damage +25/40/60%** | **No gear crit** | 🚩🚩🚩 **Uncapped**, additive then applied as `×(1+Σ)` final step. One set = **×2.25**; multiple sets → unbounded |
| **Ultimate Duration +6s (T3)** | No gear equivalent | 🚩 On a 5–6s damage/control zone this ~**doubles uptime**; stackable → near-permanent zones |
| **Frost Potency +0.06** | No gear equivalent (Frost hero only) | Minor CC buff; low concern |
| **Luck +15 (T3)** | Core implicit 2–18, Lucky affix 1–16 | Non-combat, but **feeds the spiral** (better shop tiers → more crit/flat potions) |
| **Wall Repair +100 / Reroll +1–3** | n/a | Not damage, but **enablers** (see §5) |

---

## 3. Stacking analysis (combined maxima)

Because of the pipeline order, the shop's flat and crit effects **compound multiplicatively with gear and each other**. Worked example — **Bolt Striker basic, base 10**, mid/late run with Epic–Legendary gear:

| Step | Contribution | Running damage |
|---|---|---|
| Base × maxed line (~×2.0) | line tiers | 20 |
| + flat consumables (Sharp 10 + Whetstone 30 + Lightning 12 + Basic 15) | **+67** | 87 |
| + gear flat (Helm ~22 + Boots ~24 + 2× Sharpened affix ~25) | +71 | 158 |
| × gear multipliers (Body 1.45 × Empowered 1.4 × Emberforged 1.35 ≈ **×2.74**) | gear | ~433 |
| × crit (100% chance, **one** crit-dmg set → ×2.25; more sets → higher) | **crit** | **~975** |

A base-**10** basic reaching ~**1,000/hit**, with the shop responsible for two of the biggest jumps:
- **Flat potions are added pre-gear-multiplier**, so their +67 is itself multiplied ~2.74× → they contribute ~**+185** post-gear, then ×crit.
- **Crit is a flat ~2.25× (one set) to unbounded (many sets) multiplier on the whole total**, with **no gear equivalent to price it against**.

Meanwhile GameTier enemy HP scales by the milestone curve only: `×(1 + floor(wave/5)·0.5)` = **×5 at wave 40**, **×7 at wave 60** (`GameTier.asset`). Player damage from the shop alone grows far faster than that over ~40–60 permanent-stack shop visits. **The shop scales super-linearly with run length; enemy HP scales linearly with wave.** That gap is exactly the wave 40–60 breakdown.

**Worst combined stacks:**
1. **Crit chance → 100% + uncapped crit damage** = every hit at an unbounded multiplier (🚩🚩🚩).
2. **Three flat-damage families** (FlatDamageBoost + ElementalLightning + BasicDamageBoost) summing on a tiny base, then multiplied by gear (🚩🚩).
3. **Cooldown: SwiftTonic ×0.7 × gear ×0.62 × affix ×0.6 ≈ ×0.26** fire-rate, i.e. ~4× DPS on top of per-hit damage (🚩).

---

## 4. Price / value assessment

- **Prices roughly doubled since audit_001** and **currency income was cut** (Skeleton 5→1). Early-game affordability is now genuinely tight (~a handful of trash = 1 currency each; wave-1 ≈ ~30 currency vs a T3 potion at 50–64).
- **But the real throttle is the offer structure, not price:** 4 items/visit, each buyable **once per offer** (`_purchasedThisOffer`). So a visit caps at ~4 purchases regardless of wealth. This *does* create meaningful per-visit choice now — a real improvement over audit_001's "buy everything."
- **The reroll loop defeats it, though.** `TryReroll → GenerateOffer` **clears `_purchasedThisOffer`**, so after a reroll the *same* item can be re-bought this visit. Reroll Potions grant reroll points cheaply (30–50 currency for +1–3). Net: **currency → reroll points → unlimited re-buys of the single best potion (Crit Damage) in one visit.** Over 60 waves this converts abundant late-game currency directly into unbounded crit/flat stacks.
- So: prices create choice **only if items stay stackable-but-scarce**; the reroll re-fish + permanent stacking make late-game currency effectively unlimited buying power for the strongest effect.

---

## 5. Progression impact — primary culprits for the wave 40–60 spike

Ranked by contribution to the over-clear:

1. **Crit Damage potions (uncapped `×(1+Σ)` final multiplier, no gear counterpart).** The single biggest lever — permanent, stackable, and applied on top of *everything* including gear multipliers. This alone can 2–4×+ all damage.
2. **Crit Chance potions.** Enable #1 by pushing crit to 100% (cheap, and Bolt's Overcharge upgrade helps get there), so the uncapped crit-damage multiplier applies to *every* hit rather than occasionally.
3. **The three overlapping flat-damage families** (Sharp Elixir + Greater Whetstone + Lightning Potions + Basic Damage Potions). Permanent, stackable, added *before* gear multipliers on tiny base damage → their value is multiplied by the strongest gear axis.
4. **Swift Tonic cooldown reduction.** Multiplies with two gear cooldown sources → ~4× fire rate; stackable makes it worse.
5. **Enablers:** **Reroll Potions** (bypass the once-per-offer cap to mass-buy the best potion), **Luck Potions** (accelerate reaching high-tier crit/flat offers), **Wall Repair Kit** (removes the only fail pressure — the wall — so an over-tuned board never punishes the player).

Root cause common to all: **every combat potion is `duration 0` = permanent for the run and (except Whetstone) stackable.** They were designed as *temporary* run-bonuses (CLAUDE.md §1) but are authored as **permanent, unbounded, compounding** buffs. Over a 60-wave run that turns the shop into a second, uncapped gear system layered on top of the real one.

---

## 6. Recommendations (for Jerry to review — nothing implemented)

**Highest impact / lowest risk (data-only, flags already exist):**
1. **Cap crit.** Clamp total crit-damage bonus (e.g. ≤ +100–150%) the way crit *chance* is already `Clamp01`. `ConsumableInventory.TotalCritDamageBonus()` is uncapped today — this is the top offender.
2. **Make the damaging potions non-stackable per run** (`_stackable = false`, already used by Greater Whetstone). Applying it to Crit Chance/Damage, the flat-damage family, and Swift Tonic caps each to **one purchase per run**, removing the unbounded compounding without touching magnitudes. Biggest bang-for-buck lever.
3. **Restore finite durations.** Give combat potions a real `_duration` (e.g. a few waves) so they buff *a stretch of the run*, not the whole run — this is the original "temporary run-bonus" intent and directly removes the super-linear scaling.

**Structural / medium:**
4. **De-duplicate flat damage.** Lightning Potion is a functional clone of FlatDamageBoost; Sharp/Whetstone/DamageElixir/Basic overlap heavily (audit_001 §7.7 flagged two families). Collapse to one flat-damage line, or differentiate (e.g. Lightning → a real element later).
5. **Apply flat consumable damage *after* gear multipliers** (or convert to a % bonus) so it isn't multiplied by the strongest gear axis. Code-side change to `ComputeStats` ordering — flag for a separate task, not a value tweak.
6. **Close the reroll re-buy loop.** Track "purchased this *visit*" across rerolls (not just per offer), or make items non-stackable (#2 makes this moot). Prevents converting currency → reroll points → unlimited copies.
7. **Cooldown stacking.** Make cooldown consumables non-stackable and/or cap total CDR, since they multiply with two gear cooldown sources.

**Lower priority:**
8. **Ultimate Duration:** cap total added seconds (or make non-stackable) so zones can't approach permanent uptime.
9. **Wall Repair Kit:** fine to keep, but note it removes fail pressure — revisit alongside boss/wall tuning, not the shop pass.
10. **Cleanup (not balance):** the 6 pool-excluded items (Blast Radius ×3, DamageElixirT2, both Haste Tonics) are orphaned/legacy — decide keep-and-wire or delete.

**Suggested first move:** apply **#1 (cap crit damage) + #2 (non-stackable on crit + flat + cooldown potions)** together. That neutralizes the unbounded compounding with pure data edits (no magnitude guesswork), then re-playtest wave 40–60 before deciding whether durations (#3) or pipeline ordering (#5) are still needed.

---

## Appendix — sources
- `Assets/Data/Consumables/*.asset` (magnitudes, prices, tiers, `_duration`, `_stackable`)
- `Assets/Scripts/Data/ConsumableEffectType.cs`, `ConsumableDefinitionSO.cs`
- `Assets/Scripts/Economy/ConsumableInventory.cs` (aggregation: flat sums, cooldown product, crit-chance `Clamp01`, crit-damage uncapped), `ShopController.cs` (offer draw, once-per-offer, reroll clears it)
- `Assets/Scripts/UI/ShopScreenController.cs` (`_availableConsumables` pool of 28, `_offerSize` 4)
- `Assets/Scripts/Runtime/AbilityRuntime.cs` — `ComputeStats` (pipeline order) + `RollCrit`
- `Assets/Data/Gear/Bases/GearBase_*.asset` (implicits), `Assets/Data/Gear/Affixes/Affix_*.asset` (Task 76 per-rarity ranges)
- `Assets/Data/DifficultyTiers/GameTier.asset` (60 waves, ×1.0 global, milestone +0.5/5), `Assets/Data/Enemies/*.asset` (currency/xp), `SampleScene.unity` (`_difficultyTier` → GameTier)
```
