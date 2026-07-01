# Task 77 — Implementation Summary: Developer Tool — Full Progression Reset

> Editor-only developer convenience. Adds a menu command that wipes all persistent cross-run save data back to a
> true first-launch state. No gameplay code, no save-format change, no SO/project-settings change, no player-facing UI.

## What was added
- **`Task77ProgressionReset`** (`Assets/Scripts/Editor/Task77ProgressionReset.cs`) — menu item
  **`Wavekeep/Debug/Reset All Progression`**. Shows a confirmation dialog, then deletes every persistent save file,
  and logs a clear summary of what was deleted / already absent / failed.

## Investigation: every persistent data source (all cleared)
I searched the whole codebase for `persistentDataPath`, `PlayerPrefs`, `WriteAllText/WriteAllBytes`, and save-file
constants. The project persists cross-run state **only** as three JSON files under `Application.persistentDataPath`,
each constructed in `GameSessionBootstrap` from its manager's `DefaultSaveFileName` constant:

| Data source | File | Contents |
|---|---|---|
| `GearManager` | `gear_save.json` | Owned gear instances, per-hero loadouts, Salvage Dust (Task 67 v2 format) |
| `HeroSlotUnlockManager` | `hero_slot_unlocks.json` | Persistent hero-slot unlock ceiling + wave milestones (Tasks 42 / 61–64) |
| `TalentDiscoveryManager` | `talent_discovery.json` | Discovered apex/combo codex (Task 43) |

**No `PlayerPrefs` usage exists anywhere** (verified — zero `PlayerPrefs.` references), so nothing is cleared there.
A blanket `PlayerPrefs.DeleteAll()` was deliberately **not** used, since it would risk unrelated Unity editor keys and
there are no game keys to clear. In-run state (Currency, XP, current wave) is per-run and never persisted — nothing to reset.

## Design notes
- The tool reuses the runtime `DefaultSaveFileName` constants (not string literals), so if a save file is ever
  renamed in its manager, the reset tool stays correct automatically.
- Pure file IO → works in **Edit or Play mode** and doesn't need the game running.
- Deleting (not rewriting) the files means the managers hit their "no save found → fresh state" path on next load.
- **Play-mode caveat (logged, not code-changed):** if run while playing, an already-loaded `GameSession` still holds
  its state in memory and may re-save on the next change. The console log tells the developer to stop/restart Play
  (or relaunch) for the wipe to take full effect. Adding a live in-memory reset would require modifying gameplay
  managers, which is out of scope ("no gameplay code modified").
- Confirmation dialog guards against accidental clicks; the action is irreversible.

## Acceptance criteria
- `Wavekeep/Debug/Reset All Progression` appears in the Unity menu. ✓
- Wipes gear instances, loadouts, Dust, hero unlocks, talent discovery — every found persistent source (listed above). ✓
- After running, a fresh launch = no gear, default hero slot only, zero Dust (managers load-empty when files are gone). ✓
- Clear Console log confirms what was wiped (deleted / already-absent / failures + location). ✓
- No gameplay code, save format, or SO asset modified. ✓ (new editor file only)

## Usage
Run **`Wavekeep/Debug/Reset All Progression`**, confirm the dialog, then (if it was run during Play mode) restart Play
mode. Check the Console for the "full progression reset complete" summary.
