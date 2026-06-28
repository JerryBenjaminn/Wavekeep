\# Task 66 — Gear System Redesign: Analysis \& Implementation Proposal (NO IMPLEMENTATION YET)



\## Instructions to Claude Code



\- Read CLAUDE.md in full before starting.

\- Read this whole task file before doing anything.

\- \*\*This is an ANALYSIS task, not an implementation task.\*\* Do not write or modify any code or SO assets. Your deliverable is a written proposal (markdown) covering the points in §4 below.

\- Read the existing gear-related code and data thoroughly: `GearDefinitionSO` (or equivalent), equip slot logic, loot table / drop generation, the hub/equip-management scene, and the persistence/save layer for gear.

\- Don't expand scope into adjacent systems (e.g. don't redesign LootTable\_Regular weights — that's balance work, already handled separately in Tasks 61-64).

\- Flag any ambiguity or any locked decision below that seems to conflict with existing code, rather than guessing.

\- End with a structured implementation summary (proposal, not code changes).



\## 1. Context



Wavekeep currently has a basic persistent gear system (six equip slots: Helmet, Body, Hands, Legs, Feet, Artifact; six rarity tiers Common→Unique with flat stat multipliers per CLAUDE.md §6). Playtesting confirms the core wave-defence/balance loop works for 1-2 heroes, but the gear layer itself is currently flat and uninteresting (rarity = bigger number, nothing else), and there is no salvage/disposal system for unwanted drops.



\## 2. Design Direction (locked decisions so far)



These are confirmed and should NOT be re-litigated — analyze implementation against them, don't propose alternatives to these specific points:



\- \*\*Gear identity via implicit stats + affixes:\*\*

&#x20; - Each equip slot has a fixed \*\*implicit stat type\*\* (e.g. Helmet = Max HP, Body = Armor, etc. — exact mapping TBD, not part of this task) regardless of rarity.

&#x20; - Each item additionally rolls a number of \*\*affixes\*\* from a shared pool, with affix \*count\* scaling by rarity tier (e.g. Common = 0 affixes, Unique = fixed/hand-authored, no random affixes).

&#x20; - \*\*Affix content/types (stat-only vs. proc/status effects) are explicitly NOT decided yet\*\* — out of scope for this task. Treat affixes as an abstract "rolled modifier slot" system architecturally; do not assume a specific affix data shape beyond "a list of modifier references with a count driven by rarity."

\- \*\*Salvage system:\*\* unwanted gear can be salvaged into a materials currency ("shards" or similar — exact resource model TBD, see open question in §3). Salvaging is the primary disposal path; there is no plain "delete."

\- \*\*Sinks for salvage material (at least these three, conceptually):\*\*

&#x20; 1. Reroll a single affix on an owned item.

&#x20; 2. Upgrade an owned item's rarity tier by one step.

&#x20; 3. Craft Artifact-slot items directly (Artifacts may not need to be drop-based at all).

\- \*\*Affix/rarity-upgrade safety:\*\* rerolling or upgrading rarity \*\*never destroys or risks existing affixes\*\* — no failure chance, no affix loss. This is a locked decision (no roguelite risk mechanic here).

\- \*\*Inventory pressure:\*\* gear inventory will have a \*\*hard slot cap\*\*, which is the actual pressure that forces salvage-vs-keep decisions (not a free/unlimited inventory).

\- \*\*Persistence:\*\* all of this still respects the existing locked decision that gear/artifact ownership and loadouts persist between runs (CLAUDE.md §6).



\## 3. Open Questions (explicitly NOT decided — do not assume an answer, just flag where each choice would affect your proposed architecture)



\- Single shared "Salvage Dust" resource (amount scales with rarity) vs. separate material tiers per rarity (Common Shard, Rare Shard, etc.) — flag where this choice would change your data model, but don't pick one.

\- Whether affixes are stat-only (flat/%) or will later include proc/status effects — flag where this affects whether your proposed `AffixDefinitionSO`-equivalent needs to be effect-type-generic from day one vs. can start stat-only and extend later.

\- Whether/when gear affixes should hook into the existing `UpgradeTag` / `TagInteractionRule` system from CLAUDE.md §3.8 (so a hero's tag-interaction rules could trigger from equipped gear affixes, not just in-run upgrades) — flag this as a design fork point with a recommendation on whether your proposed architecture makes this easy or hard to bolt on later, but don't implement it now.



\## 4. What I need from you (deliverable)



Produce a written proposal covering:



1\. \*\*Data model:\*\* What new/changed types are needed (e.g. a mutable `GearInstance` runtime class vs. the current static `GearDefinitionSO`-only model), respecting CLAUDE.md §3.5 (SO = read-only template, mutable state lives in a runtime wrapper). Show how rolled affixes, current rarity tier, and upgrade history would be represented and persisted.

2\. \*\*Generation flow:\*\* How a dropped item gets its implicit stat, rarity, and affix rolls assigned at drop-time.

3\. \*\*Salvage flow:\*\* How salvaging converts an item into material currency, and how that interacts with the inventory cap (e.g. forced salvage-or-discard-on-full prompts, salvage-from-inventory UI hook points).

4\. \*\*Sink flows:\*\* How reroll-affix, upgrade-rarity, and artifact-crafting would consume materials and mutate (or replace) a `GearInstance`, while preserving the "affixes always survive" rule.

5\. \*\*Impact on existing systems:\*\* What in the current equip-management hub scene, save/persistence layer, and loot-table drop logic would need to change vs. what's additive.

6\. \*\*Risk/complexity flags:\*\* Anything in the current codebase that makes this harder than expected, and a rough sense of how many implementation tasks this should be split into (this task is analysis-only; actual implementation will be scoped into separate numbered tasks after this proposal is reviewed).



Do not write code. Do not create or modify SO assets. This is a planning document to review before we lock the next round of task files.

