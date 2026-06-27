\# Task 061 — Balance Audit: Current Numeric State Across All Systems



> Status: Ready for implementation

> Depends on: all prior content tasks (heroes, abilities, enemies, waves, gear, XP/currency systems) — read-only analysis, no dependencies on specific task numbers beyond "everything implemented so far"



\---



\## Instructions to Claude Code (read first)



1\. Read CLAUDE.md in full before starting, even if read in a prior session.

2\. Read this entire task file before writing any code (note: this task produces a document, not code changes — see §1).

3\. Do not expand scope. This is a read-only audit and report. Do not modify any SO assets, values, or code as part of this task.

4\. Flag, don't guess:

&#x20;  - If you cannot find a value you'd expect to exist (e.g. a wave-scaling multiplier), say so explicitly rather than inferring or estimating a number.

&#x20;  - If a number's purpose or formula isn't documented/obvious from the code, say "unclear" rather than guessing what it's meant to do.

5\. At the end of your response, in addition to the report itself, give a short summary of which areas had the most apparent inconsistencies or "looks like a leftover test value" numbers.



\---



\## 0. Context



The project has accumulated a large amount of numeric/tunable data across many tasks, much of it set during iterative testing rather than deliberate balance passes. Before doing any actual rebalancing, the developer wants a clear, organized picture of what currently exists — flat values, scaling curves, and how systems relate to each other numerically — without changing anything yet. A deliberate balance pass will follow as a separate task once target curves are decided based on this report.



\## 1. Goal



Produce a single organized markdown report (e.g. `/docs/balance/audit\_001.md`) cataloguing all current tunable numeric values across the systems listed below, plus explicit notes on anything that looks inconsistent, arbitrary, or like a leftover test value rather than a deliberate design choice.



\## 2. Scope — What to Catalogue



\### 2.1 Hero Stats

\- For each of the four heroes (Frost Warden, Bolt Striker, Pyromancer, Marksman): base stats (HP if applicable, damage, attack speed/cooldown, range, crit chance/multiplier if applicable), and how these compare to each other at a glance (e.g. a simple side-by-side table).

\- Basic/Ultimate ability base values and how they scale per upgrade-line tier (1→2→3) for each of the 8 lines per hero.

\- Apex talent and combo-apex talent values, and how their magnitude compares to a maxed regular line (are apexes meaningfully more powerful, proportionally, and by how much).



\### 2.2 Enemy Scaling

\- Base stats (HP, damage, speed) for each enemy type (Skeleton, EvilGod boss) at wave 1 baseline.

\- The actual scaling formula/multiplier applied per wave (read from `WaveConfigSO`/`DifficultyTierSO` — report the literal formula or curve shape found in code/data, not an assumption).

\- Project this scaling out across a representative range of waves (e.g. wave 1, 5, 10, 15, 20, 30, 50) so the curve's real-world shape is visible in the report, not just the raw formula.



\### 2.3 Currency \& XP

\- Currency/XP yield per enemy kill, and whether/how this scales with wave number.

\- XP-to-level-up curve (XP required per level, and whether this curve is linear/exponential/stepped).

\- Shop item (`ConsumableDefinitionSO`) prices vs. typical currency income rate at a few representative wave points — i.e. roughly how many kills does it take to afford one shop item at wave 5 vs wave 20.



\### 2.4 Gear \& Loot

\- Stat magnitude per rarity tier (Common → Unique) for each gear slot type, and the relative power jump between adjacent tiers (e.g. is Rare meaningfully stronger than Uncommon, by what rough percentage).

\- Loot drop-rate weighting per rarity tier, both for regular enemies and boss-exclusive tiers (Legendary/Unique), including how the Luck stat (Task 24) currently influences these weights.

\- Whether equip slots (Helmet/Body/Hands/Legs/Feet/Artifact) have meaningfully different stat budgets from each other, or are currently roughly interchangeable in value.



\### 2.5 Meta-Progression

\- Hero-slot unlock thresholds (wave 15/30/50 → slot 2/3/4) — note whether these feel evenly spaced relative to how enemy scaling/difficulty grows between those wave numbers (per §2.2's curve), i.e. flag if a slot unlock happens either very early or very late relative to a difficulty spike.



\### 2.6 Cross-System Sanity Checks

\- Does total player damage output (heroes' combined DPS at a "reasonable" upgrade state for a given wave) roughly keep pace with enemy HP scaling at that same wave, or does one clearly outrun the other based on the raw numbers? This is a rough estimate, not a simulation — state assumptions clearly.

\- Are there any values that appear completely disconnected from any formula (e.g. a flat number that doesn't fit any visible scaling pattern nearby) — flag these as likely leftover test values.



\---



\## 3. Out of Scope



\- Changing any values — this task produces a report only.

\- Running actual playtests or simulations — estimates based on reading the data/code are sufficient, with assumptions clearly stated.

\- Proposing specific new target numbers — that's the developer's design decision for the next task, based on this report.



\---



\## 4. Acceptance Criteria



\- \[ ] A single markdown report exists at `/docs/balance/audit\_001.md` covering all subsections in §2.

\- \[ ] Every number/formula reported is traceable to an actual SO asset or code location (cite where it was found), not invented or assumed.

\- \[ ] Any value that couldn't be found or whose purpose is unclear is explicitly flagged as such, not silently omitted or guessed.

\- \[ ] The report includes a final "Notable Inconsistencies" section listing the values/areas that look most like arbitrary leftover test data rather than deliberate design.

\- \[ ] No SO values, code, or other project files are modified by this task.

