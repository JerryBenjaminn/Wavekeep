\# Task 71 — Gear Redesign Part 4: Salvage Core + Artifact Forge



> Read `CLAUDE.md` in full, and read the Task 66 analysis (`gear\_redesign\_001.md`) plus Task 67/68

> implementation summaries before starting. This task adds inventory cap + salvage, and introduces the

> Artifact Forge as the salvage-material sink. This file describes outcomes, not code.



\## Goal



Two things, shipped together because they're economically linked:

1\. Add a hard inventory cap and a salvage action that converts unwanted gear into Salvage Dust.

2\. Make Artifacts fully craft-only via a new "Artifact Forge": remove the Artifact slot from loot drops

&#x20;  entirely, and let the player spend Dust to craft an Artifact of a rarity they choose.



\## Locked decisions for this task



\- \*\*Artifacts are removed from loot drops entirely, retroactively.\*\* Task 68's loot tables currently

&#x20; include the Artifact slot as droppable — fix this now as part of this task's scope (not a separate

&#x20; task). Artifacts can only be obtained via the Forge from this point forward.

\- \*\*Materials remain a single shared Salvage Dust resource\*\* (per Task 67) — do not introduce per-rarity

&#x20; shard tiers. This was explicitly reconsidered and rejected in favor of keeping Task 67's existing model.

\- \*\*Salvage yield scales with the salvaged item's rarity\*\* (Common → small amount, Unique → large amount;

&#x20; exact curve is your call, document it).

\- \*\*Artifact Forge is deterministic, not chance-based:\*\* the player explicitly picks the target rarity

&#x20; (Common through Unique, including Unique — at a higher cost) and pays a Dust cost that scales with that

&#x20; chosen rarity. No RNG on the crafted result's rarity.

\- \*\*Forge cost is Dust only\*\* — there is no persistent gold/currency to spend alongside it (Currency is

&#x20; run-scoped only per CLAUDE.md §2, it does not persist between runs, so it cannot be a Forge cost).

\- Crafted Artifact instances roll affixes the same way a dropped instance would for their chosen rarity

&#x20; (via `GearGenerationConfigSO` from Task 67) — Unique crafts pull the fixed/hand-authored affix set, same

&#x20; as a Unique drop would.

\- Equipped instances cannot be salvaged (must be unequipped first, or simply aren't selectable from

&#x20; equipped state — your call on UX, but block the action either way).

\- Salvaging/crafting must never destroy or risk affixes on \*other\* items — this only ever affects the one

&#x20; instance being salvaged or the new instance being created.



\## Scope



\### 1. Remove Artifact from loot drops

\- Update the loot table data/generation logic from Task 68 so the Artifact slot is never selected as a

&#x20; drop outcome. Document this change in the implementation summary — it's a direct retroactive fix to

&#x20; Task 68's output, not new generation logic.



\### 2. Inventory cap

\- Add a hard capacity limit to gear inventory ownership (a specific number is your call — pick something

&#x20; that creates real pressure without feeling punishing this early; flag your choice for later tuning).

\- Decide and implement a full-inventory handling approach for when a new drop arrives at capacity. The

&#x20; Task 66 analysis recommended an overflow-buffer-resolved-at-Hub approach to avoid interrupting runs

&#x20; mid-wave; that's the preferred default unless you find a strong reason otherwise mid-implementation

&#x20; (flag if so).



\### 3. Salvage action

\- Add a `Salvage(instanceId)` action that removes the instance from inventory and awards Dust based on its

&#x20; rarity.

\- Surface this as whatever minimal interaction point makes sense given there's no Hub UI overhaul yet

&#x20; (debug tooling is fine for now if a real UI hook doesn't already exist to attach to — flag what you did

&#x20; and note it'll need real UI in the Hub overhaul task).



\### 4. Artifact Forge

\- Add a Forge action: player selects a target rarity, pays the corresponding Dust cost, and receives a

&#x20; newly generated Artifact-slot `GearInstance` of that rarity (implicit + affixes rolled per Task 67's

&#x20; generation rules for that rarity).

\- Define the cost curve per rarity (your call, document it — should feel meaningfully more expensive at

&#x20; higher tiers given Unique is now achievable this way).

\- Same minimal-interaction-point note as above applies — debug tooling acceptable for now if no real UI

&#x20; exists yet to hook into.



\## Out of Scope (do not implement)



\- Reroll-affix or upgrade-rarity sinks for non-Artifact gear — later task.

\- Any Hub UI overhaul beyond the minimal hook points needed to exercise this task's acceptance criteria.

\- Per-rarity shard tiers — explicitly rejected.

\- Chance/RNG on Forge output rarity — explicitly rejected.

\- Any change to non-Artifact loot table drop rates — only the Artifact slot's removal is in scope.



\## Acceptance Criteria



\- \[ ] Artifact-slot items no longer appear as possible loot drops under any circumstance.

\- \[ ] Gear inventory enforces a hard capacity; a documented, non-disruptive flow handles what happens when

&#x20;     a drop arrives at capacity.

\- \[ ] Salvaging a non-equipped instance removes it from inventory and awards Dust scaled to its rarity.

\- \[ ] Equipped instances cannot be salvaged.

\- \[ ] The Artifact Forge lets the player choose a target rarity (including Unique), spends the correct

&#x20;     scaled Dust cost with no currency/gold involved, and produces a correctly-generated Artifact

&#x20;     `GearInstance` of exactly that rarity with no RNG on the outcome rarity.

\- \[ ] Forged Artifacts persist correctly through the Task 67 save system.

\- \[ ] No existing instance's affixes are altered by any salvage or forge operation.



\## Reviewer Notes



Flag as blocking if:

\- Artifacts can still drop from loot.

\- Forge output rarity has any chance/RNG component.

\- Forge cost includes any persistent currency that doesn't actually exist in the game.

\- Per-rarity shard tiers were introduced instead of single Dust.

\- Salvaging an equipped item is possible without unequipping first.

