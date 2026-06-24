\# Task 25 — Gear Detail Panel (HUB Scene)



> Read `CLAUDE.md` in full, and review Task 12 (gear/artifact data, equip slots, persistence) and Task 14 (hub

> scene, equip-management UI) before starting. This task adds an inspection panel to the existing hub scene so

> players can see what a piece of gear actually does before deciding to equip it.



\## Goal



Clicking a gear/artifact item — either an unequipped item in the inventory list or an item currently equipped in

one of the six slots — opens a detail panel on the right side of the hub scene showing that item's full

information. Comparison against the currently equipped item in the same slot is handled separately in Task 26;

this task covers the panel itself plus equip/unequip actions.



\## Scope



\### 1. Panel Layout and Content

\- New UI panel docked to the right side of the existing hub scene layout, alongside the current inventory/equip UI

&#x20; from Task 14.

\- Panel displays, for the selected item:

&#x20; - Icon — reserve a UI Image slot bound to a sprite reference field on the gear/artifact SO; no actual artwork is

&#x20;   required yet, a visible placeholder (e.g. blank/default sprite) is fine until real icons are added later.

&#x20; - Item name

&#x20; - Rarity (matching the existing six-tier rarity display convention already used elsewhere in the hub/inventory)

&#x20; - Full list of the item's stat bonuses (existing stat fields plus `luckBonus` from Task 24), each as a labeled row

\- Each individual stat row has a hover tooltip giving a short plain-language description of what that stat does

&#x20; (e.g. hovering the Luck row explains its effect on shop/loot tier odds). Tooltip text can be authored as a small

&#x20; static lookup keyed by stat type, not per-item.



\### 2. Equip / Unequip Actions

\- Panel includes Equip and Unequip buttons.

\- Equip button is active when the selected item is an unequipped inventory item; pressing it equips the item into

&#x20; its designated slot using the existing equip logic from Task 12/14 — no new equip code path, this is a new entry

&#x20; point into the existing flow.

\- Unequip button is active when the selected item is currently equipped; pressing it unequips using the existing

&#x20; unequip logic.

\- After an equip/unequip action, the panel should refresh to reflect the item's new state (e.g. button states

&#x20; swap) rather than closing, since the same item is still the one being inspected.



\### 3. Selection Behavior

\- Panel is closed/empty by default when the hub scene loads (no grid-based inventory exists yet to define a

&#x20; sensible "first item," per Task 14's current list-based layout — revisit auto-opening the first item once

&#x20; inventory becomes grid-based, as a future task).

\- Clicking an item (inventory or equipped slot) opens the panel with that item's details, or updates it if the

&#x20; panel is already open with a different item.

\- Clicking the same already-selected item again may close the panel (toggle) — implementer's choice on exact

&#x20; toggle behavior, document what was chosen.



\## Out of Scope (do not implement)

\- Actual icon artwork — sprite reference wiring only, placeholder visuals are fine

\- Stat comparison against the currently equipped item (Task 26)

\- Grid-based inventory layout or auto-opening the first item on scene load

\- Any change to gear/artifact persistence or save format



\## Acceptance Criteria

\- \[ ] Clicking any inventory item or any equipped slot opens the detail panel with that item's icon placeholder,

&#x20;     name, rarity, and full stat list

\- \[ ] Each stat row shows a hover tooltip with a short description of that stat's effect

\- \[ ] Equip button correctly equips an unequipped item via the existing Task 12/14 equip flow; Unequip button

&#x20;     correctly unequips via the existing flow; no new parallel equip/unequip logic introduced

\- \[ ] Panel state (button availability, displayed info) updates correctly after an equip/unequip action without

&#x20;     requiring the panel to be reopened

\- \[ ] Panel is closed by default on hub scene load

\- \[ ] No SO asset is mutated at runtime



\## Reviewer Notes

Flag as blocking if:

\- Equip/Unequip buttons call new logic instead of routing through the existing Task 12/14 equip/unequip flow

\- Stat values shown in the panel are approximated/hardcoded instead of read live from the item's SO fields

