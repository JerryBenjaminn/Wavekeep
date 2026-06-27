using Wavekeep.Abilities;
using Wavekeep.Economy;
using Wavekeep.Gear;
using Wavekeep.Input;
using Wavekeep.Pooling;
using Wavekeep.Progression;

namespace Wavekeep.Core
{
    /// <summary>
    /// Composition root for a single run/scene (CLAUDE.md §3.5). A plain C# object — NOT a
    /// MonoBehaviour and NOT a static singleton. It is constructed by
    /// <see cref="GameSessionBootstrap"/> and owns the shared services. Dependent systems
    /// receive what they need from here via explicit injection rather than global access,
    /// which keeps state from leaking across scene reloads/runs.
    /// </summary>
    public sealed class GameSession
    {
        /// <summary>Decoupled gameplay signal bus (CLAUDE.md §3.3).</summary>
        public EventBus Events { get; }

        /// <summary>Object pool for 3D enemy prefabs (CLAUDE.md §3.5).</summary>
        public EnemyPoolManager EnemyPool { get; }

        /// <summary>Platform-resolved interaction input (raycast into world space, CLAUDE.md §3.7).</summary>
        public IInteractionInput InteractionInput { get; }

        /// <summary>Run-scoped Currency total + spend API (CLAUDE.md §3.2).</summary>
        public CurrencyManager CurrencyManager { get; }

        /// <summary>Run-scoped XP/level tracking (CLAUDE.md §3.2).</summary>
        public XPManager XPManager { get; }

        /// <summary>Per-run held upgrades; hero abilities resolve tag interactions against this (CLAUDE.md §3.8).</summary>
        public UpgradeInventory UpgradeInventory { get; }

        /// <summary>Per-run purchased consumable effects; AbilityRuntime reads its modifiers (Task 06 §2).</summary>
        public ConsumableInventory ConsumableInventory { get; }

        /// <summary>Run-scoped pause flag; gameplay loops freeze while it is set (Task 07 level-up picker).</summary>
        public PauseState PauseState { get; }

        /// <summary>Run-scoped reroll-point pool for the shop (Task 09). Distinct from Currency.</summary>
        public RerollManager RerollManager { get; }

        /// <summary>PERSISTENT gear inventory + per-hero equip loadouts (Task 12). Disk-backed, so it
        /// survives scene reloads/runs unlike the per-run services above (CLAUDE.md §6).</summary>
        public GearManager GearManager { get; }

        /// <summary>Rolls gear drops on enemy death into the GearManager (Task 13).</summary>
        public LootService LootService { get; }

        /// <summary>Per-run Luck total + run-progress tracking (Task 24). Source of truth for the live Luck
        /// the shop and loot rolls reweight against; resets each run like other per-run services.</summary>
        public LuckState LuckState { get; }

        /// <summary>Task 36: the run's active heroes (one or two). Heroes self-register on Initialize; the
        /// level-up pool, team ultimate input, and cooldown HUD read the team from here rather than via a
        /// scene-wide lookup or static singleton (§3.5).</summary>
        public HeroRegistry Heroes { get; }

        /// <summary>Task 38: per-run resolver for cross-hero combo apexes (e.g. Frozen Lightning). Queries the
        /// <see cref="Heroes"/> registry to decide which combos are unlocked, and is read by the ability
        /// runtime (through the execution context) to prime/consume passive-combo targets. Never null —
        /// holds an empty combo list when none are configured for the scene.</summary>
        public ComboApexState ComboApex { get; }

        /// <summary>Task 42: PERSISTENT hero-slot unlock progression. Disk-backed like <see cref="GearManager"/>,
        /// so it survives runs; raised when a run clears a wave milestone (15/30/50). The Hub team-selection
        /// panel gates how many heroes can be brought into a run against
        /// <see cref="HeroSlotUnlockManager.MaxUnlockedHeroSlots"/>.</summary>
        public HeroSlotUnlockManager HeroSlotUnlocks { get; }

        /// <summary>Task 43: PERSISTENT apex/combo-apex discovery state (the Codex). Disk-backed like
        /// <see cref="GearManager"/>/<see cref="HeroSlotUnlocks"/>; records the first-ever unlock of each
        /// talent and announces it via <see cref="Core.Events.TalentDiscoveredEvent"/>. The Hub Codex reads
        /// it to show discovered talents in full and undiscovered ones as "???".</summary>
        public TalentDiscoveryManager TalentDiscovery { get; }

        /// <summary>Task 57: the scene's screen-space cast-overlay system (a UI MonoBehaviour passed in by the
        /// bootstrap), exposed here so any hero can flash its Ultimate-cast overlay through the session rather
        /// than a static singleton (§3.5). Null in scenes/tests that don't wire one — callers null-check.</summary>
        public IScreenCastOverlay ScreenCastOverlay { get; }

        // Task 02 note: WaveSpawner is a scene MonoBehaviour that *consumes* this session
        // (pulls Events + EnemyPool from it) rather than being held here, so Core has no
        // dependency on the Waves layer. Dependency flow still originates from GameSession (§3.5).

        public GameSession(
            EventBus eventBus,
            EnemyPoolManager enemyPool,
            IInteractionInput interactionInput,
            CurrencyManager currencyManager,
            XPManager xpManager,
            UpgradeInventory upgradeInventory,
            ConsumableInventory consumableInventory,
            PauseState pauseState,
            RerollManager rerollManager,
            GearManager gearManager,
            LootService lootService,
            LuckState luckState,
            HeroRegistry heroes,
            ComboApexState comboApex,
            HeroSlotUnlockManager heroSlotUnlocks,
            TalentDiscoveryManager talentDiscovery,
            IScreenCastOverlay screenCastOverlay = null) // Task 57: optional so existing callers/tests are unaffected
        {
            Events = eventBus;
            EnemyPool = enemyPool;
            InteractionInput = interactionInput;
            CurrencyManager = currencyManager;
            XPManager = xpManager;
            UpgradeInventory = upgradeInventory;
            ConsumableInventory = consumableInventory;
            PauseState = pauseState;
            RerollManager = rerollManager;
            GearManager = gearManager;
            LootService = lootService;
            LuckState = luckState;
            Heroes = heroes;
            ComboApex = comboApex;
            HeroSlotUnlocks = heroSlotUnlocks;
            TalentDiscovery = talentDiscovery;
            ScreenCastOverlay = screenCastOverlay;
        }

        /// <summary>Release all session-scoped state. Call when the run/scene ends.</summary>
        public void Teardown()
        {
            // Explicitly unsubscribe the managers before clearing the bus (CLAUDE.md §3.5 lifecycle).
            CurrencyManager?.Dispose();
            XPManager?.Dispose();
            LootService?.Dispose();
            LuckState?.Dispose();
            HeroSlotUnlocks?.Dispose(); // Task 42: unsubscribe wave/run listeners (§3.5 lifecycle)
            TalentDiscovery?.Dispose(); // Task 43: unsubscribe apex-unlocked listener (§3.5 lifecycle)
            Heroes?.Clear(); // Task 36: drop hero references so none leak across a scene reload (§3.5)
            Events.UnsubscribeAll();
            EnemyPool.Clear();
        }
    }
}
