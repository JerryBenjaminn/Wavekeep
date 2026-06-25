using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Wavekeep.Abilities;
using Wavekeep.Data;
using Wavekeep.Economy;
using Wavekeep.Gear;
using Wavekeep.Input;
using Wavekeep.Pooling;

namespace Wavekeep.Core
{
    /// <summary>
    /// Thin scene-level entry point (CLAUDE.md §3.5). Its only job is to assemble the
    /// <see cref="GameSession"/> in <see cref="Awake"/> and expose it. Dependent
    /// MonoBehaviours read <see cref="Session"/> in their own Awake/Start — there is no
    /// static <c>Instance</c>.
    /// </summary>
    [AddComponentMenu("Wavekeep/Core/Game Session Bootstrap")]
    public sealed class GameSessionBootstrap : MonoBehaviour
    {
        [Header("Pooling")]
        [Tooltip("Parent transform for pooled enemy instances. Defaults to this object if unset.")]
        [SerializeField] private Transform _poolRoot;

        [Header("Interaction Input")]
        [Tooltip("Camera used to raycast screen taps/clicks into world space. Defaults to Camera.main if unset.")]
        [SerializeField] private Camera _interactionCamera;
        [Tooltip("Layer(s) the interaction raycast tests against (e.g. the Ground/Placement layer).")]
        [SerializeField] private LayerMask _groundLayer;
        [Tooltip("Force a specific input mode for editor testing. Auto picks touch on mobile, mouse otherwise.")]
        [SerializeField] private InteractionInputMode _inputModeOverride = InteractionInputMode.Auto;

        [Header("XP Curve (threshold = base + level * increment)")]
        [Tooltip("Base XP component of the per-level threshold. Tunable placeholder.")]
        [SerializeField, Min(1)] private int _xpBaseAmount = 10;
        [Tooltip("Per-level XP increment added to the threshold each level. Tunable placeholder.")]
        [SerializeField, Min(0)] private int _xpIncrementPerLevel = 5;

        [Header("Shop")]
        [Tooltip("Reroll points the player starts each fresh run with (Task 09). Persists across shop visits.")]
        [SerializeField, Min(0)] private int _startingRerollPoints = 3;

        [Header("Gear (Task 12)")]
        [Tooltip("Master catalog of all gear/artifact items, used to resolve saved item ids on load.")]
        [SerializeField] private GearCatalogSO _gearCatalog;

        [Header("Luck / Tier Weighting (Task 24)")]
        [Tooltip("Tunable inputs for Luck-driven shop + loot tier weighting. If unset, weighting is disabled " +
                 "(flat odds) and Luck still tracks/clamps correctly — so older scenes keep working.")]
        [SerializeField] private TierWeightingConfigSO _tierWeightingConfig;

        [Header("Combo Apexes (Task 38 — cross-hero passive synergies)")]
        [Tooltip("Cross-hero combo apexes available this run (e.g. Frozen Lightning). Each lights up only once " +
                 "BOTH of its referenced single-hero apexes are unlocked across the active heroes. Empty = no " +
                 "combos (older scenes still work).")]
        [SerializeField] private List<ComboApexTalentDefinitionSO> _comboApexes = new List<ComboApexTalentDefinitionSO>();

        /// <summary>The assembled session for this scene. Available after Awake.</summary>
        public GameSession Session { get; private set; }

        private void Awake()
        {
            var eventBus = new EventBus();

            var poolRoot = _poolRoot != null ? _poolRoot : transform;
            var enemyPool = new EnemyPoolManager(poolRoot);

            var camera = _interactionCamera != null ? _interactionCamera : Camera.main;
            var interactionInput = InteractionInputProvider.Create(_inputModeOverride, camera, _groundLayer);

            // Managers subscribe to the bus in their constructors (during Awake), so they process
            // a kill before any Start-subscribed UI reads their state.
            var currencyManager = new CurrencyManager(eventBus);
            var xpManager = new XPManager(eventBus, _xpBaseAmount, _xpIncrementPerLevel);
            var upgradeInventory = new UpgradeInventory();
            var consumableInventory = new ConsumableInventory();
            var pauseState = new PauseState();
            // Fresh manager every scene load → reroll points reset to the starting value only on a brand
            // new run (Task 08 "Play Again" reloads the scene); they persist within a run otherwise.
            var rerollManager = new RerollManager(eventBus, _startingRerollPoints);

            // Task 12: gear is DISK-backed. The manager loads from disk on construction, so a scene
            // reload reconstructs identical persistent state while the per-run services above reset.
            var savePath = Path.Combine(Application.persistentDataPath, GearManager.DefaultSaveFileName);
            var gearManager = new GearManager(_gearCatalog, savePath);

            // Task 24: per-run Luck total + run-progress tracker (fresh each scene load → potion bonus resets).
            // Built before LootService so loot rolls can reweight tiers against current Luck.
            var luckState = new LuckState(eventBus, _tierWeightingConfig);

            // Task 13: subscribes to EnemyKilledEvent (after Currency/XP) to roll drops into the gear
            // manager — an additional consumer of the kill event, not a change to the death path.
            // Task 24: also takes LuckState so drop-tier odds shift (weakly) with Luck.
            var lootService = new LootService(eventBus, gearManager, luckState);

            // Task 36: empty hero registry; the spawn flow registers each active hero into it at run start.
            var heroes = new HeroRegistry();

            // Task 38: cross-hero combo apex resolver. Reads the just-built hero registry (so it sees apex
            // unlocks live) plus the scene-configured combo list — empty when none are wired.
            var comboApex = new ComboApexState(_comboApexes, heroes);

            Session = new GameSession(
                eventBus, enemyPool, interactionInput, currencyManager, xpManager,
                upgradeInventory, consumableInventory, pauseState, rerollManager, gearManager, lootService,
                luckState, heroes, comboApex);
        }

        private void OnDestroy()
        {
            Session?.Teardown();
        }
    }
}
