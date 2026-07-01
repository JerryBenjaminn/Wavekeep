\# Task 79 — Shop Redesign: Utility-Only Analysis \& Item Proposal



\## Instructions to Claude Code



\- Read `CLAUDE.md` in full before starting.

\- Read Task 78's analysis report before starting — this task builds directly on its findings.

\- \*\*This is an analysis and proposal task only.\*\* Do not change any values, assets, or code.

&#x20; Deliverable is a written proposal only.

\- End with a structured proposal covering the points in §2 below.



\## 1. Context



Task 78's audit confirmed that the current shop's stat-boosting potions/elixirs break progression

when stacked on top of the new gear system (Tasks 67–76) — specifically uncapped crit compounding

and flat damage added pre-gear-multiplier. The design decision is to redesign the shop as

utility-only: no direct stat boosts (no Damage%, no Cooldown, no Crit, no Speed). Shop items

affect the battlefield situation, not the hero's stats.



\*\*Locked scope for the redesign:\*\*

\- \*\*Allowed categories:\*\* wall utility (repair, protection) and arena control (slow field, stun/

&#x20; crowd control effects on enemies).

\- \*\*Explicitly excluded:\*\* any direct hero stat boost (Damage, Cooldown, Crit Chance, Crit Damage,

&#x20; Attack Speed), XP boosts, Luck boosts, Reroll Potions. These are gone entirely.

\- The shop structure itself (Currency sink, between-wave, ConsumableDefinitionSO-driven,

&#x20; ShopController) stays — only the item roster changes.



\## 2. What I need from you (deliverable)



Produce a written proposal covering:



1\. \*\*Removal list:\*\* Every current `ConsumableDefinitionSO` that must be removed under the new

&#x20;  utility-only scope, with a one-line reason per item (stat boost, Luck, XP, Reroll — whichever

&#x20;  applies).



2\. \*\*Retained items:\*\* Any current items that already fit the utility-only scope without changes

&#x20;  (Wall Repair being the obvious candidate — confirm if any others qualify as-is).



3\. \*\*Proposed new item roster:\*\* A concrete list of new utility items to fill the shop, covering

&#x20;  both allowed categories. For each proposed item include:

&#x20;  - Name and description

&#x20;  - Effect type and how it would be implemented (what existing systems it hooks into, e.g. the

&#x20;    existing status-effect system from CLAUDE.md §3.8 for slow/stun, or the existing wall HP

&#x20;    system for repair/shield)

&#x20;  - Suggested price in Currency (rough, tunable later)

&#x20;  - Whether it's a one-wave effect or persists longer

&#x20;  - Any implementation concern or dependency on existing systems



4\. \*\*Currency balance check:\*\* With stat boosts gone, does the current Currency income per wave

&#x20;  still create meaningful shop decisions, or does the economy need adjusting (too much or too

&#x20;  little Currency relative to utility item prices)? Flag but don't tune yet.



5\. \*\*Risk flags:\*\* Anything in the current `ShopController`/`ConsumableDefinitionSO` architecture

&#x20;  that would make arena-control effects (slow field, stun) harder to implement than wall-utility

&#x20;  effects — e.g. does `ConsumableDefinitionSO` support area/positional effects, or is it

&#x20;  currently built only for instant/self-applied effects?



Do not change any values, assets, or code. This is a proposal to review before any implementation.

