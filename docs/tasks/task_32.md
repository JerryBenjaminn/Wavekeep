\# Task 32 — UI Clarity Pass: Level-Up Cards + Apex Talent Cooldown Display



> Read `CLAUDE.md` in full, and review Task 07 (level-up card UI), Task 21 (ultimate charge bar / cooldown UI

> pattern), and Task 29/31 (upgrade lines, apex talents) before starting. This is a UI-only task — no changes to

> upgrade line data, apex talent logic, or any gameplay calculation.



\## Goal



Level-up cards are currently too small to legibly display their text, and it's unclear at a glance which skill

(Basic or Ultimate) a given line upgrade belongs to. Separately, once an apex talent unlocks, the player has no

visual indication of its cooldown state, even though it fires automatically — making it feel invisible/unclear

when it's about to trigger.



\## Scope



\### 1. Level-Up Card Resize + Legibility

\- Increase the level-up card's size (and/or internal layout/padding) so that line name, tier, and effect

&#x20; description text fit without being clipped or overflowing — exact new dimensions are an implementer judgment

&#x20; call, prioritize legibility over fitting a specific number of cards on screen at once (adjust card-row layout/

&#x20; scroll behavior if needed to accommodate larger cards).

\- Verify text wrapping behaves correctly for the longest existing line description (Frost Warden's lines from

&#x20; Task 31) at all three tiers, not just short placeholder text.



\### 2. Skill-Source Badge/Label

\- Add a clear visual badge or label on each level-up card indicating which skill the line belongs to (e.g.

&#x20; "BASIC" or "ULTIMATE"), positioned consistently (e.g. top corner) so it's readable at a glance without needing

&#x20; to read the full description.

\- Use distinct, consistent styling per skill type (e.g. distinct color or icon) so the player can visually

&#x20; pattern-match which skill they're investing in across multiple level-ups without re-reading text each time.



\### 3. Apex Talent Cooldown Display

\- Once an apex talent unlocks (per Task 29/31's unlock mechanism), display a cooldown indicator for it in the

&#x20; existing in-game UI (e.g. alongside or near the existing ultimate charge bar from Task 21, reusing that bar's

&#x20; visual conventions — fill/empty style — for consistency) so the player can see when it's ready vs. on cooldown.

\- This indicator should only appear once the relevant apex talent has actually unlocked (no empty/placeholder

&#x20; indicator shown before unlock).

\- If multiple apex talents are unlocked at once in the future, the display should be able to show more than one

&#x20; (design for at least 2 simultaneous indicators, even though only one hero/apex exists to test with right now).

\- Read the apex ability's live cooldown state directly from its runtime instance — no approximated/duplicate

&#x20; timer.



\## Out of Scope (do not implement)

\- Any change to upgrade line content, tier values, or apex talent unlock conditions/effects (Task 29/31's logic

&#x20; is unchanged)

\- Frost Zone's area-of-effect geometry or Absolute Zero's redesign (separate task)

\- New UI screens beyond the level-up card and the apex cooldown indicator



\## Acceptance Criteria

\- \[ ] Level-up card text (name, tier, description) is fully legible without clipping for all current Frost Warden

&#x20;     lines at all tiers

\- \[ ] Each card displays a clear, consistently-styled badge indicating Basic vs Ultimate

\- \[ ] Apex talent cooldown indicator appears only after unlock, accurately reflects live cooldown state, and

&#x20;     visually follows the existing ultimate-bar fill/empty convention

\- \[ ] UI supports displaying at least 2 simultaneous apex cooldown indicators without layout breaking, even if

&#x20;     only one is testable today

\- \[ ] No SO asset is mutated at runtime

\- \[ ] Full playtest: level up through several cards confirming legibility and correct Basic/Ultimate badges;

&#x20;     unlock an apex talent and confirm its cooldown indicator appears and behaves correctly through several

&#x20;     trigger cycles



\## Reviewer Notes

Flag as blocking if:

\- Apex cooldown indicator reads from an approximated timer instead of the apex ability's actual runtime cooldown

&#x20; state

\- Card resize breaks legibility or layout for any existing card content

