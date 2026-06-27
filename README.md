# Wavekeep (working title)

Tower/Wave-defence + hero roster + Vampire-Survivors-style skill upgrades.
Engine: **Unity 6**. Targets **PC and mobile**, landscape only.

See [`CLAUDE.md`](CLAUDE.md) for architecture, conventions, and locked design decisions, and [`docs/tasks/`](docs/tasks/) for the sequential task log.

## Getting started

1. Open the project in Unity 6.
2. Import the Synty asset packages (see below) — they are **not** in version control.

## Third-party assets (not in version control)

The **Synty** assets used for 3D models, HUD, and animations live under `Assets/Synty/` and are intentionally git-ignored (see `.gitignore`) because of their size. They are **not** included in a fresh clone, so you must import them yourself from your Synty Store / package source after cloning.

Packages currently in use:

- `AnimationSwordCombat`
- `InterfaceCore`
- `InterfaceFantasyWarriorHUD`
- `PolygonFantasyRivals`
- `PolygonGeneric`
- `SyntyPackageHelper`
- `Tools`

Without them, scenes referencing Synty prefabs/materials will show missing references and pink (missing-shader) materials until the packages are re-imported.
