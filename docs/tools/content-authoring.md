# Content Authoring Tools (Tasks 15–16)

Editor-only windows for creating and editing game content without hand-wiring ScriptableObject assets
field-by-field in the Inspector. All are editor tooling only — they change no runtime/gameplay code, and
every asset they produce/edit is field-for-field equivalent to the hand-authored assets from earlier tasks.

> Open all three from the menu bar under **Wavekeep ▸ Tools**:
> - **Enemy Authoring** (Task 15)
> - **Ability Authoring** (Task 15)
> - **Wave Composition** (Task 16)

---

## Enemy Authoring — `Wavekeep ▸ Tools ▸ Enemy Authoring`

Creates a fully-populated `EnemyDefinitionSO` (and, optionally, a new `LootTableSO`) ready to drop into a
wave. No follow-up Inspector edits are required for a basic enemy.

### Fields
- **Enemy Name** — display name; also drives the asset file name (spaces/punctuation stripped).
- **Visual / Prefab**
  - *Placeholder Capsule* (default): reuses the shared placeholder capsule. Tick **Tint Capsule** and pick a
    color to auto-generate a tinted capsule prefab + URP material (saved under `Assets/Prefabs/Enemies/`).
  - *Custom*: assign your own prefab via the object field.
- **Boss Preset** — convenience toggle. Seeds tankier stats (400 HP, slow, high contact damage / rewards)
  and, in placeholder mode, uses the placeholder **boss** capsule. See "Boss note" below.
- **Base Stats** — Max Health, Move Speed, Contact Damage.
- **Rewards** — Currency Reward, XP Reward (consumed by the Task 03 kill pipeline).
- **Loot Drops**
  - *None* — drops nothing.
  - *Existing Table* — pick any existing `LootTableSO`.
  - *New Inline Table* — set an overall **Drop Chance** (0..1) and add `{item, weight}` rows. On Create this
    authors a new `LootTable_<EnemyName>.asset` in `Assets/Data/Loot/` and assigns it.

### Output
- Enemy asset → `Assets/Data/Enemies/<Name>.asset`
- Inline loot table → `Assets/Data/Loot/LootTable_<Name>.asset`
- Tinted prefab + material → `Assets/Prefabs/Enemies/<Name>.prefab` / `<Name>_Mat.mat`

### Boss note (important)
`EnemyDefinitionSO` has **no boss field** — an enemy becomes an actual boss by being referenced from a
`WaveConfigSO` (Task 10/13), which is where the boss loot table also lives. The **Boss Preset** here only
seeds stats and the boss prefab; it writes no nonexistent field. Wiring an enemy into a wave as the boss is
done in the wave config (wave-config authoring is out of scope for this tool).

---

## Ability / Upgrade Authoring — `Wavekeep ▸ Tools ▸ Ability Authoring`

A single window with a top **Ability / Upgrade** toggle. Only the fields relevant to the selected SO type
are shown, so you can't accidentally give an upgrade an ability-only field or vice-versa.

### Ability mode → `AbilityDefinitionSO` (`Assets/Data/Abilities/`)
- Name, Icon (optional Sprite).
- Base Damage, Base Cooldown (> 0), Range / AoE Radius.
- **Targeting Type** — SingleTarget or AreaOfEffect.
- **Applies Status Effects** — flag the deliberate payload ability (usually the ultimate), not a rapid basic.
- **Upgrade Levels** — add/remove rows; each row is the per-level Damage/Cooldown/Range **multiplier** on
  base (level 1 = first row). No rows = the ability runs at base (level 1).
- **Tag Interaction Rules** — add/remove rows of `{matchTag, modifierType, value}`: how this ability reacts
  when the player holds upgrades carrying that tag.

### Upgrade mode → `UpgradeDefinitionSO` (`Assets/Data/Upgrades/`)
- Name, Icon (optional).
- **Tags** — add/remove `UpgradeTag` rows. Hero abilities react to these via their tag-interaction rules.
- **Generic Effect** — Effect Type + Effect Value.
- **Status Effect on Hit** — toggle, then Status Type (Freeze/Slow/Burn), Magnitude, Duration. Delivered by
  abilities flagged *Applies Status Effects*.
- **Hero-Exclusive** — toggle and pick a `HeroDefinitionSO`. On Create, the new upgrade is **appended
  directly to that hero's `exclusiveUpgrades` list** (the pool the Task 11 level-up card picker draws from)
  — no separate manual step on the hero asset.

---

## Wave Composition — `Wavekeep ▸ Tools ▸ Wave Composition`

Views and edits a `DifficultyTierSO`'s wave sequence and each `WaveConfigSO`'s composition in one window,
instead of drilling through nested list fields in the Inspector. This is a **live editor over the real
assets** — every change is a persisted write (via `SerializedObject` + `SetDirty` + save), not a preview.

### Layout
1. **Difficulty Tier** field at the top — pick the tier to edit.
2. **Tier Settings** (edits the `DifficultyTierSO`): tier name, global stat multiplier, milestone interval/
   step, and the **tier-level** boss config — boss-wave interval, boss enemy definition, boss count.
3. **Waves list** (left): the tier's ordered waves, each showing `#position (types, total enemies)` and a
   `[BOSS]` / `[BOSS?]` tag on boss positions. Buttons per row: **▲ / ▼** reorder, **✕** remove.
   **+ Add Wave** appends a new wave.
4. **Wave detail** (right): select a wave to edit its display Wave Number, per-wave Stat Multiplier, its
   **Spawn Entries** (add/remove rows of enemy + count + interval), and — on boss positions — its
   **Boss Loot Table**.

### How boss waves work (important)
Boss designation is **positional**: the runtime spawner treats a wave's 1-based **position in the list**
(not its `Wave Number` field) as the wave number, and a wave is a boss wave when that position is a multiple
of the tier's **Boss Wave Interval** and a tier **Boss Definition** is set. So:
- The `[BOSS]` tag in the list reflects list position, and **shifts if you reorder waves**.
- `[BOSS?]` means the position is a boss slot but the tier has no Boss Definition assigned (no boss will
  actually spawn).
- The boss **enemy + count are tier-level** (shared by all boss waves); only the **boss loot table is
  per-wave** and is edited in that wave's detail panel.

### Add / reorder / remove
- **Add Wave** duplicates the previous wave's spawn entries + stat multiplier as a starting point (boss loot
  table cleared, wave number bumped), or creates a clean default wave if the tier is empty. New wave assets
  land in `Assets/Data/Waves/`.
- **Remove (✕)** *unlinks* the wave from the tier — it does **not** delete the `.asset` file (non-destructive;
  delete it manually from the Project window if you want it gone).

### Validation (non-blocking warnings)
- A wave that spawns no enemies and isn't an effective boss wave (would complete instantly).
- A spawn entry with no `EnemyDefinitionSO`.
- A boss-position wave with no Boss Loot Table assigned.

---

## Duplicate & Modify (both authoring tools)

At the top of each window is a **Template** object field with a **Load** button:

1. Drag an existing asset (enemy, or ability/upgrade) into the Template field.
2. Press **Load** to pre-fill the entire form with that asset's values (the name gets a " Copy" suffix).
3. Tweak anything you like.
4. Press **Create** — this always authors a **new** asset (unique path), never overwriting the template.

This is the fastest way to make variants (e.g. a tankier version of an enemy for a later wave, or a stronger
copy of an ability). Note: loading an upgrade template does **not** auto-enable hero-exclusive registration —
that's an explicit per-asset choice so a duplicate isn't silently re-registered onto a hero.

---

## Validation

Both windows validate inline (no blocking pop-ups). Hard errors (empty name, negative/zero-invalid stats,
hero-exclusive with no hero) show a red box and **disable the Create button** until fixed. Softer issues
(drop chance set with no loot entries, upgrade with no tags, status effect with 0 duration) show a yellow
warning but still allow Create.

---

## Verifying a created asset works

- **Enemy:** add it to a `WaveConfigSO`'s spawn list (or temporarily swap it into an existing wave) and play
  — it spawns from the pool, takes damage, dies, awards its currency/XP, and rolls its loot table.
- **Ability:** assign it as a hero's Basic or Ultimate on a `HeroDefinitionSO`, then start a run.
- **Upgrade:** if hero-exclusive, select that hero and level up — the upgrade appears in the card picker's
  candidate pool; if generic, it's part of the shared pool.
