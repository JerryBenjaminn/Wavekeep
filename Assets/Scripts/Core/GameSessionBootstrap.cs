using UnityEngine;
using Wavekeep.Abilities;
using Wavekeep.Economy;
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

            Session = new GameSession(
                eventBus, enemyPool, interactionInput, currencyManager, xpManager,
                upgradeInventory, consumableInventory, pauseState);
        }

        private void OnDestroy()
        {
            Session?.Teardown();
        }
    }
}
