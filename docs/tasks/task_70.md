\# Task 70 — Visual Loot Drop Iteration: Glowing Cube + Longer Duration



> Read `CLAUDE.md` in full, and read the Task 69 implementation summary before starting — this task

> iterates on Task 69's visual drop marker only. No generation/grant logic, no event/data changes. This

> file describes outcomes, not code.



\## Goal



Playtesting found the current arena loot-drop visual (a pillar/beam shape) doesn't read well and disappears

too quickly (\~1 second). Replace the visual with a small glowing cube, color-coded by rarity exactly as

before, and extend how long it remains visible so players have a reasonable window to notice it.



\## Locked decisions for this task



\- Rarity color mapping is unchanged: Common = grey, Uncommon = green, Rare = blue, Epic = purple,

&#x20; Legendary = orange, Unique = red.

\- This replaces the pillar/beam shape entirely — not an additional visual alongside it.

\- The marker is still purely visual, same as Task 69 (no pickup interaction, grant already happened on

&#x20; enemy death).

\- Hook point, trigger event, and pooling requirements are unchanged from Task 69 — this task only touches

&#x20; the marker's shape/material and its visible duration.



\## Scope



\- Replace the current pillar/beam visual with a small glowing/emissive cube, styled similarly to the

&#x20; looter-shooter loot-drop convention referenced in Task 69 (e.g. Destiny-style glowing engram cube) —

&#x20; simple shape, clearly colored, readable at the existing fixed 3/4 top-down camera angle.

\- Increase the marker's visible duration from the current \~1 second to a noticeably longer window — pick a

&#x20; duration that comfortably gives the player time to register it without cluttering the arena across

&#x20; multiple overlapping enemy deaths (your judgment on the exact value; flag what you chose and why in the

&#x20; implementation summary so it can be tuned further after playtesting).

\- Keep whatever fade-in/fade-out or idle animation feels appropriate for a glowing cube (e.g. gentle

&#x20; rotation, pulse, or bob) — purely visual polish, your call, consistent with not competing with combat

&#x20; readability.

\- Continue to work correctly through `EnemyPoolManager` with no leaks or leftovers across multiple

&#x20; deaths/casts in the same run, same as Task 69's requirement.



\## Out of Scope (do not implement)



\- Any change to drop generation, rarity rolling, or grant logic (Tasks 67–68 own this).

\- Any change to the `GearDroppedEvent` hook point or trigger logic.

\- Pickup/collection interaction — still explicitly not a thing.

\- Run-end summary panel — unaffected by this task.



\## Acceptance Criteria



\- \[ ] Arena loot drops now render as a small glowing cube, correctly colored by rarity, instead of the

&#x20;     previous pillar/beam shape.

\- \[ ] The marker remains visible for a noticeably longer duration than before (\~1s), with the chosen

&#x20;     duration documented in the implementation summary.

\- \[ ] No leaks or visual leftovers across multiple pooled enemy deaths in the same run.

\- \[ ] No changes to drop generation, grant logic, or the underlying drop event.



\## Reviewer Notes



Flag as blocking if:

\- Generation/grant/event logic was touched.

\- The new visual is implemented as a pickup-able object rather than purely visual.

\- Duration change introduces pooling leaks (e.g. cubes not properly returned/cleared between deaths).

