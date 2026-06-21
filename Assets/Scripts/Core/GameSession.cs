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

        // Placeholder service slots — populated by later tasks:
        // TODO (Task 03): CurrencyManager CurrencyManager { get; }
        // TODO (Task 03): XPManager XPManager { get; }
        // TODO (Task 05): HeroRuntime HeroRuntime { get; }
        //
        // Task 02 note: WaveSpawner is a scene MonoBehaviour that *consumes* this session
        // (pulls Events + EnemyPool from it) rather than being held here, so Core has no
        // dependency on the Waves layer. Dependency flow still originates from GameSession (§3.5).

        public GameSession(EventBus eventBus, EnemyPoolManager enemyPool, IInteractionInput interactionInput)
        {
            Events = eventBus;
            EnemyPool = enemyPool;
            InteractionInput = interactionInput;
        }

        /// <summary>Release all session-scoped state. Call when the run/scene ends.</summary>
        public void Teardown()
        {
            Events.UnsubscribeAll();
            EnemyPool.Clear();
        }
    }
}
