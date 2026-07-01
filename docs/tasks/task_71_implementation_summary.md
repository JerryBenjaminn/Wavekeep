# Task 71 — Implementation Summary: Salvage Core + Artifact Forge

> Gear redesign part 4. Builds on Task 67 (`GearInstance`/save v2) and Task 68 (drop generation). Adds an
> inventory cap + overflow buffer, a salvage action, and a deterministic Artifact Forge; removes Artifacts
> from loot drops.

## What changed

### Data
- **`GearEconomyConfigSO`** (new) — inventory capacity, Salvage Dust yield per rarity, Forge cost per rarity.
  Read-only tuning SO (CLAUDE.md §4). Authored/wired by the Task 71 setup.
- **`GearSaveData`** — added `overflowInstanceIds` (additive, **no version bump** — see flag #1).

### Runtime
- **`GearManager`** — now takes the affix config + economy config. Adds:
  - **Inventory cap + overflow**: `Grant` routes a drop to a persisted `_overflow` buffer when the inventory is
    at `Capacity`, instead of interrupting the run or losing it (the Task 66-recommended overflow-resolved-at-Hub
    approach). `Overflow`, `Capacity`, `InventoryFull` exposed.
  - **`Salvage(instanceId)`** — removes an owned, unequipped instance (inventory or overflow) and awards Dust by
    rarity. Equipped instances live in loadouts, not inventory/overflow, so they're structurally unsalvageable.
  - **`ClaimOverflow(instanceId)`** — moves an overflow item into inventory if there's room.
  - **`ForgeArtifact(rarity)`** — deterministic: spends the rarity's Dust cost (Dust only), generates an
    Artifact-slot instance of exactly that rarity via `GearGenerator.GenerateForBase` (affixes per rarity; Unique
    → fixed set). No RNG on the result rarity. Goes straight to inventory (bypasses the drop cap).
  - Save/load round-trips the overflow buffer (ids tagged in the save; restored to the buffer, not inventory).
- **`GearGenerator`** — new `GenerateForBase(base, rarity)` for the forge; `PickBase` now **hard-skips the
  Artifact slot** so a drop can never roll an Artifact (the runtime guarantee).
- **`GameSessionBootstrap`** — new `_gearEconomyConfig` field; passes affix + economy configs to `GearManager`.

### Removing Artifacts from drops (3 layers, defense-in-depth)
1. **`GearGenerator.PickBase` skips Artifact** — the hard runtime guarantee ("never under any circumstance").
2. **Task 71 setup strips Artifact `_slotEntries`** from the three loot tables (leaving rarity weights intact).
3. **Task 68 `MigrateTable` now excludes Artifact** from the slot pool while keeping its rarity contribution, so
   re-running Task 68 won't re-add it and **rarity weights are unchanged** (Unique still drops — on a non-Artifact
   slot now). This satisfies "no change to non-Artifact drop rates."

### Debug hooks (placeholder until the Hub UI overhaul)
`GearDebugController`: **S** = salvage first owned, **F** = forge an Artifact (cycles target rarity per press),
**O** = resolve first overflow item (claim into inventory, or salvage if full). `K` now logs cap/overflow/Dust.

### Setup
New menu **`Wavekeep/Setup Task 71 (Gear Economy)`**: authors `GearEconomyConfig`, strips Artifact from the loot
tables, wires the open scene's bootstrap. Run it after Task 67/68 setups, then save the scene.

## Chosen values (flagged for later tuning)
- **Capacity = 40.** Room for a couple of runs' drops before salvage pressure; not punishing this early.
- **Salvage Dust = `[1, 2, 4, 8, 16, 32]`** (Common→Unique, doubling).
- **Forge cost = `[10, 25, 60, 140, 320, 700]`** (Common→Unique; Unique is the premium sink).
All live on `GearEconomyConfigSO`, tunable without code.

## FLAGGED — architecture concerns

1. **Save: additive field, no version bump.** `overflowInstanceIds` was added to format **v2** rather than
   bumping to v3. JsonUtility tolerates the missing field on older v2 saves (→ empty buffer), so **existing gear
   is not wiped**. This deviates from the project's "bump version → wipe" discipline; it's deliberate (a wipe
   isn't warranted for an additive field). A future *incompatible* change should still bump + wipe per Task 67.
2. **Capacity is a soft drop-gate, not a hard structural cap.** It blocks *drops* (→ overflow); deliberate
   actions — **unequip-return** and **forge** — intentionally bypass it, so `Inventory.Count` can exceed
   `Capacity`. Any future code that assumes `Count <= Capacity` would be wrong. This avoids the "can't unequip
   because full" trap.
3. **Hub scene bootstrap also needs the configs wired.** The Task 71 setup only wires the *open* scene's
   bootstrap. Salvage/forge currently run via the gameplay-scene `GearDebugController` (where the config is
   wired). When the real Hub UI lands, the **Hub scene's** `GameSessionBootstrap` must also have
   `_gearEconomyConfig` (+ `_gearAffixConfig` for forge) wired, or salvage yields 0 / forge is unavailable there.
   (The overflow buffer persists to disk, so it's visible across scenes regardless.)
4. **Existing dropped Artifacts in saves are retained.** Removal applies to *future drops only* — owned/equipped
   Artifact instances from earlier Task 68 drops are not retroactively destroyed (the locked decision removes
   them from *drops*, not from existing ownership). The Forge is the only *new* Artifact source.
5. **Forge uses a `GearGenerator` built with a null `LuckState`.** Safe today (forge rarity is chosen; affix
   *values* use `Random`, not Luck). If `GenerateForBase`/`RollAffixes` ever start reading Luck, this would NPE.
6. **Graceful degrade without configs.** No economy config → `Capacity = int.MaxValue` (uncapped) and
   salvage/forge are no-ops; older scenes keep working until Task 71 setup is run.

## Acceptance criteria
- Artifacts never drop: `PickBase` skips Artifact + tables stripped. ✓
- Hard capacity + non-disruptive full handling: cap + persisted overflow buffer (resolve at Hub/debug). ✓
- Salvage removes a non-equipped instance, awards rarity-scaled Dust: `Salvage`. ✓
- Equipped can't be salvaged: structurally (not in inventory/overflow). ✓
- Forge: player picks rarity (incl. Unique), pays scaled Dust (no currency), deterministic rarity, affixes per
  rarity: `ForgeArtifact` + `GenerateForBase`. ✓
- Forged Artifacts persist via the Task 67 save: yes (added to inventory → serialized). ✓
- No other instance's affixes altered by salvage/forge: only the one instance is removed/created. ✓
