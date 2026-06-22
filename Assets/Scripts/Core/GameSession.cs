using Wavekeep.Abilities;
using Wavekeep.Economy;
using Wavekeep.Gear;
using Wavekeep.Input;
using Wavekeep.Pooling;

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

        // Placeholder service slots — populated by later tasks:
        // TODO (Task 05): HeroRuntime HeroRuntime { get; }
        //
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
            GearManager gearManager)
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
        }

        /// <summary>Release all session-scoped state. Call when the run/scene ends.</summary>
        public void Teardown()
        {
            // Explicitly unsubscribe the managers before clearing the bus (CLAUDE.md §3.5 lifecycle).
            CurrencyManager?.Dispose();
            XPManager?.Dispose();
            Events.UnsubscribeAll();
            EnemyPool.Clear();
        }
    }
}
