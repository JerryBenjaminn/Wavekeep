\# Wavekeep — Full Project Analysis Request



You are acting as a senior game designer and systems architect reviewing a Unity 6 roguelite tower

defense game called Wavekeep (working title). Read CLAUDE.md in full first, then read every task

implementation summary available under /docs/tasks/ before writing your analysis.



\## What I want from you



Produce a honest, detailed analysis covering all four areas below. Don't soften findings — if

something is broken, unbalanced, or architecturally fragile, say so directly.



\---



\## 1. Systems health



For each major system (wave/spawner, hero abilities, gear, shop, progression/unlocks, audio, UI/Hub),

give a brief verdict:

\- Is it architecturally sound relative to CLAUDE.md's locked decisions?

\- Are there known fragilities, tech debt, or flagged issues from implementation summaries that haven't

&#x20; been resolved?

\- Any system that exists in isolation (not yet connected to systems it should interact with)?



\---



\## 2. Game feel \& progression



Based on everything you can read about the current state:

\- Does the core loop (wave → kill → XP/gear → upgrade → next wave) have a coherent pacing arc across

&#x20; all 60 waves?

\- Where are the likely feel-bad moments (too easy, too hard, too slow, too sudden)?

\- Does the gear system (Tasks 67–76) add meaningful decisions or is it still mostly passive stat

&#x20; accumulation?

\- Does the new boss-only utility shop (Task 80) create interesting tactical moments or does it feel

&#x20; like an afterthought?



\---



\## 3. Content gaps \& hero parity



\- Which heroes are fully implemented (model + animations + shader + abilities) and which are partial?

\- Is there a risk that balance tuning done on Frost Warden (the most complete hero) will feel wrong

&#x20; when other heroes are fully playable?

\- What content is missing before all four heroes feel like distinct, complete playstyles?



\---



\## 4. MVP readiness



Given the current state, answer directly:

\- What is the shortest path to a playable, shippable MVP (Steam Early Access quality)?

\- What are the three most critical things to fix or finish before showing this to anyone outside the

&#x20; development process?

\- What systems or features that are currently planned could be cut or deferred without hurting the

&#x20; core experience?

\- Rough task estimate: how many more focused implementation tasks (in the style of the existing

&#x20; task files) would it take to reach MVP?



\---



\## Format



\- Be direct and specific — reference actual system names, task numbers, and CLAUDE.md sections

&#x20; where relevant.

\- Flag anything you're uncertain about due to missing information (e.g. if an implementation summary

&#x20; is absent for a task you expected to find).

\- End with a prioritized top-5 action list: if Jerry has time for only five more focused tasks before

&#x20; a playtest with outside players, what should they be?

