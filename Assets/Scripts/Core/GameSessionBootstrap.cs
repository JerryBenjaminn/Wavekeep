using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Wavekeep.Abilities;
using Wavekeep.Data;
using Wavekeep.Economy;
using Wavekeep.Gear;
using Wavekeep.Input;
using Wavekeep.Pooling;
using Wavekeep.Progression;

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

        [Header("XP Curve (threshold = base + level*increment + quadratic*level²)")]
        [Tooltip("Base XP component of the per-level threshold. Tunable placeholder.")]
        [SerializeField, Min(1)] private int _xpBaseAmount = 10;
        [Tooltip("Per-level XP increment added to the threshold each level. Tunable placeholder.")]
        [SerializeField, Min(0)] private int _xpIncrementPerLevel = 5;
        [Tooltip("Task 63: quadratic coefficient (×level²) that flattens early-game level front-loading " +
                 "so the player can't blow through 6+ levels in a single early wave. 0 = original linear curve.")]
        [SerializeField, Min(0)] private int _xpQuadraticPerLevel = 2;

        [Header("Shop")]
        [Tooltip("Reroll points the player starts each fresh run with (Task 09). Persists across shop visits.")]
        [SerializeField, Min(0)] private int _startingRerollPoints = 3;

        [Header("Gear (Task 12)")]
        [Tooltip("Master catalog of all gear/artifact items, used to resolve saved item ids on load.")]
        [SerializeField] private GearCatalogSO _gearCatalog;

        [Tooltip("Task 68: affix-count-per-rarity config + shared affix pool, used by drop generation to roll an " +
                 "instance's affixes. Optional — if unset, drops still generate (implicit only, no affixes). " +
                 "Authored/wired by the Task 68 setup menu.")]
        [SerializeField] private GearAffixCountConfigSO _gearAffixConfig;

        [Tooltip("Task 71: gear economy tuning (inventory capacity, salvage Dust yields, Artifact Forge costs). " +
                 "Optional — if unset, inventory is uncapped and salvage/forge are unavailable. Authored/wired by " +
                 "the Task 71 setup menu.")]
        [SerializeField] private GearEconomyConfigSO _gearEconomyConfig;

        [Header("Luck / Tier Weighting (Task 24)")]
        [Tooltip("Tunable inputs for Luck-driven shop + loot tier weighting. If unset, weighting is disabled " +
                 "(flat odds) and Luck still tracks/clamps correctly — so older scenes keep working.")]
        [SerializeField] private TierWeightingConfigSO _tierWeightingConfig;

        [Header("Combo Apexes (Task 38 — cross-hero passive synergies)")]
        [Tooltip("Cross-hero combo apexes available this run (e.g. Frozen Lightning). Each lights up only once " +
                 "BOTH of its referenced single-hero apexes are unlocked across the active heroes. Empty = no " +
                 "combos (older scenes still work).")]
        [SerializeField] private List<ComboApexTalentDefinitionSO> _comboApexes = new List<ComboApexTalentDefinitionSO>();

        [Header("Hero Slot Unlocks (Task 42 — persistent meta-progression)")]
        [Tooltip("Wave a single run must CLEAR to permanently unlock each extra hero slot beyond slot 1: " +
                 "element 0 → slot 2, element 1 → slot 3, element 2 → slot 4. Ascending. Slot 1 is always " +
                 "unlocked. Tunable here, not hardcoded in the manager.")]
        [SerializeField] private int[] _heroSlotWaveMilestones = { 15, 30, 50 };

        [Header("Screen Cast Overlay (Task 57 — hero Ultimate-cast full-screen flash)")]
        [Tooltip("Scene's ScreenCastOverlayController (on the overlay Canvas). Optional — leave null to disable " +
                 "all hero cast overlays. Wired by the Task 57 setup menu.")]
        [SerializeField] private Wavekeep.UI.ScreenCastOverlayController _screenCastOverlay;

        [Header("Audio (Task 59 — data-driven audio system)")]
        [Tooltip("WavekeepAudioConfig (mixer groups + pool sizes + per-event placeholder cues). Optional — leave " +
                 "null to disable audio. Created/wired by the Task 59 setup menu.")]
        [SerializeField] private Wavekeep.Audio.AudioConfigSO _audioConfig;

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
            var xpManager = new XPManager(eventBus, _xpBaseAmount, _xpIncrementPerLevel, _xpQuadraticPerLevel);
            var upgradeInventory = new UpgradeInventory();
            var consumableInventory = new ConsumableInventory();
            var pauseState = new PauseState();
            // Fresh manager every scene load → reroll points reset to the starting value only on a brand
            // new run (Task 08 "Play Again" reloads the scene); they persist within a run otherwise.
            var rerollManager = new RerollManager(eventBus, _startingRerollPoints);

            // Task 12: gear is DISK-backed. The manager loads from disk on construction, so a scene
            // reload reconstructs identical persistent state while the per-run services above reset.
            var savePath = Path.Combine(Application.persistentDataPath, GearManager.DefaultSaveFileName);
            // Task 71: also takes the affix config (so the Artifact Forge rolls affixes per rarity) and the economy
            // config (inventory cap, salvage yields, forge costs). Both optional — null degrades gracefully.
            var gearManager = new GearManager(_gearCatalog, savePath, _gearAffixConfig, _gearEconomyConfig);

            // Task 24: per-run Luck total + run-progress tracker (fresh each scene load → potion bonus resets).
            // Built before LootService so loot rolls can reweight tiers against current Luck.
            var luckState = new LuckState(eventBus, _tierWeightingConfig);

            // Task 13: subscribes to EnemyKilledEvent (after Currency/XP) to roll drops into the gear
            // manager — an additional consumer of the kill event, not a change to the death path.
            // Task 24: also takes LuckState so drop-tier odds shift (weakly) with Luck.
            // Task 68: generates a fresh GearInstance per drop (slot + Luck-weighted rarity + affixes); the
            // affix config supplies the per-rarity affix count + shared pool (null = implicit-only drops).
            var lootService = new LootService(eventBus, gearManager, luckState, _gearAffixConfig);

            // Task 36: empty hero registry; the spawn flow registers each active hero into it at run start.
            var heroes = new HeroRegistry();

            // Task 38: cross-hero combo apex resolver. Reads the just-built hero registry (so it sees apex
            // unlocks live) plus the scene-configured combo list — empty when none are wired.
            var comboApex = new ComboApexState(_comboApexes, heroes);

            // Task 42: persistent hero-slot unlocks. Disk-backed like gear (its own file), reloaded on each
            // scene build so a slot unlocked in a run is visible the moment the Hub rebuilds. Subscribes to
            // WaveCompleted/RunEnded to evaluate milestones — inert in the Hub scene where neither fires.
            var unlockPath = Path.Combine(Application.persistentDataPath, HeroSlotUnlockManager.DefaultSaveFileName);
            var heroSlotUnlocks = new HeroSlotUnlockManager(eventBus, unlockPath, _heroSlotWaveMilestones);

            // Task 43: persistent apex/combo-apex discovery (Codex). Disk-backed (own file), reloaded on each
            // scene build so the Hub Codex sees fresh discoveries. Subscribes to ApexUnlockedEvent and reads the
            // combo resolver to credit cross-hero combos — inert in the Hub scene (no apex unlocks fire there).
            var discoveryPath = Path.Combine(Application.persistentDataPath, TalentDiscoveryManager.DefaultSaveFileName);
            var talentDiscovery = new TalentDiscoveryManager(eventBus, comboApex, discoveryPath);

            // Task 59: audio engine. Subscribes its own event listener in the ctor (like the managers above), so
            // a kill/wave/level-up the first frame already plays. Sources are parented under this bootstrap.
            var audioManager = new Wavekeep.Audio.AudioManager(eventBus, transform, _audioConfig);

            Session = new GameSession(
                eventBus, enemyPool, interactionInput, currencyManager, xpManager,
                upgradeInventory, consumableInventory, pauseState, rerollManager, gearManager, lootService,
                luckState, heroes, comboApex, heroSlotUnlocks, talentDiscovery,
                _screenCastOverlay, // Task 57: scene overlay UI (null-safe if unwired)
                audioManager);       // Task 59: audio engine (null-safe config)
        }

        // Task 59: start this scene's looping background music + ambient bed once the session is built (Awake).
        // Both no-op for null/clip-less cues, so it's harmless until the developer assigns clips. The victory/
        // defeat cues crossfade over the music on run end; the ambient loop runs until the scene unloads
        // (AudioManager.Dispose stops it). Scene-specific music selection can grow from here later.
        private void Start()
        {
            var audio = Session?.AudioManager;
            if (audio == null || _audioConfig == null) return;
            audio.PlayMusicTrack(_audioConfig.BackgroundMusicCue, crossfade: false);
            audio.StartLoopingCue(_audioConfig.AmbientCue);
        }

        // Task 59: drive the music crossfade (AudioManager has no Update of its own). Unscaled so audio is
        // unaffected by any timescale changes; cheap no-op when nothing is crossfading.
        private void Update()
        {
            Session?.AudioManager?.Tick(Time.unscaledDeltaTime);
        }

        private void OnDestroy()
        {
            Session?.Teardown();
        }
    }
}
