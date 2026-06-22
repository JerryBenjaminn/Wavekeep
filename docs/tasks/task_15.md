# Task 15 — Content Authoring Editor Tools (Enemies & Abilities)

> Read `CLAUDE.md` in full, and review the Task 01–14 implementations before starting. This task builds custom Unity Editor tools (EditorWindow-based) for authoring new enemies and new abilities/upgrades without hand-creating and wiring multiple SO assets field-by-field in the default Inspector. This is a productivity tool for content expansion, not a gameplay system — no runtime code should change as a result of this task except where a genuine gap is found while building the tool.

## Goal

By the end of this task, you (the designer) can create a complete, correctly-wired new `EnemyDefinitionSO` (stats, prefab, loot table assignment) through one guided Editor window, and a complete new `AbilityDefinitionSO` or `UpgradeDefinitionSO` (targeting type, damage, cooldown, tags, status-effect data, tag-interaction rules) through a second guided Editor window — both significantly faster and less error-prone than manual asset creation, and both producing assets fully compatible with all existing systems (Tasks 02, 04, 09, 11, 13).

## Scope

### 1. Enemy Authoring Tool (`Scripts/Editor/EnemyAuthoringWindow.cs`)
An `EditorWindow` (menu item, e.g. `Wavekeep/Tools/Enemy Authoring`) that:
- Lets you input: name, base stats (`maxHealth`, `moveSpeed`, `contactDamage`), prefab reference (with a quick way to use the existing placeholder capsule + pick a tint color if no custom prefab exists yet), currency/XP reward values.
- Lets you assign an existing `LootTableSO` (dropdown of existing assets) or quickly author a new simple one inline (a small embedded list of `{item, weight}` entries with overall drop chance) without leaving the window.
- Optionally marks the enemy as boss-capable (if relevant fields differ for bosses per Task 10's implementation — check and accommodate whatever distinction actually exists in code).
- On "Create", generates the `EnemyDefinitionSO` asset (and a new inline-authored `LootTableSO` if applicable) in the correct project folder (per CLAUDE.md §4's existing `Assets/Data/Enemies/` convention), fully populated and ready to use — no follow-up manual field edits required for a basic enemy.
- Include a "Duplicate & Modify" flow: pick an existing `EnemyDefinitionSO` as a starting template, pre-fill the form with its values, let the designer tweak before saving as a new asset — this is likely the most common real workflow (e.g. "make a tankier version of this enemy for wave 20").

### 2. Ability/Upgrade Authoring Tool (`Scripts/Editor/AbilityAuthoringWindow.cs`)
An `EditorWindow` (menu item, e.g. `Wavekeep/Tools/Ability Authoring`) that:
- Supports creating either an `AbilityDefinitionSO` (basic/ultimate-style, per Task 04) or an `UpgradeDefinitionSO` (generic or hero-exclusive, per Task 04/11) — a toggle/tab at the top of the window switches mode and shows the relevant fields for each.
- For `AbilityDefinitionSO`: name, icon, base damage/cooldown/range, `targetingType` (SingleTarget/AreaOfEffect), upgrade-level list (add/remove rows for per-level modifiers), tag-interaction rule list (add/remove rows: matchTag + modifier type + value), and optional status-effect-on-hit data (per Task 11: type/magnitude/duration).
- For `UpgradeDefinitionSO`: name, tags (multi-select from the existing `UpgradeTag` enum), effect type/value, optional status-effect data, and — if authoring a hero-exclusive upgrade — a way to directly assign it into a chosen `HeroDefinitionSO.exclusiveUpgrades` list from within the tool, rather than requiring a separate manual step on the Hero asset afterward.
- Same "Duplicate & Modify" template flow as the enemy tool.
- On "Create", generates the asset in the correct folder (`Assets/Data/Abilities/` or `Assets/Data/Upgrades/`, matching existing convention) fully populated.

### 3. Validation & Safety
- Both tools should do basic sanity validation before allowing "Create" (e.g. non-empty name, non-negative stats, at least one loot entry if a drop chance > 0 is set, tag list non-empty for upgrades that rely on tag interactions) — simple inline warnings in the window, not blocking dialogs, consistent with a smooth authoring flow.
- Neither tool should be able to produce an asset that violates existing locked rules (e.g. an `UpgradeDefinitionSO` accidentally fillable with a rarity field that doesn't apply to it — keep each tool's fields scoped strictly to what its target SO type actually has).

### 4. Documentation
- Add a short `/docs/tools/content-authoring.md` (or similar) explaining how to open and use both windows, the "Duplicate & Modify" workflow, and where generated assets land — this is for your own future reference, not a deliverable for Claude Code's review per se, but should exist.

## Out of Scope (do not implement)
- Authoring tools for other content types (gear, consumables, wave configs, etc.) — enemies and abilities/upgrades only, for now; more tools can follow this same pattern later if useful
- Visual/art assignment beyond a basic prefab/tint reference (no sprite/model picker beyond what Unity's default object field already provides)
- In-tool playtesting/preview (e.g. simulating ability damage output inside the window) — out of scope, the tool authors data, it doesn't simulate it
- Batch/bulk creation (e.g. generating 10 enemies from a CSV) — one-at-a-time authoring is enough for now

## Acceptance Criteria
- [ ] `EnemyAuthoringWindow` creates a fully-populated, correctly-folder-placed `EnemyDefinitionSO` (and optional inline `LootTableSO`) with no required follow-up manual edits
- [ ] `EnemyAuthoringWindow` supports "Duplicate & Modify" from an existing enemy as a template
- [ ] `AbilityAuthoringWindow` creates a fully-populated `AbilityDefinitionSO` with correct targeting type, upgrade levels, and tag-interaction rules
- [ ] `AbilityAuthoringWindow` creates a fully-populated `UpgradeDefinitionSO`, including correctly registering it into a chosen hero's `exclusiveUpgrades` list when authored as hero-exclusive
- [ ] `AbilityAuthoringWindow` supports "Duplicate & Modify" from an existing ability/upgrade as a template
- [ ] Basic inline validation prevents obviously broken assets (empty name, negative stats, etc.) without being a blocking modal
- [ ] Assets created via either tool work correctly in actual gameplay with zero additional manual setup (verified by creating one new enemy and one new ability/upgrade via the tools and using them in a test run)
- [ ] `/docs/tools/content-authoring.md` exists with basic usage instructions
- [ ] No SO asset created by the tools is malformed relative to what hand-authored assets from prior tasks look like (field-for-field equivalent capability)

## Reviewer Notes
Flag as blocking if:
- A tool-created asset requires manual follow-up edits in the default Inspector to actually function correctly in gameplay (defeats the purpose of the tool)
- The tools introduce any runtime gameplay code changes beyond what's strictly needed to fix a genuine gap found while building them (this task is editor tooling, not a gameplay task)
- Hero-exclusive upgrade registration via the Ability tool doesn't actually update `HeroDefinitionSO.exclusiveUpgrades`, leaving the designer needing a manual follow-up step anyway
