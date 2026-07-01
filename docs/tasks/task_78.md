\# Task 78 — Shop Balance Analysis (Post-Gear-Redesign Audit)



\## Instructions to Claude Code



\- Read `CLAUDE.md` in full before starting.

\- Read this whole task file before doing anything.

\- Read the Task 61 audit (`audit\_001.md`) for context on what was already found and fixed in the

&#x20; previous balance pass — don't re-report already-known issues, focus on what has changed since then.

\- \*\*This is an analysis task only.\*\* Do not change any values, assets, or code. Deliverable is a

&#x20; written report only.

\- End with a structured report covering the points in §2 below.



\## 1. Context



Playtesting after the full gear redesign (Tasks 67–76) shows progression breaking down around wave

40–60 when Epic gear is available: the player can trivially clear all 60 current waves. Shop items

(potions/elixirs) are suspected to be a significant contributor — they were designed and tuned before

any persistent gear system existed, so their power level has never been evaluated alongside gear

modifiers stacking on top. The specific concern is that Shop items may be overtuned to the point where

they actively harm progression pacing rather than supporting it.



\## 2. What I need from you (deliverable)



Produce a written report covering:



1\. \*\*Current shop inventory:\*\* List every `ConsumableDefinitionSO` currently in the shop with its

&#x20;  effect type, magnitude, duration, and price. Don't editorialize yet — just the facts.

2\. \*\*Power comparison vs gear:\*\* For each consumable, estimate how its effect magnitude compares to

&#x20;  what the gear system can currently provide for the same stat (e.g. if a potion gives +30% Damage

&#x20;  and a Legendary gear affix gives +20% Damage, flag that as a red flag). Use the per-rarity affix

&#x20;  ranges from Task 76 as the reference baseline.

3\. \*\*Stacking analysis:\*\* Identify any cases where a shop item's effect stacks with a gear modifier

&#x20;  for the same stat — note the combined maximum and whether that combined value looks problematic.

4\. \*\*Price/value assessment:\*\* Do current prices create meaningful choices, or is Currency trivially

&#x20;  abundant enough that the player can buy everything available between waves?

5\. \*\*Progression impact:\*\* Based on the above, give your honest assessment of which specific

&#x20;  consumables (if any) are the primary culprits for the wave 40–60 power spike, and why.

6\. \*\*Recommendations:\*\* Concrete tuning suggestions (magnitude reductions, price increases, duration

&#x20;  nerfs, or removal candidates) with brief reasoning for each. Don't implement anything — just

&#x20;  state what you'd change and why, so Jerry can review and approve before any values are touched.



Do not change any values, assets, or code. This is a planning document only.

