\# Task 73 — Gear Redesign Part 5: Hub UI Overhaul (Inventory, Salvage, Artifact Forge)



> Read `CLAUDE.md` in full, and read the Task 66 analysis (`gear\_redesign\_001.md`) plus Task 67/68/71

> implementation summaries before starting. This task builds the real player-facing UI for everything

> those tasks built on the backend. This file describes outcomes, not code.



\## Goal



Replace the debug-only access to gear inventory, salvage, and the Artifact Forge with real Hub-scene UI.

This is the largest single surface in the gear redesign (per the Task 66 analysis) — take it carefully and

don't be afraid to flag UX decisions that feel like they need Jerry's input rather than guessing.



\## Locked decisions for this task



\- \*\*Hub-only.\*\* Salvage and the Artifact Forge are accessible only from the Hub scene, between runs —

&#x20; never in-run. This matches CLAUDE.md §6's existing locked decision that a separate hub/main-menu scene

&#x20; owns equip management, extended to cover salvage/forge as well.

\- \*\*Fix the Task 71 flag #3 gap as part of this task\*\*: the Hub scene's `GameSessionBootstrap` must have

&#x20; `\_gearEconomyConfig` and `\_gearAffixConfig` wired, or salvage/forge will silently no-op there.

\- Inventory display is \*\*per-instance\*\*, not stacked counts — each `GearInstance` is its own row/card

&#x20; (no two items are "the same" anymore, even if they rolled identically).

\- Item detail must show \*\*both\*\* the implicit stat and all rolled affixes — not just a single combined

&#x20; number, since that's the entire point of the redesign.

\- Salvage and Forge actions must respect everything already locked in Task 71 (equipped items

&#x20; unsalvageable, deterministic Forge rarity selection including Unique, Dust-only cost, no chance/RNG).

\- The existing overflow-buffer mechanic (Task 71) needs a real resolution UI here — surfacing pending

&#x20; overflow items so the player can claim or salvage them, rather than relying on the debug `O` key.

\- Don't change any backend logic (`GearManager`, `GearGenerator`, configs) — this task wires UI to what

&#x20; already exists. If you find a backend gap that blocks a UI requirement, flag it rather than quietly

&#x20; patching backend code as a side effect of a UI task.



\## Scope



\- \*\*Inventory view\*\*: list/grid of owned `GearInstance`s, showing at minimum slot, rarity (color-coded

&#x20; consistent with the established rarity palette), implicit stat, and affix list per item.

\- \*\*Item detail\*\*: selecting an item shows its full breakdown (implicit + affixes) clearly enough that a

&#x20; player can make an informed equip/salvage decision.

\- \*\*Equip flow\*\*: equipping/unequipping against the existing per-hero loadout, updated to reference

&#x20; instances (already supported by Task 67's `HeroLoadout` rewrite — this task just needs the UI to call

&#x20; into it correctly).

\- \*\*Salvage action\*\*: a clear way to select an inventory item and salvage it, with the Dust reward shown

&#x20; before/after confirmation. Equipped items should not be selectable for salvage at all (reinforces the

&#x20; Task 71 rule at the UI layer, even though it's already structurally enforced).

\- \*\*Artifact Forge screen\*\*: rarity selection (Common through Unique) with the Dust cost shown per option,

&#x20; clearly disabling/graying out rarities the player can't currently afford, and a confirm step before

&#x20; spending.

\- \*\*Materials counter\*\*: Dust total visible wherever it's relevant (inventory view, Forge screen at

&#x20; minimum).

\- \*\*Overflow resolution\*\*: a visible "pending items" affordance when the overflow buffer is non-empty,

&#x20; letting the player claim into inventory (if there's room) or salvage directly from overflow.

\- \*\*Full-inventory feedback\*\*: if the player is at capacity and tries to claim an overflow item with no

&#x20; room, give clear feedback rather than a silent failure.



\## Out of Scope (do not implement)



\- Reroll-affix or upgrade-rarity sinks — later task, not part of this UI pass (don't build UI for features

&#x20; that don't exist on the backend yet).

\- Any in-run access to salvage/forge/inventory management beyond whatever minimal equipped-loadout display

&#x20; already exists pre-run (e.g. hero select) — explicitly Hub-only per the locked decision above.

\- Any backend/data model changes.

\- Visual/art polish beyond functional clarity — this can be plain and functional; aesthetic pass can come

&#x20; later once the flow is validated.



\## Acceptance Criteria



\- \[ ] Hub scene's bootstrap has both gear configs wired; salvage and forge work correctly from the Hub UI

&#x20;     (verified, not just assumed from Task 71's backend tests).

\- \[ ] Player can view full inventory as individual instances with implicit + affixes visible per item.

\- \[ ] Player can equip/unequip gear per hero through the UI.

\- \[ ] Player can salvage a non-equipped item through the UI and see the Dust awarded.

\- \[ ] Equipped items cannot be salvaged through the UI (no path to attempt it).

\- \[ ] Player can open the Artifact Forge, see Dust cost per rarity tier (including Unique), and craft an

&#x20;     Artifact when affordable; unaffordable tiers are clearly indicated, not just silently failing.

\- \[ ] Pending overflow items are visible and resolvable (claim or salvage) from the Hub.

\- \[ ] Attempting to claim an overflow item while at capacity gives clear feedback instead of failing

&#x20;     silently.

\- \[ ] No backend/data logic was changed — only UI wiring to existing `GearManager`/`GearGenerator` APIs.

\- \[ ] Debug-only access (Task 71's `GearDebugController` keys) is no longer the only way to exercise these

&#x20;     features, though it can remain as a debug aid if harmless.



\## Reviewer Notes



Flag as blocking if:

\- Salvage/Forge/inventory management is reachable from anywhere other than the Hub scene.

\- Backend logic was modified instead of flagged.

\- Equipped items can be salvaged through any UI path.

\- Forge rarity selection has any chance element introduced at the UI layer.

\- The Hub scene's config-wiring gap (Task 71 flag #3) was not actually fixed.

