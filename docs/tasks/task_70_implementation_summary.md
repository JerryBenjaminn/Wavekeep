# Task 70 — Implementation Summary: Glowing Cube Loot Marker + Longer Duration

> Iteration on Task 69's arena loot-drop marker only. No generation/grant logic, no event/data changes —
> the only file touched is `Runtime/LootDropVfxController.cs` (shape/material + duration). Hook, trigger
> (`GearDroppedEvent`), pooling, and `RarityPalette` colours are unchanged.

## What changed

- **Shape replaced (beam+ring → glowing cube).** The marker is now a small **glowing cube** ("engram" style):
  a bright unlit **core cube** plus a larger, semi-transparent **glow cube** around it, both tinted by the
  rolled rarity via per-renderer `MaterialPropertyBlock` over one shared unlit material (Sprites/Default →
  unlit + alpha-blended, so it reads as glowing against the lit scene and can fade by colour alpha). Cube mesh
  comes from `GetPrimitiveMesh(PrimitiveType.Cube)`, mirroring `KineticVfxPresenter`. The old LineRenderer
  beam/ring is gone entirely (not kept alongside).
- **Idle animation:** the cube hovers above the death point (`_hoverHeight` 0.9 m), slowly **spins** on a tilted
  axis (80°/s), gently **bobs** (sine), and **pulses** scale (±8%). A per-marker random phase de-syncs
  overlapping drops. Fade-in (0.3 s) → hold → fade-out (1.0 s) via colour alpha.
- **Pooling unchanged:** markers are controller-owned (parented to the controller, never the pooled enemy),
  internally pooled/reused, deactivated on expiry — `EnemyPoolManager`-safe, no leaks/leftovers. The initial
  transform is set in `Show()` so a reused marker never renders a frame at its previous position.

## Duration chosen (and why)

- **Core (single source of truth):** `_holdSeconds = 5 s`, plus `_fadeInSeconds = 0.3 s` and
  `_fadeOutSeconds = 1.0 s` → **~6.3 s total visible window** (up from Task 69's ~1.4 s hold / the ~1 s players
  perceived).
- **Rationale:** 5 s comfortably gives the player time to register a drop mid-combat without the arena
  accumulating markers across overlapping enemy deaths — a regular drop is ~10% of kills, so at typical kill
  rates only a handful overlap, and the gentle fade-out keeps them from lingering. All values are serialized
  fields on the controller, explicitly tunable after playtesting (`_holdSeconds`, `_fadeInSeconds`,
  `_fadeOutSeconds`, plus `_cubeSize`, `_hoverHeight`, `_glowScale`, `_glowAlpha`, and the spin/bob/pulse knobs).

## Acceptance criteria
- Renders as a small rarity-coloured glowing cube instead of the beam: yes (`Marker` core+glow cubes via
  `RarityPalette.Color`).
- Visible noticeably longer than ~1 s, documented: yes (~6.3 s; see above).
- No pooling leaks/leftovers across deaths: yes (controller-owned internal pool, deactivate-on-expiry).
- No change to generation/grant/event: yes — only the marker's shape/material/duration changed.

## Notes / flags
- **Re-run `Wavekeep/Setup Task 69 (Visual Loot Drops)`** to pick up the new cube defaults. The setup
  recreates the `LootDropVfx` object fresh (`DestroyImmediate` + new), so the new code defaults (incl. the
  5 s hold) apply. No separate Task 70 setup menu was added — the marker is created entirely in code; only its
  behaviour changed. (If a scene already had a `LootDropVfx` saved with the old `_holdSeconds = 1.4`, that
  serialized value would otherwise stick — re-running the setup resets it.)
- The cube uses Sprites/Default (ZTest LEqual, ZWrite Off), so it is correctly occluded by opaque scene
  geometry (won't x-ray through walls) while still rendering over the ground.
