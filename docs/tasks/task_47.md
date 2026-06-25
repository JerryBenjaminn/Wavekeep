\# Task 47 — High-Impact VFX for Apex \& Combo Apex Talents



> Read `CLAUDE.md` in full, and review Task 45/46 (Basic/Ultimate VFX for both heroes, as the baseline to exceed),

> Task 29/31/35 (apex talent mechanics: Remorseless Winter, Permafrost Eruption, Thunderstorm, Lethal Surge), and

> Task 38 (Frozen Lightning combo apex) before starting. Apex talents are the strongest abilities in the game by

> design and currently have weaker visual presence than basic attacks, which undersells them — this task makes

> every apex/combo apex visually unmistakable as a major event, clearly exceeding the intensity of Task 45/46's

> effects.



\## Goal



Every apex and combo apex trigger should be immediately noticeable even mid-combat — bigger shapes, brighter

flashes, and an added sense of weight (e.g. brief screen shake or hit-stop) that Basic/Ultimate effects

intentionally don't have, so the player always knows when an apex fires without having to look for it.



\## Scope



\### 1. Shared "Apex Impact" Treatment

\- Establish a reusable intensity layer applied to all five talents in this task (on top of each one's unique

&#x20; visual, see below) so apex triggers share a consistent "this is a big deal" feel:

&#x20; - Brief screen shake on trigger (small magnitude, short duration — should read as impactful, not disorienting

&#x20;   or repeated so often it becomes annoying given apex cooldowns of 8-11s).

&#x20; - A brief hit-stop / time-dilation flicker on trigger (a few frames of slowed time), if feasible without

&#x20;   disrupting other systems' cooldown/timer accuracy — flag if this risks interfering with other timing systems

&#x20;   and propose a screen-flash-only alternative if so.

&#x20; - Effects noticeably larger in scale and brighter/more saturated than the corresponding hero's Basic/Ultimate

&#x20;   effects from Task 45/46.



\### 2. Frost Warden Apex VFX

\- \*\*Remorseless Winter\*\*: the automatic freeze pulse should have a clear "casting" tell, a more dramatic

&#x20; ice-burst on the frozen target (larger and more elaborate than Hard Freeze's normal stun visual) — make it

&#x20; obviously the hero's signature move, not a bigger version of a basic hit.

\- \*\*Permafrost Eruption\*\*: the large AoE burst should visually fill its full radius clearly (e.g. an expanding

&#x20; ring/shockwave reaching the edge of its area, rather than a localized flash), making the area of effect legible

&#x20; at a glance.



\### 3. Bolt Striker Apex VFX

\- \*\*Thunderstorm\*\*: the combined chain+multi-strike burst should visually read as a small storm — multiple

&#x20; bright bolts converging/jumping in quick succession, distinct from a merely-bigger Lightning Bolt.

\- \*\*Lethal Surge\*\*: the finishing strike should have a clear "execution" visual — e.g. a sharp, heavy flash with

&#x20; a brief dramatic pause beforehand, especially when it benefits from Static Charge stacks or the Execute bonus

&#x20; (consider scaling visual intensity with current stack count/execute-bonus state, mirroring how Task 46 scaled

&#x20; Multi-Strike's hit count to its tier).



\### 4. Frozen Lightning (Combo Apex)

\- Since this is "the strongest upgrade in the game" (Task 38) and a synergy between both heroes, its visual

&#x20; should clearly read as combining both heroes' color languages — e.g. a frost-blue freeze moment immediately

&#x20; followed by a gold lightning strike landing on the same primed target, visually telling the story of the combo

&#x20; rather than just being a bigger version of either hero's effect alone.

\- Since Frozen Lightning is a passive synergy (no own cooldown, per Task 38), this VFX triggers specifically on

&#x20; the moment Lethal Surge consumes a primed target — confirm this hooks into that exact trigger point, not a

&#x20; separate timer.



\## Out of Scope (do not implement)

\- Any change to apex talent damage, cooldowns, or unlock conditions — VFX only

\- Audio

\- Any change to Basic/Ultimate VFX from Task 45/46 (those stay as the baseline this task exceeds, not as

&#x20; something to also rework)



\## Acceptance Criteria

\- \[ ] All five talents (Remorseless Winter, Permafrost Eruption, Thunderstorm, Lethal Surge, Frozen Lightning)

&#x20;     have visuals clearly more intense than the corresponding hero's Basic/Ultimate effects

\- \[ ] Shared screen-shake/hit-stop treatment applies consistently across all five without feeling repetitive or

&#x20;     disruptive given their cooldowns

\- \[ ] Permafrost Eruption's visual clearly communicates its actual AoE radius

\- \[ ] Frozen Lightning's VFX visually combines both heroes' color languages and triggers exactly on Lethal

&#x20;     Surge's primed-target consumption, not a separate timer

\- \[ ] No change to any talent's damage, cooldown, or unlock logic

\- \[ ] Full playtest: trigger each of the five talents in a run, confirm each is immediately noticeable mid-combat

&#x20;     and clearly reads as more powerful than a basic attack



\## Reviewer Notes

Flag as blocking if:

\- Any apex/combo apex visual is not clearly distinguishable in intensity from Basic/Ultimate effects

\- Screen shake/hit-stop is implemented in a way that affects gameplay timing/cooldown accuracy

\- Frozen Lightning triggers on a new independent timer instead of Lethal Surge's actual prime-consumption moment

