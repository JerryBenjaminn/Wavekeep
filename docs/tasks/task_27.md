\# Task 27 — Test Gear Population (All Slots × All Tiers)



> Read `CLAUDE.md` in full, and review Task 12 (gear/artifact data, slots, rarity tiers) and Task 24 (luckBonus

> field) before starting. This is a content/test-data task, not a systems task — no new code paths should be

> needed if Task 12's authoring pattern already supports creating new gear assets.



\## Goal



Currently only one item exists per equip slot, which makes it impossible to properly test the Task 26 stat

comparison feature (there's nothing of a different tier to compare against). This task adds enough gear/artifact

assets to cover every combination of slot and rarity tier, so comparison and tier-based behavior can be tested

end-to-end.



\## Scope



\- Create one gear/artifact `ScriptableObject` asset for every combination of the six equip slots (Helmet, Body,

&#x20; Hands, Legs, Feet, Artifact) and the six rarity tiers (Common, Uncommon, Rare, Epic, Legendary, Unique) —

&#x20; 36 items total, unless some of these combinations are already covered by existing assets, in which case only the

&#x20; missing combinations need to be added.

\- Stat values (damage, luck bonus, or whatever stat fields exist on the gear/artifact SO) do not need real balance

&#x20; — placeholder values that simply increase somewhat with tier are sufficient (e.g. higher tier = slightly higher

&#x20; numbers), since real tuning happens later. The goal is variety for testing, not correctness.

\- Naming convention should make each item's slot and tier identifiable at a glance in the project window (e.g.

&#x20; follow whatever naming pattern existing gear assets already use, extended consistently).

\- Use the existing Task 15 Editor Authoring tool if it already supports gear/artifact creation; otherwise create

&#x20; assets directly. Flag if the authoring tool needs extending to support this and ask before adding new tooling.

\- Boss-exclusive lock from Task 13 (Legendary/Unique normally restricted to boss drops) only affects how items are

&#x20; \*obtained\* in normal gameplay — it does not prevent these test assets from existing in the project for manual

&#x20; testing purposes (e.g. dropped via debug tools or assigned directly for a test).



\## Out of Scope (do not implement)

\- Real balance values for any stat

\- Changes to loot tables, drop rates, or boss-exclusivity rules

\- New stat fields beyond what already exists on the gear/artifact SO



\## Acceptance Criteria

\- \[ ] All 36 slot × tier combinations exist as gear/artifact assets (or the previously-missing ones are added)

\- \[ ] Each asset has placeholder stat values that scale upward with tier

\- \[ ] Assets are named consistently and identifiably by slot and tier

\- \[ ] Existing gear assets are not duplicated or overwritten unnecessarily

\- \[ ] No changes made to loot table weighting, drop rates, or boss-exclusivity logic



\## Reviewer Notes

Flag as blocking if:

\- Stat values are identical across tiers (defeats the purpose of testing comparison)

\- Any change is made to drop-rate/loot-table logic instead of just adding static test assets

