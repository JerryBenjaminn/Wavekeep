\# Task 69 — Gear Redesign Part 3: Visual Arena Loot Drops + End-of-Run Summary Panel



> Read `CLAUDE.md` in full, and read `gear\_redesign\_001.md` (Task 66 analysis) plus the Task 67 and Task 68

> implementation summaries before starting — this task is purely presentational and must not change how

> gear is generated or granted (that logic already exists from Tasks 67–68). This file describes outcomes,

> not code.



\## Goal



Replace the current text-toast loot notification with a physical, color-coded visual drop in the arena

when an enemy dies and rolls gear — similar in spirit to looter-shooter conventions (e.g. Destiny). The

drop is purely visual; the actual instance grant to inventory still happens the same way it does today (no

pickup interaction on the arena floor). At the end of a run, show a summary panel listing every piece of

gear that dropped during that run.



\## Scope



\### 1. Arena visual drop

\- When an enemy death results in a gear roll, spawn a brief visual marker in the arena at (or near) the

&#x20; enemy's death position, color-coded by the rolled rarity tier:

&#x20; - Common = grey, Uncommon = green, Rare = blue, Epic = purple, Legendary = orange, Unique = red.

\- The visual should read clearly at the existing fixed 3/4 top-down camera angle (CLAUDE.md §3.7) and at

&#x20; whatever scale is legible against enemy/arena models — a simple recognizable shape (e.g. a light beam,

&#x20; glow, or icon-on-ground) is preferable to something that competes visually with combat readability.

\- The marker is timed/transient (appears, holds briefly, fades) — it does not need to persist or be

&#x20; collected; the grant already happened the moment the enemy died, same as today.

\- Must work correctly with `EnemyPoolManager` — triggered per death event, no leaks/persistence issues

&#x20; across pooled enemy reuse, and no interference with existing death VFX/animation (Task 54 etc.).

\- Hook this to the existing `GearDroppedEvent` (or equivalent drop-resolution point) — no new parallel

&#x20; drop-detection path.



\### 2. Remove the existing text toast

\- Remove the current `LootDropHud` text-toast notification entirely — this visual system replaces it, it

&#x20; does not supplement it.



\### 3. End-of-run summary panel

\- At run end (victory or defeat — whichever existing run-end flow already fires, e.g. `OnRunEnded`), show

&#x20; a panel listing every gear instance that dropped during that run, grouped or sorted by rarity, with

&#x20; enough info per item to be useful (slot, rarity, at minimum — implicit/affix detail if cheap to include,

&#x20; but don't block this task on richer item-detail UI work that belongs to the later Hub overhaul).

\- This is a new screen/panel in the existing run-end flow, not a replacement of any other run-end content

&#x20; (e.g. don't remove existing victory/defeat stats if they exist — add to that flow).



\## Out of Scope (do not implement)



\- Any change to gear generation, rolling, or granting logic (Tasks 67–68 own this; this task only

&#x20; visualizes what already happened).

\- Pickup/collection interaction on the arena floor — explicitly rejected; grant remains automatic/backend.

\- Inventory cap, salvage, sinks, or Hub UI overhaul — later tasks.

\- Rich per-item detail UI (full affix breakdown, tooltips, comparison) in the summary panel beyond basic

&#x20; slot/rarity info — later Hub UI task can build on this if needed.



\## Acceptance Criteria



\- \[ ] Every enemy death that rolls gear produces a correctly-colored visual drop marker in the arena

&#x20;     matching the rolled rarity, with no pickup interaction required or possible.

\- \[ ] The old text-toast notification no longer appears anywhere.

\- \[ ] The visual drop system works correctly through `EnemyPoolManager` pooling with no leaks or visual

&#x20;     leftovers across multiple casts/deaths in the same run.

\- \[ ] At run end, a summary panel correctly lists every gear instance that dropped during that run.

\- \[ ] No change to what gear drops, how rarity/affixes are rolled, or how instances are granted to

&#x20;     inventory — verified identical to pre-task behavior aside from the visual presentation layer.



\## Reviewer Notes



Flag as blocking if:

\- Any drop-generation or grant logic was touched or duplicated instead of purely visualizing the existing

&#x20; event.

\- A new parallel drop-detection path was introduced instead of hooking the existing drop-resolution event.

\- The arena visual is implemented as a pickup-able object rather than a purely visual marker.

\- The summary panel removes or replaces existing run-end content rather than adding to it.

