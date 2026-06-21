using UnityEngine;
using UnityEngine.InputSystem;
using Wavekeep.Abilities;
using Wavekeep.Core;
using Wavekeep.Data;
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
        [Tooltip("Placeholder manual trigger for the ultimate; real charge mechanics are a later task.")]
        [SerializeField] private Key _ultimateKey = Key.U;

        public HeroDefinitionSO Definition { get; private set; }
        public IAbility Basic { get; private set; }
        public IAbility Ultimate { get; private set; }

        private WaveSpawner _waveSpawner;
        private UpgradeInventory _upgrades;
        private bool _initialized;

        /// <summary>
        /// Configure this hero instance from its definition + run services. Called by the hero-select
        /// flow right after the prefab is instantiated. Applies the placeholder tint and builds the
        /// two ability runtimes from the definition's ability assets.
        /// </summary>
        public void Initialize(HeroDefinitionSO definition, GameSession session, WaveSpawner waveSpawner)
        {
            Definition = definition;
            _waveSpawner = waveSpawner;
            _upgrades = session.UpgradeInventory;

            Basic = definition.BasicAbility != null ? new AbilityRuntime(definition.BasicAbility) : null;
            Ultimate = definition.UltimateAbility != null ? new AbilityRuntime(definition.UltimateAbility) : null;

            ApplyTint(definition.Tint);
            _initialized = true;
        }

        private void Update()
        {
            if (!_initialized) return;

            var context = new AbilityExecutionContext(transform.position, _waveSpawner.ActiveEnemies, _upgrades);

            if (Basic != null)
            {
                Basic.Tick(Time.deltaTime);
                Basic.Execute(context); // no-ops unless ready AND a target is in range (auto-fire)
            }

            if (Ultimate != null)
            {
                Ultimate.Tick(Time.deltaTime);

                var keyboard = Keyboard.current;
                if (keyboard != null && keyboard[_ultimateKey].wasPressedThisFrame)
                {
                    Ultimate.Execute(context);
                    Debug.Log($"[HeroRuntime] {Definition.HeroName}: ultimate triggered.");
                }
            }
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
