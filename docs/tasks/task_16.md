# Task 16 — Wave Composition Editor Tool

> Read `CLAUDE.md` in full, and review the Task 01–15 implementations before starting, especially Task 02 (`WaveConfigSO`/`DifficultyTierSO`), Task 10 (milestone scaling/boss waves), Task 13 (boss loot table wiring), and Task 15 (Enemy/Ability authoring tools, for consistent tooling style). This task adds a third Editor tool: a Wave Composition window for viewing and editing `WaveConfigSO`/`DifficultyTierSO` content without manually editing list fields one row at a time in the default Inspector.

## Goal

By the end of this task, you can open one Editor window, see a `DifficultyTierSO`'s full wave sequence at a glance, click into any wave to add/remove/edit enemy spawn entries (enemy type, count, spawn interval) and boss spawn + boss loot table assignment (per Task 13), and create new `WaveConfigSO` entries — all without hand-navigating nested list fields in the Inspector.

## Scope

### 1. `WaveCompositionWindow` (`Scripts/Editor/WaveCompositionWindow.cs`)
Menu item, e.g. `Wavekeep/Tools/Wave Composition`.
- **Tier/wave overview:** pick a `DifficultyTierSO` from a dropdown; see its ordered list of `WaveConfigSO` waves as a simple scrollable list (wave number, enemy count summary, boss indicator if applicable).
- **Wave detail/edit panel:** selecting a wave from the list shows an editable panel: its enemy spawn entries (add/remove rows, each row picks an `EnemyDefinitionSO` + count + spawn interval), its `statMultiplier` override, and — if it's a designated boss wave (per Task 10) — its boss reference and `bossLootTable` (per Task 13).
- **Add new wave:** append a new `WaveConfigSO` to the selected tier's sequence, pre-filled with sensible defaults (e.g. copy the previous wave's composition as a starting point — mirrors the "Duplicate & Modify" pattern from Task 15).
- **Reorder/remove waves:** basic up/down or drag-style reordering, and removal, of waves within a tier's sequence.
- All edits write directly to the underlying SO assets (standard `SerializedObject`/`EditorUtility.SetDirty` + save pattern) — the window is a view/editor over existing data, not a new parallel data store.

### 2. Validation & Safety
- Inline warnings (non-blocking) for obviously broken configurations: a wave with zero enemy spawn entries and no boss, a spawn entry with a null `EnemyDefinitionSO`, a boss wave missing its `bossLootTable` reference.
- Consistent with Task 15's tools: warnings, not blocking modals.

### 3. Documentation
- Extend `/docs/tools/content-authoring.md` (from Task 15) with a section on this tool, or add a sibling doc — your call, keep all three tools documented in one discoverable place.

## Out of Scope (do not implement)
- Authoring tools for other content types beyond what Task 15 already covers
- Visual wave-preview/simulation (e.g. simulating spawn timing visually) — this is a data editor, not a simulator
- Changes to runtime wave logic itself (Task 02/10) — this task only adds an editor view over existing data structures; if a genuine structural gap is found while building the tool, flag and fix it minimally, same rule as Task 15

## Acceptance Criteria
- [ ] `WaveCompositionWindow` lets you view a `DifficultyTierSO`'s full wave sequence and select any wave for editing
- [ ] Enemy spawn entries (enemy/count/interval) can be added, edited, and removed per wave through the window
- [ ] Boss wave designation, boss reference, and `bossLootTable` are visible and editable for boss waves
- [ ] New waves can be appended (with a sensible default/duplicate-from-previous starting point) and existing waves can be reordered/removed
- [ ] Inline validation flags empty/broken wave configurations without blocking edits
- [ ] All edits correctly persist to the underlying `WaveConfigSO`/`DifficultyTierSO` assets (verified by editing via the tool, then confirming the same data appears in the default Inspector and in an actual playtest)
- [ ] Documentation updated per §3
- [ ] No runtime gameplay code changes beyond what's strictly necessary to fix a genuine gap

## Reviewer Notes
Flag as blocking if:
- The tool maintains its own in-memory copy of wave data that doesn't reliably sync back to the actual SO assets (edits must be real, persisted changes, not a preview that's lost on window close)
- Adding a wave through the tool produces a `WaveConfigSO` that's missing fields a hand-authored one would have
