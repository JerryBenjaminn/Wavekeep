# Gear System Redesign — Analysis & Implementation Proposal (Task 66)

> Analysis only — no code or SO assets changed. Deliverable per Task 66 §4.
> Reviewed code: `GearItemSO`, `ArtifactItemSO`, `LootItemSO`, `GearSlot`, `Rarity`, `GearCatalogSO`,
> `LootTableSO`/`LootEntry`, `GearInventory`, `HeroLoadout`, `GearManager`, `GearSaveData`, `LootService`,
> consumption in `HeroRuntime`/`AbilityRuntime`/`AbilityExecutionContext`, Hub UI (`HubController`,
> `GearStatInfo`, `StatPanelController`, `LootDropHud`), and the authoring scripts `Task27GearPopulation`/
> `Task63BalanceSetup`.

---

## 0. Executive summary / verdict

The current gear layer is exactly as described: **rarity = one bigger number, nothing else.** Architecturally it is a *fixed catalog of pre-authored, identical-by-definition items* that are dropped, stacked by count, equipped by SO reference, and saved by `itemId` string. That model is clean and well-built for what it is — but it has **no concept of a per-item instance**, which is the one thing rolled affixes, rarity-upgrade, and reroll all require.

**The single dominating change is identity:** moving from *"owned = a count of a shared SO"* to *"owned = a list of unique mutable `GearInstance` objects."* Everything else (affixes, salvage, sinks, cap) is additive once that exists.

**Good news:** the combat-consumption side is already generic. `HeroLoadout` aggregates equipped items into an `IReadOnlyList<StatModifier>` that `AbilityRuntime.ComputeStats` reads through one shared switch. As long as affixes are **stat modifiers**, the entire ability/damage pipeline needs **zero changes** — affixes just become more entries in that list.

**Biggest hidden risk (flagged up front):** the §2 implicit-stat examples (*Helmet = Max HP, Body = Armor*) reference **hero defensive stats that do not exist in the current combat model.** Heroes don't take damage — enemies attack the **wall**; `HeroDefinitionSO._baseHealth` is unused at runtime (per the Task 61 audit). The only hero-side stats gear can affect today are ability **Damage / Cooldown / Range** (+ non-combat **Luck**). See §11.

---

## 1. How the current system actually works (facts)

| Concern | Current implementation |
|---|---|
| Item template | `LootItemSO` (abstract) → `GearItemSO` (carries `_slot`) / `ArtifactItemSO` (slot = Artifact). Fields: `_itemId`, `_itemName`, `_icon`, `_rarity`, `_statModifiers : List<StatModifier>`, `_luckBonus`. |
| "Stats" | A `List<StatModifier>` baked into the SO. In practice **one** modifier per item, value set purely by rarity (`Gear_*` assets authored by `Task27GearPopulation`/`Task63BalanceSetup`). |
| Identity | **The SO reference itself**, plus a stable `_itemId` string for saves. Two dropped "Rare Helmets" are *the same object*. |
| Inventory | `GearInventory` = `Dictionary<LootItemSO,int>` — **stacked counts, uncapped.** |
| Loadout | `HeroLoadout` = `LootItemSO[6]` (indexed by `GearSlot`). `Rebuild()` flattens every equipped item's `StatModifiers` into `AggregatedModifiers` + sums `LuckBonus`. |
| Equip authority | `GearManager.Equip/Unequip/Grant` — replace-not-destroy, persists after every change. |
| Persistence | `GearSaveData` JSON, `saveVersion = 1`. `owned : [{itemId, count}]`, `loadouts : [{heroId, slots:[{slot, itemId}]}]`. Resolved back via `GearCatalogSO.Find(itemId)`. |
| Catalog | `GearCatalogSO` = flat registry of every finished `LootItemSO`, indexed by `itemId`. |
| Drop flow | `LootService.OnEnemyKilled` → roll table drop chance → Luck-weighted pick of **one pre-made `LootItemSO`** from the table's entries → `GearManager.Grant` → `GearDroppedEvent`. |
| Consumption | `HeroRuntime` (run start): `_equippedModifiers = loadout.AggregatedModifiers`; passed via `AbilityExecutionContext.EquippedModifiers` into `AbilityRuntime.ComputeStats`. Read **once per run** (gear can't change mid-run). |
| Salvage / materials / cap / affixes | **None exist.** Confirmed by search — greenfield. |

**Implication:** the codebase is small and consistent (~700 lines of gear runtime + 1080 lines of Hub/stat UI), but every layer assumes *shared, immutable, stackable items*. The redesign rewrites the identity-bearing layers and leaves the combat pipeline mostly alone.

---

## 2. The core architectural pivot: `GearInstance`

Per CLAUDE.md §3.5 (SO = read-only template; mutable state in a runtime wrapper), introduce a **mutable runtime+persisted object** that is the unit of ownership, equip, salvage, and mutation.

```
GearInstance (mutable; persisted; NOT a ScriptableObject)
  instanceId      : string (GUID)        // unique identity — replaces "itemId as identity"
  baseId          : string               // → GearBaseSO (slot + implicit stat type)
  rarity          : Rarity               // current tier (upgrade-rarity mutates this)
  affixes         : List<RolledAffix>    // the rolled "modifier slots"
  // optional provenance for the "upgrade history" ask:
  originRarity    : Rarity               // what it dropped/was-crafted as
  crafted         : bool

RolledAffix
  affixId         : string               // → AffixDefinitionSO
  rolledValue     : float                // value chosen within the affix's range at roll time

GearInstance derived (read-only, computed from templates + rolled data):
  Slot            => base.Slot
  GetModifiers()  => [ base.ImplicitModifier(rarity) ] + affixes.Select(a => affixDef(a).Modifier(a.rolledValue))
  LuckBonus       => (if Luck is modelled as an implicit/affix)
  DisplayName/Icon=> from base + rarity
```

New **read-only template SOs**:

- **`GearBaseSO`** — one per slot (≈6 assets), or a few visual variants per slot. Defines `GearSlot` + the slot's **implicit stat type** + a per-rarity base magnitude (the implicit can scale gently with tier). This replaces the 36 pre-authored `Gear_*` SOs.
- **`AffixDefinitionSO`** — one per affix in the **shared pool**. Holds: id, display name, `[minValue,maxValue]` roll range, optional slot-eligibility filter, weight, and an **effect** (see §10 open-Q on stat-only vs proc). Treat as an abstract "rolled modifier slot."
- **`GearGenerationConfigSO`** (or fields on the tier/catalog) — the **rarity → affix-count** table (Common 0 … Unique fixed/hand-authored), the shared affix-pool list, and the rarity-roll weights (currently implicit in loot-table entry rarity + Luck; see §5).

`GearCatalogSO` changes role: from *"registry of finished items"* to *"registry of `GearBaseSO` + `AffixDefinitionSO`"* so a saved `GearInstance` can resolve `baseId`/`affixId` back to templates on load. (`Find(baseId)`, `FindAffix(affixId)`.)

**Why this shape:** `GearInstance.GetModifiers()` returns the same `IReadOnlyList<StatModifier>` shape `HeroLoadout` already aggregates → **combat code untouched for stat affixes**. Mutations (reroll/upgrade) edit the instance *in place* (same `instanceId`), so equip references and save references stay valid.

---

## 3. Generation flow (drop-time) — §4.2

Today `LootService.Roll` returns a finished SO. New flow builds an instance:

1. **Drop gate** — unchanged (`table.DropChance`).
2. **Pick a base/slot** — the loot table lists `GearBaseSO` entries (by slot) with weights, OR a single "any-gear" entry that picks a slot uniformly. (Today entries *are* the finished items; see §5 for the table-role change.)
3. **Roll rarity** — reuse the existing **Luck / `TierWeightingConfigSO`** weighting that currently shifts odds across listed rarities. Rarity moves from *"which finished item you picked"* to *"a roll on top of a slot."*
4. **Resolve implicit** — from `GearBaseSO` + rolled rarity.
5. **Roll affixes** — `affixCount = config.AffixCountFor(rarity)` (Common 0 … Unique fixed). Draw that many distinct affixes from the shared pool (respecting slot eligibility, no duplicate affix types), each with a `rolledValue` in range.
6. **Construct `GearInstance`** with a fresh GUID; **grant** to inventory (subject to the cap, §4); publish `GearDroppedEvent(instance)`.

> Unique = "fixed/hand-authored, no random affixes" (§2) → Unique instances pull a hand-authored affix set from the base/config rather than rolling.

---

## 4. Salvage flow + inventory cap — §4.3

**Cap (the real pressure):** `GearInventory` gains a hard `Capacity`. Owned becomes `List<GearInstance>` (no stacking — every instance is unique).

**Salvage:** `GearManager.Salvage(instanceId)` → remove from inventory → award materials (amount by rarity, optionally + affix count) → persist. Equipped instances are **not** salvageable (block, or require unequip first — recommend block for safety; an equipped item isn't in inventory anyway).

**Full-inventory handling (UX fork — flag):** when a drop arrives and `Count == Capacity`, options:
- (a) **In-run prompt**: pause-light "salvage an item or salvage the drop" pop-up. Intrusive mid-wave.
- (b) **Overflow buffer → resolve at Hub**: drop is held in a small pending list surfaced next time the Hub opens (keeps runs uninterrupted). **Recommended.**
- (c) **Auto-salvage lowest-value on full** (with a toggle). Simplest, least player agency.

Hook points: `GearDroppedEvent` consumer checks capacity; `LootDropHud` already exists for the pickup toast and is the natural place to surface "inventory full." Salvage-from-inventory is a new Hub action (§7).

---

## 5. Sink flows — §4.4 (materials in, `GearInstance` mutated, affixes always survive)

All three route through `GearManager`, spend materials, and **persist**. The locked "affixes never destroyed/risked" rule is satisfied because every operation only **adds** or **replaces a single chosen slot**, never removes or gambles existing ones.

1. **Reroll one affix** — choose an affix slot on an instance → spend materials → re-roll **only that slot's** value (or value+type, design TBD) → other affixes untouched, `instanceId` unchanged.
2. **Upgrade rarity by one step** — spend materials → `rarity++` → roll the *new additional* affix slots to match the higher rarity's count → existing affixes preserved verbatim. (Edge: upgrading **into Unique**, which is "fixed/hand-authored," needs a rule — disallow, or fill from the hand-authored Unique set. **Flag.**)
3. **Craft Artifact directly** — spend materials → create a new Artifact-slot `GearInstance` (roll or choose rarity/affixes). Implies **Artifacts are craft-only** → loot tables should **exclude** the Artifact slot from drops. **Flag** (changes drop tables' slot coverage).

---

## 6. Impact on existing systems — §4.5

| File / system | Change | Size |
|---|---|---|
| **`GearInstance`, `GearBaseSO`, `AffixDefinitionSO`, `GearGenerationConfigSO`, `RolledAffix`** | NEW | core |
| `GearInventory` | **Rewrite**: `Dictionary<LootItemSO,int>` → `List<GearInstance>` + `Capacity`. Breaking. | medium |
| `HeroLoadout` | `LootItemSO[]` → `GearInstance[]`; `Rebuild()` aggregates `instance.GetModifiers()`. **Public contract `AggregatedModifiers : IReadOnlyList<StatModifier>` stays identical.** | small |
| `GearManager` | Equip/Unequip/Grant operate on instances; **add** `Salvage`, the 3 sinks, materials balance; save v2. | large |
| `GearSaveData` | NEW v2 DTOs (instances + materials) + **v1→v2 migration** (or pre-release wipe). | medium |
| `GearCatalogSO` | Registry of **bases + affixes** (resolve `baseId`/`affixId`), not finished items. | small |
| `LootService` | `Roll` **generates an instance** (base+rarity+affixes) instead of returning a pre-made SO. Biggest drop-flow change. | medium |
| `LootTableSO` / `LootEntry` | Entries reference **bases** (slot), with rarity now a separate weighted roll. **Role change — flag vs balance ownership (Tasks 61–64 own the *weights*, this changes the *meaning* of an entry).** | medium |
| **Hub UI** (`HubController` 721 ln, Task 25 gear panel, `GearStatInfo`, `StatPanelController`, `LootDropHud`) | Inventory list → **per-instance** (no stacks); item detail shows **implicit + affixes**; add **salvage** button, **materials** counter, **3 sink screens**, full-inventory handling. **Largest surface.** | large |
| `AbilityRuntime`, `HeroRuntime`, `AbilityExecutionContext` | **UNCHANGED** for stat affixes (modifier pipeline already generic). Only **proc/status** affixes (open-Q §10.2) would need new runtime plumbing. | none / deferred |
| Authoring: `Task27GearPopulation`, `Task63BalanceSetup` gear tuning, the 36 `Gear_*`/`Artifact_*` SOs | **Obsolete** → replaced by ~6 bases + affix pool + gen config. Cleanup/migration. | medium |
| `GearDebugController` | Minor — grant/generate instances instead of SOs. | small |

---

## 7. Open questions — where each forks the architecture (§3)

**Q1 — Single "Salvage Dust" vs per-rarity shards.**
- *Single dust:* materials = one `int` in save; sink costs = one number each; one UI counter. **Simplest.** Recommended unless the design wants rarity-gated crafting economies.
- *Per-rarity shards:* materials = `Dictionary<Rarity,int>` (or 6 ints) in save; every sink cost and the salvage yield become per-rarity; UI shows 6 counters. Touches the materials data model, salvage award, every sink cost, and UI. **No code preference forced — flag both shapes are supported by the same `GearManager` materials API; only the field type differs.**

**Q2 — Stat-only vs proc/status affixes.**
- If `AffixDefinitionSO.Effect` is **just a `StatModifier`**, `HeroLoadout.GetModifiers()` feeds the existing pipeline → **no combat change.**
- If procs/status come later, they need a runtime hook analogous to the existing status-effect/`OnHit` path. **Recommendation:** make `AffixDefinitionSO` carry a small *sealed effect abstraction* (a discriminated kind, only `StatModifier` implemented now) **from day one**, so adding a proc kind later is a new branch — **not** an SO-shape migration. Cheap insurance; avoids re-rolling every saved affix later.

**Q3 — Gear affixes ↔ `UpgradeTag`/`TagInteractionRule` (§3.8).**
- Today tag interactions resolve against the run's **held `UpgradeInventory`** only. To let a hero's tag rules trigger off **equipped affixes**, the resolution must also read equipped-affix tags.
- **Recommendation:** give `AffixDefinitionSO` an optional `UpgradeTag` field **now** (cheap, data-only). Then "gear affixes feed tag interactions" is later a small change at the *resolution* site (union held-upgrade tags with equipped-affix tags) — **no data reshape.** If affixes are *not* tag-aware from the start, bolting this on later means editing every affix asset. **Architecture makes it easy iff affixes are tag-aware from day one.**

---

## 8. Locked-decision / code conflicts to flag (per instructions)

1. **Implicit stats reference a hero-stat layer that doesn't exist.** §2's *Helmet = Max HP, Body = Armor* presupposes hero **HP/Armor**. Heroes take no damage today (enemies hit the **wall**; `_baseHealth` unused — Task 61 audit). The only live hero-affecting stats are ability **Damage/Cooldown/Range** + **Luck**. **Either** the implicit map must be drawn from existing ability stats, **or** a hero defensive-stat system is a *prerequisite* task before slot implicits like Max HP/Armor mean anything. This is the most important pre-design decision. *(Exact mapping is explicitly out of scope per §2, but the dependency is not.)*
2. **Persistence migration.** Save is `v1` (`itemId`+count). `GearInstance` requires `v2`. The existing `Load` already *discards* non-current versions → without a migration, the redesign **wipes existing gear saves.** Pre-release that's likely acceptable; **confirm** (wipe-with-log vs a real v1→v2 converter that maps each old stacked item to a base+implicit instance).
3. **Loot-table semantics vs balance ownership.** Tasks 61–64 own loot **weights**; this redesign changes what an **entry means** (finished item → base/slot, with rarity as a separate roll). Coordinate so the balance weighting and the new generation roll don't double-count rarity.
4. **Artifact craft-only.** If Artifacts are crafted (§2 sink 3), drop tables must stop dropping the Artifact slot — a small but explicit data change.

---

## 9. Risk / complexity & suggested task split — §4.6

**Top risks:** (a) the identity rewrite rippling through inventory→loadout→save→Hub UI; (b) the Hub UI is the single largest surface (~1080 lines today, plus 3 new sink screens + materials + salvage + cap UX); (c) the implicit-stat/hero-stat dependency (§8.1) could spawn a prerequisite task; (d) save migration.

**Lowest risk / pleasant surprise:** combat consumption is untouched for stat affixes.

**Suggested split (implementation tasks, to be scoped after this review):**

1. **Data model + persistence v2** — `GearInstance`, `GearBaseSO`, `AffixDefinitionSO`, `GearGenerationConfigSO`, catalog-as-bases/affixes, save v2 (+ migration/wipe decision). Rewrite `GearInventory`/`HeroLoadout` to instances; keep `AggregatedModifiers` contract. *No new player-facing features yet — proves the pipeline still equips/persists.*
2. **Generation flow** — `LootService` + loot-table role: drops produce rolled instances (implicit + rarity + affixes).
3. **Inventory cap + salvage core** — capacity, materials currency (Q1 shape), `Salvage`, full-inventory handling (Q-UX).
4. **Sinks (backend)** — reroll-affix, upgrade-rarity, craft-artifact, all preserving affixes.
5. **Hub UI overhaul** — instance inventory, item detail (implicit + affixes), salvage, materials counter, 3 sink screens, full-inventory prompt.
6. *(Prerequisite/parallel, conditional on §8.1)* **Hero defensive-stat layer** if slot implicits need Max HP/Armor/etc. — *or* fold the implicit map into existing ability stats and skip this.
7. *(Optional)* Authoring tooling for bases/affixes + cleanup of the 36 obsolete `Gear_*` assets.

A reasonable first lock is **Tasks 1–2** (model + generation, no UI) so the instance pipeline is proven before the large UI investment.
