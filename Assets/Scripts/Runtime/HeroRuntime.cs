using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Wavekeep.Abilities;
using Wavekeep.Core;
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
        [Tooltip("Placeholder manual trigger for the ultimate; real charge mechanics are a later task.")]
        [SerializeField] private Key _ultimateKey = Key.U;

        public HeroDefinitionSO Definition { get; private set; }
        public IAbility Basic { get; private set; }
        public IAbility Ultimate { get; private set; }

        /// <summary>Task 22: the basic/ultimate's FINAL stats, resolved through the SAME inputs this hero
        /// feeds the abilities each frame (upgrades + consumables + equipped gear). Null until the ability
        /// exists. The stat panel reads these so its numbers match real execution exactly.</summary>
        public AbilityStats? BasicStats => Basic?.ResolveStats(_upgrades, _consumables, _equippedModifiers);
        public AbilityStats? UltimateStats => Ultimate?.ResolveStats(_upgrades, _consumables, _equippedModifiers);

        private WaveSpawner _waveSpawner;
        private UpgradeInventory _upgrades;
        private ConsumableInventory _consumables;
        private PauseState _pause;
        private IAbilityFeedback _feedback;
        private IReadOnlyList<StatModifier> _equippedModifiers;
        private LuckState _luck;
        private bool _initialized;

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
            _upgrades = session.UpgradeInventory;
            _consumables = session.ConsumableInventory;
            _pause = session.PauseState;

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
            _feedback = presenter;

            Basic = definition.BasicAbility != null
                ? new AbilityRuntime(definition.BasicAbility, AbilityRole.Basic) : null;
            Ultimate = definition.UltimateAbility != null
                ? new AbilityRuntime(definition.UltimateAbility, AbilityRole.Ultimate) : null;

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

            ApplyTint(definition.Tint);
            _initialized = true;
        }

        // --- Task 29: upgrade-line progression + apex talents -------------------------------------

        /// <summary>The lines this hero owns (from its definition), or null.</summary>
        public IReadOnlyList<UpgradeLineDefinitionSO> UpgradeLines =>
            Definition != null ? Definition.UpgradeLines : null;

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

            int next = current + 1;
            _lineTiers[line] = next;

            var tier = line.TierAt(next);
            if (tier != null && tier.Effect != null)
            {
                // Feed the existing resolution engine — identical to a pre-migration upgrade pick.
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

            var context = new AbilityExecutionContext(
                transform.position, _waveSpawner.ActiveEnemies, _upgrades, _consumables,
                _equippedModifiers, _feedback);

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
                    // Task 21: the cooldown is now a real gate. Only fire if charged; Execute starts the
                    // cooldown on a successful hit (its existing behaviour). A press while charging is a
                    // no-op so the ultimate can't be spammed.
                    if (Ultimate.IsReady)
                    {
                        Ultimate.Execute(context);
                        Debug.Log($"[HeroRuntime] {Definition.HeroName}: ultimate triggered.");
                    }
                    else
                    {
                        Debug.Log($"[HeroRuntime] {Definition.HeroName}: ultimate on cooldown " +
                                  $"({Ultimate.CooldownProgress01 * 100f:0}% charged).");
                    }
                }
            }

            // Task 29: unlocked apex talents tick + auto-fire on their OWN cooldowns, no player input —
            // they share the same execution context (targets/upgrades/gear) as the Basic/Ultimate.
            for (int i = 0; i < _apexAbilities.Count; i++)
            {
                var apex = _apexAbilities[i];
                apex.Tick(Time.deltaTime);
                apex.Execute(context); // no-ops unless ready AND a target is in range
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
