# Shop Redesign Proposal 001 — Utility-Only Shop (Task 79)

> **Proposal only. No values, assets, or code were changed.** Builds on Task 78 (`shop_balance_analysis_002.md`).
> Locked scope: shop becomes **utility-only** — wall utility (repair/protection) + arena control (slow/CC).
> **Excluded entirely:** any hero stat boost (Damage, Cooldown, Crit, Attack Speed), AoE-radius, Ultimate-duration,
> XP, Luck, Reroll. Shop structure (Currency sink, between-wave, `ConsumableDefinitionSO` + `ShopController`) stays.

---

## 1. Removal list

Every current `ConsumableDefinitionSO` that must go under utility-only scope. (34 assets total; 28 live in the pool.)

| Asset(s) | Effect type | Reason to remove |
|---|---|---|
| `SharpElixir`, `DamageElixirT2` (Honed), `GreaterWhetstone` | `FlatDamageBoost` | Direct hero damage stat |
| `BasicDamagePotionT1/T2/T3` | `BasicDamageBoost` | Direct hero damage stat |
| `LightningPotionT1/T2/T3` | `ElementalLightning` (flat dmg) | Direct hero damage stat (a flat-damage clone) |
| `CritChancePotionT1/T2/T3` | `CritChanceBoost` | Crit stat — **the #1 balance offender in Task 78** |
| `CritDamagePotionT1/T2/T3` | `CritDamageBoost` | Crit stat — **uncapped, top offender in Task 78** |
| `HasteTonicT1`, `HasteTonicT3`, `SwiftTonic` | `CooldownReduction` | Cooldown / attack-speed stat |
| `FrostPotionT1/T2/T3` | `FrostPotency` | Buffs the **hero's** frost ability magnitude — a hero-kit stat, not a battlefield effect the shop places |
| `BlastRadiusPotionT1/T2/T3` | `AoeRadiusBoost` | Buffs the hero's ability radius — hero stat |
| `UltimateDurationPotionT1/T2/T3` | `UltimateDurationBoost` | Buffs the hero's ultimate uptime — hero-kit stat (see judgment note) |
| `LuckPotionT1/T2/T3` | `LuckBoost` | Luck — explicitly excluded |
| `RerollPotionT1/T2/T3` | `GainRerollPoints` | Reroll — explicitly excluded |

**That is 33 of 34 assets removed.** Only `WallRepairKit` survives (§2).

- **XP boosts:** none exist — there is no XP-boost consumable/effect type, so nothing to remove there (noted for completeness).
- **Judgment call — Ultimate Duration:** not named in the excluded list, but it modifies the *hero's* ultimate, not the battlefield situation. Per the locked spirit ("affect the battlefield, not the hero's stats") I recommend **removing** it. Flagging in case you'd rather keep zone-uptime as a control tool — if kept, it's the one "borderline" retain.
- The `ConsumableEffectType` enum values themselves (`FlatDamageBoost`, `CritChanceBoost`, …) can stay in code as dead cases or be pruned later; removing the **assets** is what empties the shop. Pruning the enum + `ConsumableInventory` aggregates is optional cleanup, not required for the redesign.

---

## 2. Retained items (fit utility-only as-is)

| Asset | Effect | Why it stays |
|---|---|---|
| `WallRepairKit` | `HealWall` +100, instant, price 30 | Pure wall utility; already routes through `WallRuntime.Heal` — the exact model for the new roster. |

**Nothing else qualifies as-is.** Every other current item is a hero modifier, Luck, or Reroll. `WallRepairKit` is the only survivor and the template for the new items.

---

## 3. Proposed new item roster

Two categories. Prices are rough (see §4). "Duration" = does the effect last one wave or longer.
Implementation hooks reference the **existing** systems found in code:
- **Wall:** `WallRuntime` (`Heal`, `TakeDamage`, `MaxHP/CurrentHP`) — `HealWall` already wired end-to-end.
- **Arena:** `EnemyRuntime.ApplyStatusEffect(Freeze|Slow|Burn, magnitude, duration)` (§3.8 status machine) and the
  `GroundZone` / `GroundZoneManager` subsystem (`GroundZone.Box/Circle` + `Zones.Spawn`), which is fed
  `WaveSpawner.ActiveEnemies` each frame by `HeroRuntime`.

### A. Wall utility

| # | Name | Description | Effect / hook | Price | Duration | Concern |
|---|---|---|---|---|---|---|
| 1 | **Wall Repair Kit** *(retained)* | Instantly restore 100 wall HP. | `HealWall` → `WallRuntime.Heal` (exists) | ~30 | instant | none |
| 2 | **Reinforced Repair** | Instantly restore a large chunk of wall HP. | new tier of `HealWall` (bigger value, or % of MaxHP) | ~60 | instant | trivial — same code path, new asset |
| 3 | **Reinforced Barricade** | Wall takes **−40% damage** for the next wave. | **NEW** `WallRuntime` damage-reduction window (a timed multiplier applied inside `TakeDamage`) | ~45 | one wave | **New wall mechanic** — `WallRuntime` has no damage-reduction today (§5) |
| 4 | **Aegis Shield** | Adds a **temporary 150-HP buffer** that soaks hits before real HP; unused portion expires at wave end. | **NEW** `WallRuntime` absorb pool (drained first in `TakeDamage`) | ~50 | one wave | **New wall mechanic** — no absorb concept today (§5) |

### B. Arena control

| # | Name | Description | Effect / hook | Price | Duration | Concern |
|---|---|---|---|---|---|---|
| 5 | **Tar Field** | Spawns a slowing field across the enemy approach lane; enemies inside move **40% slower**. | `GroundZone.Box(slow=0.4)` + `Zones.Spawn` (exists — this is exactly what Frost Zone/Frozen Ground do) | ~40 | one wave (long-duration zone) | Needs shop→arena plumbing (§5). Persisting zone handles the "no enemies at purchase" timing naturally |
| 6 | **Glacial Choke** | A strong slow (or brief freeze) zone directly **in front of the wall** to blunt the final approach. | `GroundZone.Box` near the wall; freeze variant needs zone status-type = Freeze | ~45 | one wave | GroundZone currently applies **only Slow** (hardcoded) — freeze variant needs a small extension (§5) |
| 7 | **Flash Freeze** | **Freezes every enemy currently on the field** for ~3s (movement → 0). | `EnemyRuntime.ApplyStatusEffect(Freeze, 0, 3)` over `WaveSpawner.ActiveEnemies` | ~55 | instant burst | Needs enemies present → **timing** (§5): best as a "triggers at next wave start" armed effect, or shop reachable mid-wave |

**Design shape:** items 1–2 reuse the existing `HealWall` path (zero new mechanics — just new assets). Items 3–4 need
new **wall-protection** capability. Items 5–7 need shop→arena plumbing; 5–6 lean on the existing `GroundZone`
subsystem (lowest arena risk), while 7 (freeze-all) needs the active-enemy list and a timing decision.

**Suggested minimum viable set** (lowest new-code cost, covers both categories): **1, 2 (repair tiers)** + **5 (Tar
Field via existing GroundZone)** + **3 (wall damage-reduction)**. That gives repair, protection, and arena control
while only adding one new wall mechanic and one shop→GroundZone hook.

---

## 4. Currency balance check (flag, don't tune)

- **Current income is low + flat:** Skeleton/PlaceholderGrunt **1** currency, EvilGod boss **25** (Task 78). A wave
  of ~20–40 trash yields ~20–40 currency, plus 25 every 5th (boss) wave.
- **Removing the stat potions removes almost every currency sink.** Utility items are bought **reactively**
  (wall low → repair; hard wave incoming → Tar Field), so a careful player may buy little and **Currency pools up
  unspent** — the opposite of Task 78's overspend problem. The shop risks becoming a rarely-touched safety valve.
- **Meaningful-decision check:** decisions become *situational* (do I repair now or save for the boss wave?) rather
  than *stat-optimization*. That's healthier, but only if prices are low enough that reactive buys are affordable
  from the modest flat income, and high enough that you can't blanket-buy every wave. Current income likely needs a
  **modest bump or a per-wave-scaling pass** once the utility prices are set — flagged, not tuned here.
- **Stranded reroll economy:** removing Reroll Potions removes the (likely only) source of reroll points, so
  `RerollManager` / the shop's `TryReroll` button may become vestigial. Decide whether reroll stays as a feature
  (fed some other way) or is retired with the potions. Flag for the implementation task.

---

## 5. Risk flags — architecture fit for arena vs wall effects

**Wall utility is a clean fit; arena control is not, under the current shape.** Specifics:

1. **`ConsumableDefinitionSO` is built for scalar, self/target-less effects.** It has `EffectType` + a single
   `EffectValue` + `Duration` + `Price` — **no positional/area data** (no radius, no placement, no target set, no
   status-type). Wall effects need none of that (they act on the single wall). **Arena effects need area + status
   type**, so either new SO fields (radius/area/status-type) or a fixed convention (e.g. "always a full-lane band").

2. **`ShopController` has no arena reference.** Its dependencies are `CurrencyManager`, `ConsumableInventory`,
   `WallRuntime`, `RerollManager`, `LuckState`. **`WallRuntime` is already injected → wall items work today.** There
   is **no `GroundZoneManager`, no `WaveSpawner`/`ActiveEnemies`, no `HeroRuntime`** — arena items need a **new
   dependency injected** into `ShopController` and a new routing branch in `ApplyEffect`.

3. **Timing: the shop is between waves, when there may be zero active enemies.** An instant "freeze all"/"slow all"
   has nothing to act on at purchase. Two clean resolutions: **(a)** spawn a *persisting* `GroundZone` whose duration
   covers the coming wave (enemies get slowed as they enter — no enemy list needed at purchase; matches how
   Frost Zone already works), or **(b)** an "**armed for next wave**" concept that fires at wave start. (a) is the
   lower-risk path and is why items 5–6 are framed as zones rather than instant pulses.

4. **`GroundZone` applies only `Slow` (hardcoded at `GroundZone.cs:199`).** Slow fields (items 5, and 6's slow
   variant) work with the existing factory. A **freeze/stun zone** needs `GroundZone` extended to carry a
   `StatusEffectType` — a small, contained change, but a change.

5. **No `Stun` in `StatusEffectType`** — the enum is `Freeze / Slow / Burn`. **`Freeze` (movement → 0) is the de
   facto stun**: since enemies only threaten by reaching the wall, freezing movement fully neutralizes them. So "stun"
   maps to `Freeze` — no new status type needed, just be explicit that CC = Freeze. (`Burn` is damage, so I've kept it
   out of the roster to stay pure-control per scope.)

6. **`WallRuntime` supports only `Heal` + `TakeDamage`.** There is **no shield/absorb, no damage-reduction, and no
   max-HP mutation.** Repair (items 1–2) works as-is; **wall *protection* (items 3–4) is genuinely new
   capability** on `WallRuntime` (a timed damage multiplier and/or an absorb pool drained before real HP, plus per-run
   reset). Lowest-risk protection = a timed incoming-damage multiplier applied inside `TakeDamage`.

7. **Effect lifecycle mismatch.** `ConsumableInventory._activeEffects` (with `Tick`/expiry) is built for *ability
   modifiers* the `AbilityRuntime` reads each cast; wall/arena one-shots (`HealWall`) bypass it entirely. New
   **one-shot** arena/wall items (spawn a zone, apply a wall buff) similarly won't fit `_activeEffects` — their
   "duration" lives on the `GroundZone` or a new `WallRuntime` timer, **not** in `ConsumableInventory`. Only if a
   utility effect must persist and be queried later would it need inventory tracking. Keep the two lifecycles distinct.

**Summary:** wall repair (1–2) is a pure asset-authoring job (reuses `HealWall`). Wall protection (3–4) needs new
`WallRuntime` mechanics. Arena slow (5, 6-slow) reuses the mature `GroundZone` subsystem but needs a shop→`GroundZoneManager`
dependency + SO area data. Arena freeze (6-freeze, 7) additionally needs a `GroundZone` status-type extension and/or
the active-enemy list plus a between-wave timing decision. Recommend implementing in that order of increasing risk.

---

## Appendix — sources
- `Assets/Data/Consumables/*.asset`, `Assets/Scripts/Data/ConsumableDefinitionSO.cs`, `ConsumableEffectType.cs`,
  `StatusEffectType.cs`
- `Assets/Scripts/Economy/ShopController.cs` (dependencies + `ApplyEffect` routing), `ConsumableInventory.cs`
- `Assets/Scripts/Runtime/WallRuntime.cs` (`Heal`/`TakeDamage` only), `EnemyRuntime.cs` (`ApplyStatusEffect`),
  `GroundZone.cs` (Slow hardcoded; `Box/Circle` factories), `GroundZoneManager.cs`, `HeroRuntime.cs`
  (ticks zones with `WaveSpawner.ActiveEnemies`), `Assets/Scripts/Abilities/AbilityExecutionContext.cs`
- Task 78 report `docs/balance/shop_balance_analysis_002.md`; enemy currency from `Assets/Data/Enemies/*.asset`
```
