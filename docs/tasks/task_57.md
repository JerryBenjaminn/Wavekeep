\# Task 057 — Frost Warden Ultimate: Full-Arena Frost Zone Depth + Screen Cast Overlay Effect



> Status: Ready for implementation

> Depends on: Task 033 (Frost Zone full-arena-width redesign), Task 045 (Frost Warden VFX), §3.7 (3D + Camera — fixed 3/4 top-down), §3.6 (UI scaling, Canvas Scaler)



\---



\## Instructions to Claude Code (read first)



1\. Read CLAUDE.md in full before starting, even if read in a prior session.

2\. Read this entire task file before writing any code.

3\. Do not expand scope. Implement exactly what's specified; flag suggestions separately instead of acting on them.

4\. Flag, don't guess, on:

&#x20;  - Any mismatch between this task's "Pre-confirmed State" section and what you actually find in the project.

&#x20;  - Any conflict with locked decisions in CLAUDE.md §2/§3.

&#x20;  - Anything requiring manual Editor judgment (exact texture choice, fade timing feel) — implement with clearly tunable Inspector values, note recommended defaults, and let the developer fine-tune.

5\. Do not touch ScriptableObject template assets at runtime.

6\. At the end of your response, summarize what was implemented, anything flagged, and what (if anything) needs manual Editor action/tuning.



\---



\## 0. Context / Pre-confirmed State (do not re-verify, treat as given)



\- Frost Warden's Ultimate currently creates a fixed-area Frost Zone covering the full arena \*\*width\*\* (Task 033), positioned in front of the wall, but does not yet cover the full arena \*\*depth\*\* (from the enemy spawn edge to the wall).

\- This task has two independent parts: (A) expanding the existing Frost Zone's depth coverage, and (B) adding a new, generic, reusable screen-space "cast effect" overlay system, with Frost Warden's Ultimate as its first user.

\- \*\*Design intent for part B, important for scope:\*\* this is the first of what will eventually be four hero-specific screen-cast overlays (one per hero, on Ultimate cast). Because all four heroes' Ultimates will be cast frequently in a real match, each individual overlay must be brief and subtle (low max opacity, short duration) so multiple overlays from different heroes don't visually clutter or obscure the screen when triggered close together or in overlap. Build the underlying system generically (texture + color + duration + max opacity as swappable inputs) so future heroes can register their own overlay without duplicating this system's code — but only Frost Warden's specific overlay (frost/window-frost look) needs to actually be wired up and used in this task.



\---



\## 1. Goal



\### Part A — Frost Zone Depth Expansion

Expand the Frost Zone's coverage area so it spans the full playable arena depth (enemy spawn edge to the wall), not just the full width as currently implemented.



\### Part B — Screen Cast Overlay System

Add a generic, reusable full-screen overlay effect system triggered on hero Ultimate cast, and wire Frost Warden's Ultimate to trigger a brief frost/window-frost-style overlay using a simple static texture + alpha animation (no custom shader needed for this).



\## 2. Scope



\### 2.1 Frost Zone Depth Expansion



\- Locate the existing Frost Zone area/collider/trigger-volume definition (from Task 033) and extend its depth dimension to span from the enemy spawn edge to the wall, matching the arena's full playable depth — not a guessed fixed value; read the actual arena/spawn/wall position references already used elsewhere in the project (e.g. wherever `WaveSpawner` or arena bounds are defined) rather than hardcoding a new magic number.

\- Confirm the freeze/slow effect (and the existing Frost status shader from Task 044, if it's the same visual system) still applies correctly to enemies anywhere within this newly expanded area, not just near the wall.

\- This is a numeric/positional change only — do not alter the Frost Zone's freeze/slow gameplay logic itself (duration, slow %, freeze threshold, etc. — Task 24/33/etc. mechanics stay as-is).



\### 2.2 Screen Cast Overlay System (generic)



\- Create a reusable component/manager (e.g. `ScreenCastOverlayController`, scene-scoped or owned by `GameSession` per §3.5's no-static-singleton convention) responsible for displaying a brief full-screen UI overlay image on demand.

\- Exposed parameters per overlay request: `Texture2D/Sprite overlayTexture`, `Color tintColor` (optional, for heroes whose effect isn't a pre-colored texture), `float fadeInDuration`, `float holdDuration`, `float fadeOutDuration`, `float maxOpacity`.

\- Implementation: a full-screen `Image` (or `RawImage`) on a dedicated Canvas layer, alpha-animated via coroutine or simple tween (no shader required, per developer's choice — plain alpha lerp on the Image's CanvasRenderer/color alpha is sufficient).

\- The system must support being called by any hero's Ultimate-cast code without that hero needing to know about other heroes' overlays — i.e. a simple `TriggerOverlay(OverlayConfig config)`-style public method, not hardcoded to Frost Warden.

\- Respect §3.6 (UI scaling/Canvas Scaler conventions already established) so the overlay scales correctly across supported aspect ratios.



\### 2.3 Frost Warden Ultimate Wiring



\- On Frost Warden's Ultimate cast, call the new overlay system with a frost/window-frost-style static texture (use an existing suitable texture/sprite from already-imported assets if one fits, e.g. from the Synty VFX/UI packs — flag if nothing suitable is found rather than sourcing new art).

\- Suggested defaults (tunable, not hardcoded as unchangeable): short fade-in (\~0.1s), brief hold (\~0.3s), short fade-out (\~0.1s), totaling roughly the \~0.5s the developer described, at a low max opacity (e.g. 0.3–0.4 range) so the arena remains visible underneath. Expose all of these as Inspector-tunable fields on whatever config asset/component holds Frost Warden's overlay settings, rather than hardcoding the values inline.



\---



\## 3. Out of Scope



\- Building or wiring overlay effects for Bolt Striker, Pyromancer, or Marksman — system must support it generically, but only Frost Warden's is implemented now.

\- Any custom shader work for the overlay — explicitly a simple texture + alpha animation per developer's choice.

\- Changes to Frost Zone's slow/freeze percentages, duration, or any other gameplay-balance numbers — depth coverage only.

\- Any change to the permanent Frost emission shader on Frost Warden's model (separate, unrelated system) — don't touch.



\---



\## 4. Acceptance Criteria



\- \[ ] Frost Zone now visually and functionally covers the full arena depth (spawn edge to wall), not just full width.

\- \[ ] Enemies anywhere within the expanded zone area are correctly affected by the existing freeze/slow logic — verified across the full depth, not just near the wall.

\- \[ ] No change to Frost Zone's underlying slow/freeze gameplay values.

\- \[ ] A generic `ScreenCastOverlayController` (or equivalent) exists, callable by any hero without hero-specific coupling.

\- \[ ] Frost Warden's Ultimate cast triggers a brief (\~0.5s total), low-opacity, frost-styled full-screen overlay using a simple texture + alpha animation.

\- \[ ] Overlay timing (fade in/hold/fade out) and max opacity are exposed as tunable values, not hardcoded.

\- \[ ] Overlay scales correctly per existing Canvas Scaler/UI scaling conventions (§3.6).

\- \[ ] System is structured so adding a second hero's overlay later requires no changes to `ScreenCastOverlayController` itself — only a new config + a new trigger call from that hero's Ultimate code.

