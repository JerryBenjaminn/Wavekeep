using System.Collections.Generic;
using UnityEngine;
using Wavekeep.Abilities;
using Wavekeep.Core;
using Wavekeep.Core.Events;
using Wavekeep.Data;
using Wavekeep.Economy;
using Wavekeep.Gear;
using Wavekeep.Waves;

namespace Wavekeep.Runtime
{
    /// <summary>
    /// The real, data-driven hero driver (CLAUDE.md §3.2) — replaces Task 04's throwaway
    /// <c>HeroAbilityController</c>. Holds the selected <see cref="HeroDefinitionSO"/> and one
    /// <see cref="AbilityRuntime"/> for each of its Basic and Ultimate abilities, orchestrating them
    /// without knowing their internals: it ticks both each frame, auto-fires the basic (idle/auto-
    /// battler), and triggers the ultimate on a placeholder debug key (no charge resource yet).
    ///
    /// It is hero-agnostic: ALL differentiation comes from <see cref="HeroDefinitionSO"/> field/asset
    /// values (abilities + tint), never code branching — so a third hero is purely new assets.
    ///
    /// Design note: HeroRuntime is a MonoBehaviour (unlike the plain-C# EnemyRuntime/AbilityRuntime)
    /// because there is exactly one, scene-bound, player hero that needs a transform, per-frame tick,
    /// and input. A single Update is not the §3.4 concern (which targets hundreds of enemies). The
    /// heavy ability logic stays in the plain-C#, testable <see cref="AbilityRuntime"/>.
    /// </summary>
    [AddComponentMenu("Wavekeep/Runtime/Hero Runtime")]
    public sealed class HeroRuntime : MonoBehaviour
    {
        [Tooltip("Task 36: when ON this hero auto-casts its Ultimate the moment it is ready and a target is " +
                 "in range. HeroTeamController toggles this globally; when OFF the Ultimate waits for that " +
                 "hero's manual key via TryCastUltimate. Input itself lives in HeroTeamController, not here, " +
                 "so two heroes from the same prefab don't fight over one key.")]
        [SerializeField] private bool _autoUltimate = true;

        [Tooltip("Task 56: fail-safe (seconds). If a basic attack's impact-frame Animation Event never arrives " +
                 "(e.g. it's missing/misplaced on the clip), the basic's damage/VFX is applied anyway after this " +
                 "long so the attack can't get permanently stuck. Only used on the animation-gated path.")]
        [SerializeField, Min(0.1f)] private float _basicAttackImpactTimeout = 1.2f;

        public HeroDefinitionSO Definition { get; private set; }
        public IAbility Basic { get; private set; }
        public IAbility Ultimate { get; private set; }

        /// <summary>Task 36: per-hero auto-ultimate flag (set by <c>HeroTeamController</c>'s global toggle).</summary>
        public bool AutoUltimate { get => _autoUltimate; set => _autoUltimate = value; }

        /// <summary>Task 22: the basic/ultimate's FINAL stats, resolved through the SAME inputs this hero
        /// feeds the abilities each frame (upgrades + consumables + equipped gear). Null until the ability
        /// exists. The stat panel reads these so its numbers match real execution exactly.</summary>
        public AbilityStats? BasicStats => Basic?.ResolveStats(_upgrades, _consumables, _equippedModifiers);
        public AbilityStats? UltimateStats => Ultimate?.ResolveStats(_upgrades, _consumables, _equippedModifiers);

        private WaveSpawner _waveSpawner;
        private EventBus _events; // Task 43: publish ApexUnlockedEvent so the discovery manager can record/notify
        private UpgradeInventory _upgrades;
        private ConsumableInventory _consumables;
        private ComboApexState _comboApex; // Task 38: cross-hero combo resolver (shared run-scoped service)
        private PauseState _pause;
        private IScreenCastOverlay _screenOverlay; // Task 57: hero Ultimate-cast full-screen flash (may be null)
        private IAbilityFeedback _feedback;
        private IReadOnlyList<StatModifier> _equippedModifiers;
        private LuckState _luck;
        private bool _initialized;

        // Task 56: animation-gated basic auto-attack. When the hero's model has a usable Attack animation, the
        // basic's damage/VFX is deferred from "ready + target in range" to the Attack clip's impact frame. When
        // it doesn't (controller empty/unassigned, or a non-animated placeholder hero), _animationGatedBasic is
        // false and the basic keeps its original immediate auto-fire — so this can never regress the attack.
        private HeroAnimationDriver _animDriver;
        private bool _animationGatedBasic;
        private bool _basicAttackPending;   // an Attack clip is playing; its impact hasn't applied yet
        private float _basicPendingElapsed; // counts up while pending; drives the impact fail-safe timeout

        // Task 29: per-line tier state (0 = not yet picked, 1–3 = current tier). This IS the line
        // progression model — there is no flat list of picked upgrade ids. The picked tier's effect is
        // pushed into the existing UpgradeInventory (so AbilityRuntime resolves it unchanged); this map is
        // the source of truth for "which tier is each line at".
        private readonly Dictionary<UpgradeLineDefinitionSO, int> _lineTiers =
            new Dictionary<UpgradeLineDefinitionSO, int>();
        private IReadOnlyList<ApexTalentDefinitionSO> _apexTalents;
        private readonly HashSet<ApexTalentDefinitionSO> _unlockedApexes = new HashSet<ApexTalentDefinitionSO>();
        // Unlocked apex abilities — independent IAbility instances ticked + auto-fired each frame, with no
        // player input, alongside (but separate from) Basic/Ultimate.
        private readonly List<IAbility> _apexAbilities = new List<IAbility>();

        // Task 31 (Pass 2): persistent ground/zone effects (Frost Zone, Frozen Ground patches). Owned here
        // (per-run, no static singleton); abilities spawn into it via the execution context, and it is
        // ticked below each frame — frozen with the rest of gameplay while paused.
        private readonly GroundZoneManager _zones = new GroundZoneManager();

        // Task 35: caster-scoped combat state (Bolt Striker's Static Charge combo). Owned here so the basic
        // and the Lethal Surge apex — separate AbilityRuntime instances — share one combo counter.
        private readonly HeroCombatState _combatState = new HeroCombatState();

        // Task 48: Pyromancer's Burn-reaction service (Spreading Flame death-spread + Combustion expiry-detonation).
        // Owned per-run (no static singleton); polled each frame with the live enemy list + held upgrades. Only
        // ticked for a hero whose abilities are fire-based (data flag, not identity), so it is inert otherwise.
        private readonly FireSubsystem _fire = new FireSubsystem();
        private bool _hasFireReactions;

        /// <summary>Task 24: the hero's live total Luck (hero base + summed equipped-gear Luck + in-run
        /// potion bonus), clamped to 0–100. Delegates to the run's <see cref="LuckState"/>, which is the
        /// single numeric source of truth the shop/loot rolls reweight against — so the stat panel, the
        /// shop, and loot all read the SAME value. 0 until <see cref="Initialize"/> runs.</summary>
        public float CurrentLuck => _luck != null ? _luck.CurrentLuck : 0f;

        /// <summary>
        /// Configure this hero instance from its definition + run services. Called by the hero-select
        /// flow right after the prefab is instantiated. Applies the placeholder tint and builds the
        /// two ability runtimes from the definition's ability assets.
        /// </summary>
        public void Initialize(HeroDefinitionSO definition, GameSession session, WaveSpawner waveSpawner)
        {
            Definition = definition;
            _waveSpawner = waveSpawner;
            _events = session.Events; // Task 43: announce apex unlocks for the discovery/Codex system
            _upgrades = session.UpgradeInventory;
            _consumables = session.ConsumableInventory;
            _comboApex = session.ComboApex; // Task 38: same resolver for every hero; reads the shared registry
            _pause = session.PauseState;
            _screenOverlay = session.ScreenCastOverlay; // Task 57: triggered on this hero's Ultimate cast (null-safe)

            // Task 12: this hero's equipped gear/artifact modifiers (a live view of the loadout's
            // aggregated list) feed AbilityRuntime's existing modifier pipeline. Equip happens between
            // runs, so it's stable during a run; reading the live reference keeps it correct regardless.
            var loadout = session.GearManager.GetLoadout(definition);
            _equippedModifiers = loadout.AggregatedModifiers;

            // Task 24: feed the two persistent Luck sources (hero base + summed equipped-gear Luck) into the
            // run's LuckState. The gear portion is read from the loadout's recompute-on-change total (not per
            // frame); the third source — in-run Luck potions — is added to LuckState by the shop and resets
            // each run. CurrentLuck (read by the stat panel + shop + loot) is the clamped sum of all three.
            _luck = session.LuckState;
            _luck?.SetHeroLuck(definition.BaseLuck, loadout.TotalLuckBonus);

            // Task 08: self-contained visual feedback — no scene/prefab wiring. Added on the hero so its
            // beam/ring originate at the caster. AddComponent runs its Awake synchronously (hero is active).
            if (!TryGetComponent(out AbilityIndicatorPresenter presenter))
            {
                presenter = gameObject.AddComponent<AbilityIndicatorPresenter>();
            }

            // Task 45: also add the frost ability-VFX presenter and fan feedback to BOTH via a composite —
            // the generic presenter draws the diagnostic beam/ring, the frost presenter draws the projectile/
            // burst/zone effects. Each reacts only to the calls it implements, so no double-draw, no parallel
            // trigger path. Inert (no allocation) for heroes whose abilities never call the frost methods.
            if (!TryGetComponent(out FrostVfxPresenter frostPresenter))
            {
                frostPresenter = gameObject.AddComponent<FrostVfxPresenter>();
            }

            // Task 46: Bolt Striker's electrical VFX presenter joins the same composite. Inert (no allocation)
            // for heroes whose abilities never call the lightning methods (gated by AbilityVfxStyle.Lightning).
            if (!TryGetComponent(out LightningVfxPresenter lightningPresenter))
            {
                lightningPresenter = gameObject.AddComponent<LightningVfxPresenter>();
            }

            // Task 47: high-impact apex / combo-apex VFX presenter joins the composite too. Inert until an
            // apex fires (OnApexImpact) or Frozen Lightning resolves (OnComboFrozenLightning).
            if (!TryGetComponent(out ApexVfxPresenter apexPresenter))
            {
                apexPresenter = gameObject.AddComponent<ApexVfxPresenter>();
            }

            // Task 51: Pyromancer's fire VFX presenter joins the composite. Inert (no allocation) for heroes whose
            // abilities never call the fire methods (Fireball impact / Combustion / Spreading Flame / Firewall).
            if (!TryGetComponent(out FireVfxPresenter firePresenter))
            {
                firePresenter = gameObject.AddComponent<FireVfxPresenter>();
            }

            // Task 52: Marksman's kinetic VFX presenter joins the composite. Inert for heroes whose abilities never
            // call the kinetic methods (tracers / pierce sparks / Armor Shred indicator / Minigun spin-up).
            if (!TryGetComponent(out KineticVfxPresenter kineticPresenter))
            {
                kineticPresenter = gameObject.AddComponent<KineticVfxPresenter>();
            }
            _feedback = new CompositeAbilityFeedback(
                presenter, frostPresenter, lightningPresenter, apexPresenter, firePresenter, kineticPresenter);

            Basic = definition.BasicAbility != null
                ? new AbilityRuntime(definition.BasicAbility, AbilityRole.Basic) : null;
            Ultimate = definition.UltimateAbility != null
                ? new AbilityRuntime(definition.UltimateAbility, AbilityRole.Ultimate) : null;

            // Task 48: only run the Burn-reaction poller for a fire hero (data-flagged abilities, not identity),
            // so it stays inert for every other hero even though they share the run's UpgradeInventory.
            _hasFireReactions =
                (definition.BasicAbility != null && definition.BasicAbility.AppliesBurnOnHit) ||
                (definition.UltimateAbility != null && definition.UltimateAbility.AppliesFireWall);

            // Task 29: seed every owned line at tier 0, and remember the hero's apex talents so unlocks can
            // be checked as lines reach max tier. No upgrades are picked yet, so no apex is unlocked here.
            _lineTiers.Clear();
            _unlockedApexes.Clear();
            _apexAbilities.Clear();
            if (definition.UpgradeLines != null)
            {
                for (int i = 0; i < definition.UpgradeLines.Count; i++)
                {
                    var line = definition.UpgradeLines[i];
                    if (line != null && !_lineTiers.ContainsKey(line)) _lineTiers[line] = 0;
                }
            }
            _apexTalents = definition.ApexTalents;

            // Task 36: register into the run's hero set so the team input, level-up pool, and cooldown HUD
            // can reach every active hero through GameSession (no static singleton). Idempotent.
            session.Heroes?.Register(this);

            // Task 56: find the model's Animator (it may sit on a child of the hero root) and attach the generic
            // HeroAnimationDriver to ITS GameObject so Unity's Animation Events reach it. Bind the basic's impact
            // callback and cache whether this hero can actually drive an Attack animation (controller assigned +
            // Attack state + Attack trigger). If not, the basic stays on the original immediate path below.
            _basicAttackPending = false;
            _basicPendingElapsed = 0f;
            var animator = GetComponentInChildren<Animator>(true);
            if (animator != null)
            {
                _animDriver = animator.GetComponent<HeroAnimationDriver>();
                if (_animDriver == null) _animDriver = animator.gameObject.AddComponent<HeroAnimationDriver>();
                _animDriver.BindBasicAttackImpact(HandleBasicAttackImpact);
                _animationGatedBasic = _animDriver.CanDriveAttack;
            }
            else
            {
                _animDriver = null;
                _animationGatedBasic = false;
            }

            ApplyTint(definition.Tint);
            _initialized = true;
        }

        // --- Task 29: upgrade-line progression + apex talents -------------------------------------

        /// <summary>The lines this hero owns (from its definition), or null.</summary>
        public IReadOnlyList<UpgradeLineDefinitionSO> UpgradeLines =>
            Definition != null ? Definition.UpgradeLines : null;

        /// <summary>Task 32: the currently-UNLOCKED apex abilities (live runtime instances), for the
        /// cooldown HUD to read directly. Empty until an apex unlocks; grows as more unlock.</summary>
        public IReadOnlyList<IAbility> ApexAbilities => _apexAbilities;

        /// <summary>Task 38: true if the given single-hero apex is currently unlocked on this hero. The
        /// cross-hero combo resolver (<c>ComboApexState</c>) queries this across active heroes to decide
        /// whether a combo apex (e.g. Frozen Lightning) is live.</summary>
        public bool IsApexUnlocked(ApexTalentDefinitionSO apex) =>
            apex != null && _unlockedApexes.Contains(apex);

        /// <summary>Current tier of a line (0 = not yet picked, 1–3). Unknown lines read 0.</summary>
        public int GetLineTier(UpgradeLineDefinitionSO line) =>
            line != null && _lineTiers.TryGetValue(line, out int tier) ? tier : 0;

        /// <summary>True once a line is at its max tier (no further picks possible).</summary>
        public bool IsLineMaxed(UpgradeLineDefinitionSO line) =>
            line != null && GetLineTier(line) >= line.TierCount;

        /// <summary>Advance a line by one tier (the level-up card pick). Pushes the new tier's effect into
        /// the run's UpgradeInventory — the SAME inventory AbilityRuntime resolves against, so the line's
        /// effect applies through the existing pipeline with no parallel path. Then re-checks apex unlocks.
        /// Returns false (no change) if the line is null, not owned, or already maxed.</summary>
        public bool TryUpgradeLine(UpgradeLineDefinitionSO line)
        {
            if (line == null || !_lineTiers.ContainsKey(line)) return false;

            int current = _lineTiers[line];
            if (current >= line.TierCount) return false; // already maxed

            // Task 31 REPLACE semantics: a line holds only its CURRENT tier's effect in the UpgradeInventory,
            // so the designer-authored per-tier values are ABSOLUTE (Tier 3 = "+50%", not +15%+30%+50%).
            // Swap the previous tier's effect out before adding the new one.
            if (current >= 1)
            {
                var prevTier = line.TierAt(current);
                if (prevTier != null && prevTier.Effect != null) _upgrades?.Remove(prevTier.Effect);
            }

            int next = current + 1;
            _lineTiers[line] = next;

            var tier = line.TierAt(next);
            if (tier != null && tier.Effect != null)
            {
                // Feed the existing resolution engine — same pipeline AbilityRuntime already reads.
                _upgrades?.Add(tier.Effect);
            }
            Debug.Log($"[HeroRuntime] Line '{line.LineName}' → Tier {next}/{line.TierCount}.");

            CheckApexUnlocks();
            return true;
        }

        // After any tier change, unlock every apex whose required lines are now ALL at max tier. An unlocked
        // apex becomes a live, auto-firing AbilityRuntime instance (its own cooldown, no player input).
        private void CheckApexUnlocks()
        {
            if (_apexTalents == null) return;
            for (int i = 0; i < _apexTalents.Count; i++)
            {
                var apex = _apexTalents[i];
                if (apex == null || _unlockedApexes.Contains(apex)) continue;
                if (!AllLinesMaxed(apex.RequiredLines)) continue;

                _unlockedApexes.Add(apex); // mark unlocked even if mis-authored, so we don't re-warn each tier

                // Task 43: announce the unlock (BEFORE the ability null-check below, so a mis-authored apex
                // still counts as discovered). _unlockedApexes already contains it, so the discovery manager's
                // combo re-scan sees this hero's apex as live when deciding whether a cross-hero combo unlocked.
                _events?.Publish(new ApexUnlockedEvent(apex));

                if (apex.Ability == null)
                {
                    Debug.LogWarning($"[HeroRuntime] Apex '{apex.ApexName}' unlocked but has no ability assigned.");
                    continue;
                }
                _apexAbilities.Add(new AbilityRuntime(apex.Ability, AbilityRole.Apex));
                Debug.Log($"[HeroRuntime] APEX UNLOCKED: '{apex.ApexName}' — now auto-firing on its own cooldown.");
            }
        }

        private bool AllLinesMaxed(IReadOnlyList<UpgradeLineDefinitionSO> lines)
        {
            if (lines == null || lines.Count == 0) return false; // an apex with no required lines never unlocks
            for (int i = 0; i < lines.Count; i++)
            {
                var line = lines[i];
                if (line == null || GetLineTier(line) < line.TierCount) return false;
            }
            return true;
        }

        private void Update()
        {
            if (!_initialized) return;

            // Task 07: freeze ability ticking, auto-fire, ultimate input, and consumable timers while
            // the level-up card picker is up — so the run is genuinely paused for the player's choice.
            if (_pause != null && _pause.IsPaused) return;

            // Advance any timed consumable effects (permanent ones are untouched). The hero is the
            // per-frame consumer of these modifiers, so it owns their tick (Task 06).
            _consumables?.Tick(Time.deltaTime);

            // Task 31: surface the basic's current effective damage so apex/other abilities can scale off it
            // (e.g. Permafrost Eruption = 50% of basic). Resolved through the same pipeline as the stat panel.
            float basicDamage = BasicStats?.Damage ?? 0f;

            // Task 31 (Pass 2): advance persistent ground/zones with the live enemy list + basic damage.
            _zones.Tick(Time.deltaTime, _waveSpawner.ActiveEnemies, basicDamage);

            // Task 48: advance the Burn-reaction poller (Spreading Flame / Combustion) for fire heroes.
            // Task 51: pass the feedback sink so spread/combustion fire their VFX at the exact reaction moment.
            if (_hasFireReactions)
                _fire.Tick(Time.deltaTime, _waveSpawner.ActiveEnemies, _upgrades, basicDamage, _feedback);

            var context = BuildContext(basicDamage);

            if (Basic != null)
            {
                Basic.Tick(Time.deltaTime);
                TickBasicAutoAttack(context); // Task 56: animation-gated when possible, else immediate auto-fire
            }

            if (Ultimate != null)
            {
                Ultimate.Tick(Time.deltaTime);

                // Task 36: auto-ultimate. When the toggle (HeroTeamController) leaves AutoUltimate on, the
                // hero fires its ultimate the instant it is ready and a target is in range — Execute no-ops
                // (and keeps the cooldown) when nothing is in range, so it simply retries. When the toggle is
                // off, the cast comes via TryCastUltimate from the team input instead.
                // Task 49: while a shot-burst ultimate (Minigun) is mid-channel, keep driving its Execute every
                // frame regardless of the auto toggle, so the active burst fires its queued shots either way.
                if (Ultimate.IsChanneling || (AutoUltimate && Ultimate.IsReady))
                {
                    ExecuteUltimateWithOverlay(context); // Task 57: flash the cast overlay on a successful cast
                }
            }

            // Task 29: unlocked apex talents tick + auto-fire on their OWN cooldowns, no player input —
            // they share the same execution context (targets/upgrades/gear) as the Basic/Ultimate. This is
            // unaffected by the auto-ultimate toggle (Task 36), which only governs the player-cast Ultimate.
            for (int i = 0; i < _apexAbilities.Count; i++)
            {
                var apex = _apexAbilities[i];
                apex.Tick(Time.deltaTime);
                apex.Execute(context); // no-ops unless ready AND a target is in range
            }
        }

        // Task 56: drive the basic auto-attack. On the original immediate path the basic fires (damage+VFX) the
        // moment it's ready and a target is in range. On the animation-gated path it instead plays the Attack clip
        // and defers the hit to that clip's impact frame (HandleBasicAttackImpact), so damage/VFX land on the
        // visual swing — and Idle plays whenever no target is in range.
        private void TickBasicAutoAttack(AbilityExecutionContext context)
        {
            if (!_animationGatedBasic)
            {
                Basic.Execute(context); // no-ops unless ready AND a target is in range
                return;
            }

            if (_basicAttackPending)
            {
                // Fail-safe: if the impact Animation Event never arrives (missing/misplaced on the clip), apply
                // the basic after the timeout so an animation-gated attack can't get permanently stuck.
                _basicPendingElapsed += Time.deltaTime;
                if (_basicPendingElapsed >= _basicAttackImpactTimeout) ResolveDeferredBasic();
                return;
            }

            // Only start an attack when the basic is off cooldown AND something is in range — otherwise stay Idle.
            if (Basic.IsReady && Basic.HasTargetInRange(context))
            {
                _animDriver.TriggerAttack();
                _basicAttackPending = true;
                _basicPendingElapsed = 0f;
            }
        }

        // Task 56: invoked from the Attack clip's impact-frame Animation Event (via HeroAnimationDriver) — the
        // single point where the deferred basic's damage/VFX is applied. Guarded against firing while paused
        // (the Animator keeps playing during a PauseState pause) and against stray events when nothing is pending.
        private void HandleBasicAttackImpact()
        {
            if (!_basicAttackPending) return;
            if (_pause != null && _pause.IsPaused)
            {
                _basicAttackPending = false;
                _basicPendingElapsed = 0f;
                return;
            }
            ResolveDeferredBasic();
        }

        // Apply the deferred basic now, with a context rebuilt at THIS moment so targeting reflects current
        // positions. Execute consumes the cooldown on hit, which stops the auto-attacker re-triggering until the
        // basic is ready again. Idempotent per attack (pending is cleared first, so the fail-safe and the event
        // can never both apply the same swing).
        private void ResolveDeferredBasic()
        {
            _basicAttackPending = false;
            _basicPendingElapsed = 0f;
            if (Basic == null) return;
            Basic.Execute(BuildContext(BasicStats?.Damage ?? 0f));
        }

        // Task 36: build the per-frame execution context (extracted so the manual TryCastUltimate path can
        // reuse the exact same context the auto path uses). Cheap — references live lists, allocates nothing.
        private AbilityExecutionContext BuildContext(float basicDamage)
        {
            return new AbilityExecutionContext(
                transform.position, _waveSpawner.ActiveEnemies, _upgrades, _consumables,
                _equippedModifiers, _feedback, basicDamage, _zones,
                _waveSpawner.DefendedLineZ, _waveSpawner.ApproachDirectionZ, // Task 33: full-width Frost Zone band
                _waveSpawner.SpawnLineZ, // Task 53: spawn edge, so Firewall can center at mid-arena depth
                _combatState, // Task 35: shared Static Charge combo
                _comboApex);  // Task 38: cross-hero combo apex resolver (Frozen Lightning prime/consume)
        }

        /// <summary>Task 36: cast this hero's Ultimate on explicit player command (used by
        /// <c>HeroTeamController</c> when the auto-ultimate toggle is off). No-op if not ready or paused, so a
        /// press while charging is harmless. Returns true if it actually fired.</summary>
        public bool TryCastUltimate()
        {
            if (!_initialized || Ultimate == null || !Ultimate.IsReady) return false;
            if (_pause != null && _pause.IsPaused) return false;

            ExecuteUltimateWithOverlay(BuildContext(BasicStats?.Damage ?? 0f));
            Debug.Log($"[HeroRuntime] {Definition.HeroName}: ultimate triggered (manual).");
            return true;
        }

        // Task 57: run the ultimate and, if it actually CAST this call, flash this hero's screen overlay. A
        // successful instant cast (Frost Warden's Frost Zone) flips the ability ready→not-ready as it consumes
        // its cooldown — that edge is the cast moment. (Channeled ultimates stay "ready" while channelling, so
        // they don't surface here yet; their overlays are out of scope until those heroes are wired — flagged.)
        private void ExecuteUltimateWithOverlay(AbilityExecutionContext context)
        {
            bool wasReady = Ultimate.IsReady;
            Ultimate.Execute(context);
            if (wasReady && !Ultimate.IsReady)
                _screenOverlay?.Trigger(Definition.UltimateCastOverlay);
        }

        private void ApplyTint(Color tint)
        {
            // .material instantiates a per-instance material, so we don't mutate the shared asset.
            if (TryGetComponent<Renderer>(out var renderer))
            {
                renderer.material.color = tint;
            }
        }
    }
}
