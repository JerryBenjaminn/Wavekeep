\# Task 40 — UI Fixes: In-Game Stat HUD Positioning + Hub Layout Overlap



> Read `CLAUDE.md` in full, and review Task 22 (in-game stat panel) and Task 37 (hub team-selection panel) before

> starting. This is a UI-only task — no gameplay logic changes.



\## Goal



Fix two separate layout problems:

1\. The in-game stat HUD (Task 22) is centered on screen and some of its content overflows/runs off-screen.

2\. The hub scene's team-selection panel (Task 37) has overlapping elements — most visibly, the "Editing: \[Hero

&#x20;  Name]" header renders directly on top of the hero list row below the team-select buttons (see attached

&#x20;  reference: "Editing: Bolt Striker" overlaps the "Frost Warden" row).



\## Scope



\### 1. In-Game Stat HUD Repositioning

\- Move the stat HUD from its current centered position to the right side of the screen, vertically centered (or

&#x20; otherwise positioned so all content fits fully on-screen with no clipping/overflow at any common resolution

&#x20; given the project's landscape-only constraint, CLAUDE.md §3.6).

\- Resize/re-layout internal elements as needed so all stat rows (including Luck from Task 24, crit/elemental

&#x20; stats from Task 23, and Armor/MagicResist if displayed) fit without text or rows running off the visible area.

\- Verify this also accounts for \*\*two heroes'\*\* worth of stats now being relevant (Task 36/37 multi-hero) if the

&#x20; panel is meant to show both heroes' stats — confirm current behavior (single panel for one hero, or does it

&#x20; need a per-hero split?) and document which approach was taken if this wasn't already decided.



\### 2. Hub Scene Layout Cleanup

\- Fix the overlapping "Editing: \[Hero Name]" header so it no longer renders on top of the hero list/team-select

&#x20; rows — reposition it to its own clear space (e.g. above the gear list it's labeling, or moved to where it

&#x20; doesn't collide with the team-select row above it).

\- Review the broader hub layout (team-select row, equip slot rows, gear panel from Task 25/26) for any other

&#x20; spacing/alignment issues beyond this one overlap — tighten up padding/margins/alignment generally so the screen

&#x20; doesn't look visually rough, using consistent spacing between sections.

\- This task does not need to redesign the hub's visual style (colors, fonts) — focus on fixing overlap and

&#x20; spacing/alignment problems with the existing style.



\## Out of Scope (do not implement)

\- New HUD/hub features or information not already present

\- Visual restyle (colors, fonts, iconography) beyond what's needed to fix overlap/spacing

\- Any gameplay logic changes



\## Acceptance Criteria

\- \[ ] In-game stat HUD is repositioned to the right side of the screen, fully visible with no off-screen/clipped

&#x20;     content at standard landscape resolutions

\- \[ ] Hub scene's "Editing: \[Hero Name]" header no longer overlaps the hero list/team-select rows

\- \[ ] No other overlapping elements remain in the hub scene's team-select/equip/gear-panel area

\- \[ ] Spacing/alignment across the hub scene reads as visually clean and consistent, even without a style change

\- \[ ] No SO asset is mutated at runtime

\- \[ ] Full playtest: confirm in-game HUD is fully readable during a run with stats from Luck, crit, and other

&#x20;     systems all visible; confirm hub scene shows no overlap when switching which hero is being edited



\## Reviewer Notes

Flag as blocking if:

\- Fix only addresses the one overlap shown in the reference screenshot while leaving other visible overlaps

&#x20; unaddressed in the same screen

\- HUD repositioning still clips/overflows at standard landscape resolutions

