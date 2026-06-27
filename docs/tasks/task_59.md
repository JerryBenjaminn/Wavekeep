\# Task 059 — Audio System: Core Architecture



> Status: Ready for implementation

> Depends on: Task 01 (EventBus, GameSession), §3.3 (Event-Driven Communication), §3.5 (No static singletons), §3.6 (Cross-platform — audio must not assume PC-only output)



\---



\## Instructions to Claude Code (read first)



1\. Read CLAUDE.md in full before starting, even if read in a prior session.

2\. Read this entire task file before writing any code.

3\. Do not expand scope. Implement exactly what's specified; flag suggestions separately instead of acting on them.

4\. Flag, don't guess, on:

&#x20;  - Any mismatch between this task's "Pre-confirmed State" section and what you actually find in the project.

&#x20;  - Any conflict with locked decisions in CLAUDE.md §2/§3.

&#x20;  - Any gameplay moment that needs a sound trigger but has no existing EventBus event to hook into (e.g. crit hits, apex/combo-apex activation, gear equip, hero-slot unlock, shop purchase, discovery-codex first-unlock) — list these explicitly in your summary as "events that need to be added" rather than silently inventing ad-hoc direct method calls scattered through gameplay code.

5\. Do not touch ScriptableObject template assets at runtime.

6\. At the end of your response, summarize what was implemented, anything flagged, and what (if anything) needs manual Editor action (e.g. assigning actual audio clips, which is explicitly out of scope — see below).



\---



\## 0. Context / Pre-confirmed State (do not re-verify, treat as given)



\- No audio system exists yet in the project. This task builds the full architecture from scratch.

\- The developer will supply actual audio clips (SFX purchased from asset packs, music from asset packs) afterward by dragging them into SO assets created by this system — \*\*this task must not require or assume any specific audio clip exists.\*\* Build the data structures and hookups; leave clip fields empty/unassigned, ready for the developer to populate.

\- Folder convention for audio assets going forward: `Assets/Audio/SFX/\[Heroes|Enemies|UI|Environment]/` and `Assets/Audio/Music/`. This task should create this folder structure (even if empty aside from placeholder SO assets) and the SO assets that will reference clips placed there.



\---



\## 1. Goal



Build a complete, data-driven audio architecture: an `AudioManager` service owned by `GameSession`, an `AudioCueDefinitionSO` data type, Audio Mixer groups, and EventBus-driven hookups for all currently-existing gameplay events — without assigning any actual audio clips.



\## 2. Scope



\### 2.1 Data Layer



\- `AudioCueDefinitionSO`: defines one playable sound cue. Fields:

&#x20; - `AudioClip\[] clips` (array, not single clip — supports random variation pool for the same cue, e.g. multiple hit-grunt variations)

&#x20; - `AudioMixerGroup` category reference (or an enum `AudioCategory { Music, SFX, UI }` that maps to a mixer group — pick whichever is cleaner architecturally and document the choice)

&#x20; - `float volume` (0–1, default 1)

&#x20; - `Vector2 pitchRandomRange` (default (1,1) — no variation unless set)

&#x20; - `bool loop` (for sustained sounds like Ultimate active-zone loops)

\- Follow §3.5: this SO is a read-only template; any runtime playback state (e.g. currently-playing loop instance for a hero's active Ultimate) lives in a runtime wrapper/class, never written back into the SO.



\### 2.2 AudioManager



\- Owned by `GameSession` (no static singleton, per §3.5), instantiated/injected the same way as `CurrencyManager`/`XPManager`/etc.

\- Public API: something like `PlayCue(AudioCueDefinitionSO cue)`, `PlayCueAtPosition(AudioCueDefinitionSO cue, Vector3 position)` (for potential future spatial audio, even if not used yet), `StartLoopingCue(AudioCueDefinitionSO cue) → returns a handle/ID`, `StopLoopingCue(handle)`.

\- Internally manages a small pool of `AudioSource` components (object-pooled, consistent with the project's general pooling philosophy from §3.5 — don't `Instantiate`/`Destroy` an AudioSource per sound in steady-state play) for one-shot SFX, plus dedicated persistent AudioSources for music and for looping cues (e.g. one looping AudioSource per active hero Ultimate zone effect, managed by handle).



\### 2.3 Audio Mixer Setup



\- Create an Audio Mixer asset with groups: `Master` → `Music`, `SFX`, `UI` (children of Master). This task only needs to wire `AudioManager` to route playback through the correct group based on each cue's category — actual volume-slider UI for a settings menu is out of scope (just make sure the mixer groups exist and are correctly used, so a future settings task can expose volume sliders against them).



\### 2.4 EventBus Hookups — Existing Events Only



Wire `AudioManager` (or a dedicated `AudioEventListener` component it owns) to subscribe to the six existing EventBus events from §3.3 and trigger a cue-play call for each — using \*\*empty/placeholder `AudioCueDefinitionSO` references\*\* that the developer will populate later:



\- `OnEnemyKilled` → enemy-death SFX cue (placeholder, enemy-type-agnostic for now — note in summary if a per-enemy-type cue lookup would be better long-term, but don't build that complexity now unless trivial)

\- `OnWaveStarted` / `OnWaveCompleted` → wave-start/wave-complete cues

\- `OnXPLevelUp` → level-up cue

\- `OnCurrencyChanged` → do NOT play a sound on every currency tick (this fires frequently/per-kill and would be noisy) — flag this explicitly rather than wiring it, since it's likely the wrong event to attach an SFX to; correct hook is probably the shop-purchase action instead, which doesn't exist as an event yet (see §2.5).

\- `OnRunEnded` → victory/defeat music or cue (branch on `RunResult` if that data is available on the event payload)



\### 2.5 Flag Missing Event Hookups (Do Not Implement New Events Yet)



List explicitly in your response summary — without implementing — every gameplay moment that needs audio but has no EventBus event yet to hook into. Based on current project scope, this likely includes at minimum:

\- Hero Basic-ability attack impact (note: Frost Warden/Skeleton already have `OnAttackImpactFrame()`/`OnBasicAttackImpactFrame()` Animation Event callbacks from Tasks 054/056 — flag whether hooking audio there directly, bypassing EventBus, is acceptable for this specific case since it's already a tightly-coupled animation-driven callback, or whether it should publish a new EventBus event instead for consistency)

\- Ultimate cast start / Ultimate active loop / Ultimate end

\- Apex talent activation / combo-apex activation

\- Critical hit

\- Shop purchase (success/fail)

\- Gear equip/unequip

\- Hero-slot unlock (meta-progression)

\- Discovery-codex first-unlock notification

\- UI button clicks generally



Do not add these new events or hookups in this task — just enumerate them clearly so a follow-up task can address each deliberately.



\### 2.6 Music Playback (Basic)



\- Add a simple `PlayMusicTrack(AudioCueDefinitionSO musicCue, bool crossfade)` method on `AudioManager` for switching background music tracks (e.g. hub vs. in-run vs. boss vs. victory/defeat) with optional crossfade (simple linear volume crossfade over a tunable duration is sufficient — no need for anything elaborate). Do not wire this to any specific trigger yet beyond `OnRunEnded` (§2.4) — scene/state-based music switching (hub vs in-run) is a follow-up task once scene structure for this is finalized.



\---



\## 3. Out of Scope



\- Assigning any actual audio clips — all `AudioCueDefinitionSO` assets created by this task remain clip-less placeholders for the developer to fill in.

\- Settings menu / volume slider UI.

\- Spatial/3D positional audio behavior (the position parameter exists on the API for future use, but don't implement distance attenuation logic beyond Unity's AudioSource defaults).

\- Implementing any of the new events listed in §2.5 — flag only.

\- Hero-specific or enemy-specific cue variety beyond one placeholder cue per existing EventBus event.



\---



\## 4. Acceptance Criteria



\- \[ ] `AudioCueDefinitionSO` exists with fields per §2.1, follows SO-is-read-only-template convention.

\- \[ ] `AudioManager` exists, owned by `GameSession`, no static singleton.

\- \[ ] AudioSource pooling in place for one-shot SFX (no per-sound Instantiate/Destroy).

\- \[ ] Audio Mixer asset created with Master/Music/SFX/UI group structure, correctly routed from `AudioManager`.

\- \[ ] All six existing EventBus events from §3.3 have a corresponding placeholder cue hookup, except `OnCurrencyChanged` which is explicitly flagged as the wrong hook point rather than wired.

\- \[ ] `Assets/Audio/SFX/\[Heroes|Enemies|UI|Environment]/` and `Assets/Audio/Music/` folder structure exists.

\- \[ ] Response summary explicitly lists all missing-event gameplay audio hooks per §2.5, without implementing them.

\- \[ ] No actual audio clips assigned anywhere — all cues are clip-less and ready for the developer to populate.

