# Task 68 — Implementation Summary

> Drop-generation flow (rolled `GearInstance` at kill-time). This file records implementation notes,
> findings, and any deviations flagged for Jerry.

---

## Pre-work finding: what the gear "Range" stat actually does

**Verdict: AMBIGUOUS — Range is referenced in multiple inconsistent ways.** It is neither cleanly
"targeting distance, always-satisfied" nor cleanly "AoE/burst radius." What the gear
`GearStatType.RangeMultiplier` (→ `AbilityModifierType.RangeMultiplier`) scales depends entirely on the
ability's `TargetingType`, and it is **dead for the majority of abilities** while **live for a minority**.

Per the investigation instructions, this is the "flag the ambiguity, don't pick an interpretation" case.

### How it's consumed (code path)

`AbilityRuntime.ComputeStats` produces one `range` out-param. `RangeMultiplier` modifiers (gear, tag rules,
consumables) all scale this single value. But the *base* it scales and the *meaning* of the result differ
by targeting type (`AbilityRuntime.cs:1484-1487`, then the per-mode `Execute*` consumers):

```
radiusBase = (TargetingType == TargetedAreaOfEffect) ? Definition.AoeRadius : Definition.Range
range      = radiusBase * level.RangeMultiplier   // gear/tag/consumable mods layer on after
```

### Arena geometry (to judge "always-satisfied")

From the scene setup scripts: wall at `z=0`, enemy spawn line at `z=20`, spawn markers spread `x=±12`
(`Task02SceneSetup.cs`), caster/hero at `z≈-3` (`Task05SceneSetup.HeroSpawnPosition`, used as
`CasterPosition = transform.position` in `HeroRuntime.BuildContext`). **Farthest possible enemy from the
caster ≈ √(23² + 12²) ≈ 26m.** Any acquisition range ≥ ~26 covers the whole arena → the gate never fails.

### Per-targeting-type breakdown (all 16 authored abilities)

| Targeting type (assets) | What `range` (the gear-scaled value) is | Base values authored | Gear `RangeMultiplier` effect |
|---|---|---|---|
| **SingleTarget** (Lightning Bolt, Voltaic Nuke, Remorseless Winter, Thunderstorm, Lethal Surge, Fireball, Executioner's Volley) | acquisition radius (`Definition.Range`) | 28–100 (all ≥ 26m arena reach) | **DEAD** — reach is already saturated; scaling it up changes nothing |
| **AreaOfEffect, instant** (Cataclysm, Permafrost Eruption, Wildfire Apocalypse) | caster-centred blast radius (`Definition.Range`) | 4–6m (smaller than arena) | **LIVE** — meaningfully grows the AoE |
| **TargetedAreaOfEffect** (Frost Bolt Burst — the Frost Warden basic) | impact **blast** radius (`Definition.AoeRadius`); the *cast distance* is read raw from `Definition.Range` and is a separate, always-satisfied gate | blast 2.5m / cast 28m | **LIVE** — grows the blast (but the field literally named `Range`, 28, is the dead one; the live value is `AoeRadius`) |
| **AreaOfEffect, zone** (Frost Zone, Firewall) | n/a — these short-circuit before targeting (`AppliesZonePayload`/`AppliesFireWall`) and size off `AoeRadius` + arena depth; `range` is computed but unused | range 100 (ignored) | **DEAD** |
| **PiercingLine** (Marksman Shot, Minigun, Bullet Storm) | shot line length (`Definition.Range`) | 28–100 (already spans arena depth) | **DEAD** — corridor already reaches the spawn line |

### Why this is worse than a cleanly dead stat

It is **inconsistent and unpredictable**, not uniformly inert like the Task 61 hero-HP finding:

- For the bread-and-butter SingleTarget abilities (every hero's primary single-target damage, including most
  basics and several ultimates/apexes) and for all PiercingLine/zone abilities, `RangeMultiplier` does
  **nothing**.
- For three instant-AoE apexes and the one Frost basic, it's a **real AoE-size buff**.

So a player equipping a Range item gets a stat whose value swings between "inert" and "modest AoE buff"
purely based on which hero/ability they happen to run — with no in-UI signal of which they're getting.

### This already shipped in Task 67's authored data

`RangeMultiplier` is **not** hypothetical — Task 67 (`Task67GearSetup.cs`) already authored it into live
content:

- **`base_legs` (Greaves)** — the **Legs slot implicit** is `GearStatType.RangeMultiplier`.
- **`affix_farsight` (Farsight)** — a shared-pool affix, `RangeMultiplier` 1.05–1.25×, eligible for all slots.

Both are therefore inert on most builds today.

### Recommendation (final call is Jerry's)

Following the "dead/always-satisfied" branch of the investigation instructions, because Range is inert for
the **majority** of abilities and inconsistent where it's live:

1. **Do not use `RangeMultiplier` as an implicit or affix going forward in the gear redesign.** For Task 68's
   generation + any base/affix authoring, restrict implicits to **Damage (flat/×) and Cooldown (×)**, plus
   **Luck** for the Artifact slot — the stats that are unambiguously live for every hero.
2. **Re-map the Legs slot implicit** (currently `RangeMultiplier`) to a live stat — simplest is another
   Cooldown or Damage variant so all six slots carry a stat that actually does something. The `affix_farsight`
   affix should likewise be dropped or re-pointed. *(Flagging — not changing it in this pass without sign-off.)*
3. **Flag `GearStatType.RangeMultiplier` / `AbilityModifierType.RangeMultiplier` as a candidate for removal
   or repurpose in a future task.** The clean fix, if Range is wanted as a gear stat, is to **split the
   conflated field** into two independently-named, independently-scaled stats:
   - `AcquisitionRange` (SingleTarget/PiercingLine reach) — currently always-saturated; would only matter if
     base reaches were tightened below ~26m, or
   - `BlastRadius` / `AoeSize` (the AoE/TargetedAoE radius) — the value that's actually live today.
   A gear stat could then target the live one honestly. **This is a design decision, not mine to make.**

**Net for Task 68:** treat Range as effectively dead for implicit/affix purposes; author Damage/Cooldown
(+Luck) only; flag the existing Legs/Farsight usages and the underlying enum for Jerry's future cleanup.

> Note: Task 68 (below) does NOT author any new Range usage. It only *migrates the existing tuned data*, so
> the pre-existing `base_legs` (Range implicit) and `affix_farsight` (Range affix) carry through unchanged —
> they remain flagged above, not introduced here.

---

## Implementation — drop generation flow

### What changed

- **`GearGenerator`** (new, `Assets/Scripts/Gear/GearGenerator.cs`) — the real drop generator. `TryGenerate(table)`:
  1. drop-chance gate (unchanged), 2. weighted **slot/base** pick, 3. **rarity** roll reusing the *same*
  `LuckState.LootTierMultiplier` step the old path used (normalised across the table's listed rarity span — no
  second weighting model), 4. affix roll: `GearAffixCountConfigSO.AffixCountFor(rarity)` distinct, slot-eligible
  affixes drawn by `DrawWeight` without replacement, each value `Random.Range(min,max)`. **Unique** skips rolling
  and takes the base's hand-authored fixed set. Returns a fresh `GearInstance` (implicit resolved by the instance
  itself from base+rarity).
- **`LootService`** — reduced to a thin subscriber: hands the kill's table to `GearGenerator`, grants via
  `GearManager.Grant(GearInstance)` (persists, Task 67), publishes `GearDroppedEvent`.
- **`LootTableSO`** — new shape: `_slotEntries` (`LootSlotEntry` = base + weight) and `_rarityWeights`
  (`LootRarityWeight` = rarity + weight), rolled independently. Legacy `_entries`/`Entries`/`TotalWeight` **kept
  but flagged DEAD as a runtime source** (so Task 13/27/63 setups + the Enemy authoring window still compile, and
  so the Task 68 setup can derive the new weights from the tuned legacy ones).
- **`GearBaseSO`** — new `_uniqueAffixes : List<FixedAffix>` (hand-authored fixed Unique set; new `FixedAffix` type).
- **`GearDroppedEvent`** — now carries the generated `GearInstance` (was `LootItemSO`). `LootDropHud` reads
  `.Rarity`/`.ItemName` unchanged.
- **`GameSessionBootstrap`** — new `_gearAffixConfig` field, passed into `LootService` (null ⇒ implicit-only drops).
- **`Task68DropGenerationSetup`** (new editor menu: *Wavekeep/Setup Task 68 (Drop Generation)*) — derives each
  table's `_slotEntries`+`_rarityWeights` from its legacy entries (aggregating marginal slot and rarity weights),
  authors the Unique fixed-affix sets on the 6 bases, and wires the bootstrap's affix config.

### Acceptance criteria — how each is met
- Fresh `GearInstance` with slot + rolled rarity + correct implicit (instance computes it) + config-matching affix
  count: `GearGenerator.TryGenerate` + `RollAffixes`.
- No duplicate affix type / slot-eligible only: draw-without-replacement over the slot-filtered pool.
- Unique → fixed/hand-authored set: `BuildUniqueAffixes` reads `GearBaseSO.UniqueAffixes`.
- Rarity uses the existing Luck-weighted approach (no second model): ports the old `LootService` weighting verbatim.
- Granted via `GearManager` + persists: `GearManager.Grant(GearInstance)`.
- `GearDroppedEvent` still fires with the instance.

### FLAGGED deviation (per task instructions — not silent)
**Boss tables lose their slot×rarity JOINT correlation.** The old tables baked specific (slot, rarity) pairs;
the new shape rolls slot and rarity **independently**. Consequences:
- **Regular** preserves *exactly* — it was already a clean grid (5 gear slots × {Common 12, Uncommon 5, Rare 2,
  Epic 1}), which is a perfect product of marginals.
- **BossEarly / BossLate** preserve the **marginal** slot distribution and **marginal** rarity distribution
  exactly (so overall drop-rate + rarity feel is held), but specific old pairings can now cross — e.g. BossLate
  previously guaranteed Unique only on the Artifact and Legendary only on Feet; now a Unique can land on any of
  that table's listed slots. This is intrinsic to the "rarity is a separate roll" model the task mandates and is
  not separately fixable without re-introducing per-(slot,rarity) entries. The drop *rates* were not re-balanced.

### Coordination note (balance ownership, Tasks 61–64)
The Task 68 setup **derives** the new weights from the legacy entries, so it re-syncs to whatever the balance
tasks tuned. If a balance pass edits the legacy `_entries` weights later, **re-run "Setup Task 68"** to
regenerate `_slotEntries`/`_rarityWeights`. (The legacy `_entries` are the editable source of truth until the
authoring tools are migrated to author the new shape directly — a later cleanup.)

### Now-dead as a result (flagged, not removed — per scope)
- `LootTableSO._entries` / `Entries` / `TotalWeight` and `LootEntry` — no longer read at runtime (only a setup
  derivation source + legacy authoring).
- The 36 finished `Gear_*`/`Artifact_*` `LootItemSO` assets and `GearCatalogSO.Items` remain dead ownership
  sources (already flagged in Task 67); they now also serve only as the legacy weight-derivation source.

### Setup required
Run **Wavekeep/Setup Task 67** (if not already), then **Wavekeep/Setup Task 68 (Drop Generation)** from the
gameplay scene, and save the scene.
