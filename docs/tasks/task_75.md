\# Task 75 — Gear Redesign Part 6: Reroll-Affix + Upgrade-Rarity Sinks



> Read `CLAUDE.md` in full, and read the Task 66 analysis (`gear\_redesign\_001.md`) plus Task 67, 71,

> and 73 implementation summaries before starting. This task adds the two remaining gear-mutation sinks

> to the backend and wires them into the existing Hub UI from Task 73. This file describes outcomes,

> not code.



\## Goal



Complete the gear economy by adding the two remaining Dust sinks: rerolling a single affix's value on

an owned item, and upgrading an owned item's rarity tier by one step. Both are Hub-only actions, both

consume Salvage Dust, and neither ever destroys or risks existing affixes.



\## Locked decisions for this task



\- \*\*Reroll-affix:\*\* only the rolled \*value\* changes (within the same affix type's min/max range from

&#x20; `AffixDefinitionSO`). The affix type itself never changes — Damage% stays Damage%, Cooldown stays

&#x20; Cooldown. A new value is rolled within the existing affix's range; the old value is replaced.

\- \*\*Upgrade-rarity cap is Legendary.\*\* The upgrade-rarity sink cannot produce Unique — Unique is only

&#x20; obtainable via the Artifact Forge (Task 71). Attempting to upgrade a Legendary item gives no upgrade

&#x20; option; Legendary is the ceiling for this sink.

\- \*\*Affixes always survive upgrade-rarity:\*\* existing affixes are preserved verbatim when rarity is

&#x20; raised. The new rarity tier's additional affix slot(s) (per `GearGenerationConfigSO`) are rolled

&#x20; fresh and appended — no existing affix is removed, replaced, or rerolled.

\- \*\*Dust only, no other currency\*\* — consistent with Task 71's Forge.

\- \*\*Hub-only\*\* — these actions are never available in-run, consistent with the locked decision from

&#x20; Task 73.

\- \*\*No chance/risk element\*\* — both operations always succeed when the player can afford them. No

&#x20; failure states, no partial outcomes.

\- Costs are your call; document them in the implementation summary and add them to `GearEconomyConfigSO`

&#x20; (the existing tuning SO from Task 71) so they're tunable without code changes. Reroll should feel

&#x20; cheaper than a full rarity upgrade; rarity upgrade should feel cheaper than forging fresh at the

&#x20; equivalent tier.



\## Scope



\### 1. Reroll-affix (backend)

\- Add a `RerollAffix(instanceId, affixIndex)` action to `GearManager` that re-rolls the value of a

&#x20; single affix slot (by index) within that affix type's `\[minValue, maxValue]` range, spends the

&#x20; configured Dust cost, and persists the updated instance. All other affixes and the item's rarity are

&#x20; unchanged. `instanceId` is unchanged.



\### 2. Upgrade-rarity (backend)

\- Add an `UpgradeRarity(instanceId)` action to `GearManager` that raises the item's rarity by one step

&#x20; (Common→Uncommon→Rare→Epic→Legendary, hard cap), spends the configured Dust cost, rolls any new

&#x20; affix slots added by the higher rarity (via existing generation logic from Task 67/68), appends them

&#x20; to the existing affix list, and persists. Existing affixes are untouched.

\- Block or return an error if called on a Legendary (already at cap) or a Unique (not upgradeable via

&#x20; this sink at all) — flag your chosen error-handling approach.



\### 3. Hub UI wiring

\- Surface both actions from the existing item-detail view introduced in Task 73:

&#x20; - \*\*Reroll:\*\* per-affix control on the detail view (e.g. a button per affix row) showing the Dust

&#x20;   cost, disabled/hidden if the item has no affixes or the player can't afford it.

&#x20; - \*\*Upgrade rarity:\*\* a single per-item action on the detail view showing the Dust cost and the

&#x20;   resulting rarity, disabled/hidden if the item is already Legendary (or Unique).

\- Both actions should show the Dust cost clearly before the player commits, consistent with how the

&#x20; Forge and single-item salvage already communicate costs.

\- Wire `\_gearEconomyConfig` in the Hub scene's bootstrap if not already done — it should be wired from

&#x20; Task 73, but confirm it covers the new cost fields added here.



\## Out of Scope (do not implement)



\- Rerolling the affix \*type\* — explicitly rejected; only the value changes.

\- Upgrading any item to Unique via this sink — explicitly rejected; Unique is Forge-only.

\- Any change to Artifact Forge, salvage, mass salvage, or overflow behavior from Tasks 71/73/74.

\- Any in-run access to these actions.

\- Authoring tooling or cleanup of the 36 obsolete `Gear\_\*` assets — optional, separate future task.

\- RangeMultiplier split — separate backlog task.



\## Acceptance Criteria



\- \[ ] `RerollAffix(instanceId, affixIndex)` re-rolls only the targeted affix's value within its defined

&#x20;     range, spends Dust, persists, leaves all other affixes and rarity unchanged.

\- \[ ] `UpgradeRarity(instanceId)` raises rarity by one step (max Legendary), spends Dust, appends

&#x20;     new affix slots for the higher rarity, leaves existing affixes untouched, persists.

\- \[ ] Neither action is available on a Unique item; upgrade-rarity is not available on a Legendary item.

\- \[ ] Both costs live in `GearEconomyConfigSO` and are tunable without code changes.

\- \[ ] Both actions are accessible from the Hub item-detail UI with cost shown before committing.

\- \[ ] Neither action is reachable in-run.

\- \[ ] No existing affix is destroyed, replaced, or rerolled as a side effect of either operation.

\- \[ ] Dust balance is correctly deducted and the updated balance is reflected in the UI immediately.



\## Reviewer Notes



Flag as blocking if:

\- Reroll changes the affix type, not just the value.

\- Upgrade-rarity can produce a Unique item through any path.

\- Either cost is hardcoded instead of read from `GearEconomyConfigSO`.

\- Either action is accessible outside the Hub scene.

\- An existing affix was removed or altered as a side effect of an upgrade-rarity operation.

