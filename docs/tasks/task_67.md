\# Task 67 — Gear Redesign Part 1: GearInstance Data Model \& Persistence v2



> Read `CLAUDE.md` in full, and read `gear\_redesign\_001.md` (Task 66 analysis) in full before starting —

> this task implements its §2 (GearInstance pivot) and §4.1, using the locked decisions below. Don't

> re-derive the design from scratch; follow the analysis's recommended shape, but the exact types/fields

> are your call as the implementer — this file describes outcomes, not code.



\## Goal



Replace the current "owned = count of a shared, immutable gear SO" model with "owned = a unique, mutable,

per-item instance." This is the foundational pivot the rest of the gear redesign (generation, salvage,

sinks, Hub UI) depends on. No new player-facing features yet — this task proves the instance pipeline

still equips and persists correctly before any larger investment.



\## Locked decisions for this task



\- Implicit stats map only to existing live stats (ability Damage / Cooldown / Range) plus Luck — no hero

&#x20; HP/Armor/defensive stats; that layer doesn't exist and is out of scope.

\- Materials: a single shared "Salvage Dust" resource (not per-rarity). Wire up the data field for it now

&#x20; even though salvage itself is a later task.

\- Affix effects must be structured as an extensible/sealed shape with only a stat-modifier kind

&#x20; implemented now, so a future proc/status kind is a clean addition rather than a save-format migration.

\- Each affix definition should be able to optionally carry one of the existing `UpgradeTag` values

&#x20; (CLAUDE.md §3.8), even though wiring that into `TagInteractionRule` resolution is a future task.

\- Save migration: any save below the new version wipes gear data with a clear log message on load. Do

&#x20; not write a v1→v2 converter.

\- No operation introduced anywhere in the eventual gear redesign may destroy or risk existing affixes —

&#x20; keep this in mind for how you shape mutation methods now, even though reroll/upgrade land in later tasks.



\## Scope



\- Introduce a read-only template for a gear "base" per slot archetype (the slot + which existing live stat

&#x20; it implicitly boosts + how that scales by rarity). Six slots (Helmet, Body, Hands, Legs, Feet, Artifact)

&#x20; is the expected count; don't over-author beyond what's needed.

\- Introduce a read-only template for an "affix" definition: a rollable modifier with a value range, which

&#x20; slot(s) it's eligible for, a draw weight (used by generation in a later task, but the field should exist),

&#x20; and the extensible effect shape described above.

\- Introduce a small config asset that defines how many affixes a rolled item of each rarity tier gets

&#x20; (Common through Legendary scaling up; Unique fixed/hand-authored with no random rolls), and references

&#x20; the shared affix pool.

\- Introduce the actual per-item instance concept: a unique, mutable, persisted object that references its

&#x20; base template, its current rarity, and its rolled affixes, and that can compute its full set of stat

&#x20; modifiers on demand in the exact shape the combat pipeline already consumes — so `AbilityRuntime` and

&#x20; `HeroRuntime` require zero changes.

\- Update the catalog so saved instances can resolve their base/affix references back to templates on load.

\- Update inventory ownership from a stacked-count model to a list of unique instances (no cap yet — that's

&#x20; a later task).

\- Update hero loadouts to reference instances instead of shared SOs, while keeping the existing aggregated-

&#x20; modifiers contract that `HeroRuntime` reads identical.

\- Update equip/unequip/grant operations to work against instance identity.

\- Bump the gear save format version; on load of an older version, wipe gear data and log clearly why.

\- You may add a minimal debug-only way to spawn a test instance (extending the existing gear debug tooling)

&#x20; purely to verify this task's acceptance criteria — real drop generation is a separate, later task and

&#x20; should not be built here.



\## Out of Scope (do not implement)



\- Drop generation logic (rolling base/rarity/affixes at kill-time) — later task.

\- Inventory capacity cap, salvage, full-inventory handling — later task.

\- Reroll-affix, upgrade-rarity, craft-artifact sinks — later task.

\- Any Hub UI changes — later task.

\- A hero defensive-stat layer — explicitly rejected per the locked decision above.

\- A real v1→v2 save converter — explicitly rejected; wipe-with-log only.

\- Deleting the now-obsolete pre-authored gear item assets or the old gear-population authoring scripts —

&#x20; flag what becomes dead as a result of this task instead of removing it now.



\## Acceptance Criteria



\- \[ ] A gear instance can be created (via the minimal debug path), equipped to a hero slot, and its stat

&#x20;     modifiers visibly affect a test run's ability Damage/Cooldown/Range exactly as the old item-based

&#x20;     system did.

\- \[ ] Saving and reloading correctly round-trips an instance's identity, rarity, and rolled affixes.

\- \[ ] Loading a save from before this change wipes gear data cleanly, logs why, and does not throw.

\- \[ ] `AbilityRuntime`, `HeroRuntime`, and the ability execution context require no changes.

\- \[ ] No ScriptableObject template is mutated at runtime.



\## Reviewer Notes



Flag as blocking if:

\- Implicit stats reference anything other than existing live ability stats / Luck.

\- Affix effect representation isn't extensible (e.g. hardcoded to only support a stat modifier with no

&#x20; room for a future proc/status kind without a data reshape).

\- The aggregated-modifiers contract consumed by `HeroRuntime` changed shape.

\- A v1→v2 converter was written instead of wipe-with-log.

\- Affixes carry no optional tag field at all.

