\# Task 80 — Shop Redesign: Utility-Only Boss Reward + New Item Roster



> Read `CLAUDE.md` in full, and read Task 78 and Task 79 analysis reports in full before starting.

> This task replaces the current between-wave currency shop with a boss-exclusive utility reward,

> implements the approved 7-item roster, and removes all stat-boosting consumables. This file

> describes outcomes, not code.



\## Goal



Redesign the shop from a between-wave currency sink with stat-boosting potions into a boss-exclusive

"pick one utility reward" moment. Currency remains in the game for potential future use but is no

longer spent in the shop. The new shop opens only after a boss dies, offers 3–4 utility items, and

the player picks exactly one for free. No purchase, no currency transaction.



\## Locked decisions for this task



\- \*\*Boss-only trigger:\*\* the shop opens exclusively when a boss wave is cleared, not after every

&#x20; wave. Investigate the current `WaveConfigSO`/`DifficultyTierSO` data to identify which waves are

&#x20; boss waves and where natural shop moments should fall across the 60-wave run. Document your chosen

&#x20; trigger points and reasoning in the implementation summary — these are flagged for tuning, not

&#x20; final.

\- \*\*Pick one, free:\*\* the player sees 3–4 utility items and picks exactly one at no Currency cost.

&#x20; No buying multiple, no reroll, no skip-with-currency. The choice is the reward.

\- \*\*Currency is retained\*\* in the game (CurrencyManager, drop pipeline, UI counter) but has no sink

&#x20; in this task. Do not remove Currency infrastructure — it may gain new uses later. If Currency

&#x20; currently displays somewhere in the run UI, it can stay visible.

\- \*\*Ultimate Duration consumable is removed\*\* (flagged as borderline in Task 79 — confirmed removed

&#x20; here).

\- \*\*All 7 approved utility items ship in this task\*\* (see §1 below). No phased rollout.

\- \*\*Aegis Shield\*\* (wall damage-reduction/absorb buffer) is a genuinely new wall mechanic not

&#x20; present in `WallRuntime` today — flag the implementation approach clearly and note it as the

&#x20; highest-risk item in the roster.

\- Arena-control items (Tar Field, Glacial Choke, Flash Freeze) hook into the existing

&#x20; `GroundZoneManager` and status-effect system (CLAUDE.md §3.8) — do not build a parallel

&#x20; arena-effect path.

\- Freeze (movement → 0) is the de-facto stun for this game since enemies only threaten by reaching

&#x20; the wall. No separate Stun type needed.



\## 1. Approved utility item roster



\### Wall utility

\- \*\*Wall Repair Kit\*\* (retained as-is) — heals wall HP via existing `WallRuntime.Heal`.

\- \*\*Reinforced Repair\*\* — larger wall HP heal than the basic kit.

\- \*\*Reinforced Barricade\*\* — reduces incoming wall damage by a percentage for one wave.

\- \*\*Aegis Shield\*\* — grants the wall a temporary absorb buffer that takes hits before wall HP does,

&#x20; for one wave. New mechanic — flag implementation approach and any `WallRuntime` changes needed.



\### Arena control

\- \*\*Tar Field\*\* — places a persistent slow GroundZone across the lane for one wave, reusing the

&#x20; existing `GroundZoneManager`/Slow path.

\- \*\*Glacial Choke\*\* — slow/freeze zone near the wall for one wave, reusing `GroundZoneManager`

&#x20; with a Freeze status extension if needed.

\- \*\*Flash Freeze\*\* — instantly freezes all currently active enemies for a short fixed duration

&#x20; (\~3s), then thaws. One-time trigger, not a persistent zone.



All items are one-wave-or-shorter effects unless noted. Exact magnitudes and durations are your

call — document every value in the implementation summary and flag them all for tuning.



\## 2. Scope



\- Remove all 33 consumable assets identified in Task 79's removal list (everything except Wall

&#x20; Repair Kit). Do not leave orphaned assets — clean up SO files and any catalog/shop references.

\- Implement the 6 new `ConsumableDefinitionSO` assets (Wall Repair Kit already exists).

\- Extend `ConsumableDefinitionSO` and `ShopController` as needed to support area/positional effects

&#x20; (Tar Field, Glacial Choke, Flash Freeze) and the new Aegis Shield wall mechanic — the current

&#x20; schema only supports scalar value + duration and has no arena reference. Do the minimum necessary

&#x20; to support these effect types cleanly without building a general-purpose scripting system.

\- Change the shop trigger from between-wave to boss-death only, with 3–4 items offered and a

&#x20; single free pick. Wire this to whatever existing boss-wave/wave-complete event is appropriate.

\- Update `ShopController` to inject whatever new dependencies arena-control effects need (e.g.

&#x20; `GroundZoneManager`, `WaveSpawner`) — per CLAUDE.md §3.5, pass these via init/injection from

&#x20; `GameSession`, not static access.

\- Update CLAUDE.md §1 (Core Loop) and §2 (Locked Design Decisions) to reflect the new shop model

&#x20; — the current text describes a between-wave currency shop, which is no longer accurate.



\## Out of Scope (do not implement)



\- Any new Currency sink — Currency infrastructure is retained but unused for now.

\- Reroll mechanic of any kind.

\- Any change to XP, ability upgrades, or the level-up card system.

\- Sorting, filtering, or UI polish beyond functional clarity.

\- Wall repair/upgrade meta-progression (CLAUDE.md §6 post-MVP item — still out of scope).



\## Acceptance Criteria



\- \[ ] Shop opens only on boss death, never after a regular wave.

\- \[ ] Player is offered 3–4 utility items and picks exactly one at no Currency cost.

\- \[ ] All 33 removed consumable assets are gone with no orphaned references.

\- \[ ] All 7 utility items work correctly in a test run (wall items affect wall HP/damage,

&#x20;     arena-control items produce visible effects on enemies/arena).

\- \[ ] Aegis Shield implementation approach is documented; `WallRuntime` changes (if any) are

&#x20;     flagged in the implementation summary.

\- \[ ] Arena-control items hook into existing `GroundZoneManager`/status-effect systems — no

&#x20;     parallel arena-effect path introduced.

\- \[ ] Currency infrastructure (CurrencyManager, drop pipeline, UI) is untouched.

\- \[ ] CLAUDE.md §1 and §2 updated to reflect the new shop model.

\- \[ ] All new item magnitudes/durations/trigger wave points documented in the implementation

&#x20;     summary and flagged for tuning.



\## Reviewer Notes



Flag as blocking if:

\- Shop triggers on any non-boss wave.

\- Currency is spent anywhere in the new shop flow.

\- A parallel arena-effect system was built instead of hooking existing GroundZoneManager.

\- Any removed consumable still has live SO assets or shop references.

\- New dependencies were injected via static access instead of GameSession init injection.

\- CLAUDE.md was not updated to reflect the new shop model.

