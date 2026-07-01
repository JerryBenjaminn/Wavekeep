using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Wavekeep.Core;
using Wavekeep.Core.Events;
using Wavekeep.Data;
using Wavekeep.Gear;

namespace Wavekeep.Runtime
{
    /// <summary>
    /// Debug stand-in for the real loot/hub flow (Task 13/14), updated for the Task 67 instance model. Provides
    /// keyboard triggers to SPAWN a test <see cref="GearInstance"/>, equip/unequip it on the active hero, and
    /// inspect state — enough to verify the full data/equip/persistence/stat pipeline. Drives nothing in normal
    /// play; gated behind <see cref="_enableDebugKeys"/>.
    ///
    /// Keys: G = spawn a random debug instance (random base + rarity, affixes rolled at their midpoint — this is
    /// a MINIMAL debug spawn, NOT the real weighted drop generation, which is a later task); J = equip the first
    /// owned instance on the active hero; L = unequip everything; K = log inventory + loadout + the basic
    /// ability's gear-inclusive effective damage (proving stats flow through the unchanged AbilityRuntime pipeline).
    /// Task 71 adds: S = salvage the first owned instance (→ Dust); F = forge an Artifact, cycling the target rarity
    /// each press (spends Dust); O = resolve the first overflow item (claim into inventory, or salvage if full).
    /// </summary>
    [AddComponentMenu("Wavekeep/Debug/Gear Debug Controller")]
    public sealed class GearDebugController : MonoBehaviour
    {
        [SerializeField] private bool _enableDebugKeys = true;
        [SerializeField] private GameSessionBootstrap _bootstrap;

        [Header("Task 67 — instance spawn")]
        [Tooltip("Gear bases the G key spawns instances from (authored by the Task 67 setup).")]
        [SerializeField] private List<GearBaseSO> _debugBases = new List<GearBaseSO>();
        [Tooltip("Affix-count config + shared pool used to roll the spawned instance's affixes (Task 67 setup).")]
        [SerializeField] private GearAffixCountConfigSO _affixConfig;

        [Header("Legacy (Task 12) — retained for older setup wiring; granted via the temporary drop bridge")]
        [Tooltip("Legacy finished items. DEAD as an ownership source (see GearCatalogSO); kept so older setup " +
                 "scripts that wire this field don't break, and so G can still grant something if no bases are wired.")]
        [SerializeField] private List<LootItemSO> _sampleItems = new List<LootItemSO>();

        private EventBus _events;
        private GearManager _gear;
        private HeroDefinitionSO _activeHero;
        private int _forgeRarityCursor; // Task 71: cycles the forge target rarity per F press

        private void Start()
        {
            if (_bootstrap == null || _bootstrap.Session == null)
            {
                Debug.LogWarning("[GearDebugController] No GameSessionBootstrap/Session; disabling.", this);
                enabled = false;
                return;
            }

            _events = _bootstrap.Session.Events;
            _gear = _bootstrap.Session.GearManager;
            _events.Subscribe<HeroSelectedEvent>(OnHeroSelected);
        }

        private void OnDestroy()
        {
            if (_events != null) _events.Unsubscribe<HeroSelectedEvent>(OnHeroSelected);
        }

        private void OnHeroSelected(HeroSelectedEvent evt) => _activeHero = evt.Hero;

        private void Update()
        {
            if (!_enableDebugKeys) return;
            var keyboard = Keyboard.current;
            if (keyboard == null) return;

            if (keyboard[Key.G].wasPressedThisFrame) GrantRandom();
            if (keyboard[Key.J].wasPressedThisFrame) EquipFirstOwned();
            if (keyboard[Key.L].wasPressedThisFrame) UnequipAll();
            if (keyboard[Key.K].wasPressedThisFrame) LogState();
            if (keyboard[Key.S].wasPressedThisFrame) SalvageFirstOwned();   // Task 71
            if (keyboard[Key.F].wasPressedThisFrame) ForgeNextRarity();     // Task 71
            if (keyboard[Key.O].wasPressedThisFrame) ResolveOverflow();     // Task 71
        }

        // --- Task 71 debug hooks (placeholder for the eventual Hub salvage/forge UI) -----------------

        private void SalvageFirstOwned()
        {
            var first = _gear.Inventory.Count > 0 ? _gear.Inventory.Items[0] : null;
            if (first == null) { Debug.LogWarning("[GearDebugController] Inventory empty — nothing to salvage."); return; }
            int dust = _gear.Salvage(first.ItemId);
            Debug.Log($"[GearDebugController] Salvaged '{first.ItemName}' → +{dust} Dust. Total: {_gear.SalvageDust}.");
        }

        private void ForgeNextRarity()
        {
            int count = System.Enum.GetValues(typeof(Rarity)).Length;
            var rarity = (Rarity)(_forgeRarityCursor % count);
            _forgeRarityCursor++;
            var instance = _gear.ForgeArtifact(rarity);
            if (instance != null)
                Debug.Log($"[GearDebugController] Forged '{instance.ItemName}'. Dust left: {_gear.SalvageDust}.");
            else
                Debug.Log($"[GearDebugController] Forge {rarity} failed (need more Dust than {_gear.SalvageDust}, or no config).");
        }

        private void ResolveOverflow()
        {
            var overflow = _gear.Overflow;
            if (overflow.Count == 0) { Debug.Log("[GearDebugController] No overflow pending."); return; }
            var first = overflow[0];
            if (_gear.ClaimOverflow(first.ItemId))
                Debug.Log($"[GearDebugController] Claimed overflow '{first.ItemName}' into inventory ({overflow.Count - 1} left).");
            else
            {
                int dust = _gear.Salvage(first.ItemId); // inventory full → salvage it instead
                Debug.Log($"[GearDebugController] Inventory full — salvaged overflow '{first.ItemName}' → +{dust} Dust.");
            }
        }

        private void GrantRandom()
        {
            var instance = BuildDebugInstance();
            if (instance != null)
            {
                _gear.Grant(instance);
                Debug.Log($"[GearDebugController] Spawned '{instance.ItemName}' (slot {instance.Slot}, " +
                          $"{instance.Affixes.Count} affix(es)). Saved.");
                return;
            }

            // Fallback: no bases wired → grant a legacy sample through the temporary drop bridge so G still works.
            if (_sampleItems.Count > 0)
            {
                var legacy = _sampleItems[Random.Range(0, _sampleItems.Count)];
                if (legacy != null) { _gear.Grant(legacy); Debug.Log($"[GearDebugController] Granted legacy '{legacy.ItemName}' via bridge."); }
                return;
            }

            Debug.LogWarning("[GearDebugController] No debug bases or sample items wired — nothing to spawn.");
        }

        // Minimal debug instance assembly (NOT the real generation): random base, random rarity (Common..Legendary;
        // Unique is hand-authored, never debug-rolled), and affix-count distinct affixes from the eligible pool at
        // their midpoint value.
        private GearInstance BuildDebugInstance()
        {
            if (_debugBases == null || _debugBases.Count == 0) return null;
            var baseTemplate = _debugBases[Random.Range(0, _debugBases.Count)];
            if (baseTemplate == null) return null;

            var rarity = (Rarity)Random.Range(0, (int)Rarity.Legendary + 1);

            var affixes = new List<RolledAffix>();
            if (_affixConfig != null)
            {
                int count = _affixConfig.AffixCountFor(rarity);
                var eligible = new List<AffixDefinitionSO>();
                var pool = _affixConfig.AffixPool;
                for (int i = 0; i < pool.Count; i++)
                    if (pool[i] != null && pool[i].IsEligibleFor(baseTemplate.Slot)) eligible.Add(pool[i]);

                for (int i = 0; i < count && eligible.Count > 0; i++)
                {
                    int idx = Random.Range(0, eligible.Count);
                    var def = eligible[idx];
                    eligible.RemoveAt(idx); // distinct affix types per item
                    affixes.Add(new RolledAffix(def, def.MidValue));
                }
            }

            return GearInstance.Create(baseTemplate, rarity, affixes);
        }

        private void EquipFirstOwned()
        {
            if (_activeHero == null) { Debug.LogWarning("[GearDebugController] No active hero yet (pick a hero first)."); return; }

            var first = _gear.Inventory.Count > 0 ? _gear.Inventory.Items[0] : null;
            if (first == null) { Debug.LogWarning("[GearDebugController] Inventory empty — press G to spawn first."); return; }

            if (_gear.Equip(_activeHero, first))
            {
                Debug.Log($"[GearDebugController] Equipped '{first.ItemName}' on '{_activeHero.HeroName}' slot {first.Slot}. Saved.");
                LogState();
            }
        }

        private void UnequipAll()
        {
            if (_activeHero == null) { Debug.LogWarning("[GearDebugController] No active hero yet."); return; }
            foreach (GearSlot slot in System.Enum.GetValues(typeof(GearSlot)))
            {
                _gear.Unequip(_activeHero, slot);
            }
            Debug.Log($"[GearDebugController] Unequipped all slots on '{_activeHero.HeroName}'. Saved.");
            LogState();
        }

        private void LogState()
        {
            Debug.Log($"[GearDebugController] Inventory: {_gear.Inventory.Count}/{_gear.Capacity} owned. " +
                      $"Overflow: {_gear.Overflow.Count}. Dust: {_gear.SalvageDust}.");

            if (_activeHero == null) return;

            var loadout = _gear.GetLoadout(_activeHero);
            foreach (GearSlot slot in System.Enum.GetValues(typeof(GearSlot)))
            {
                var item = loadout.GetEquipped(slot);
                Debug.Log($"  [{slot}] = {(item != null ? item.ItemName : "(empty)")}");
            }

            // Prove the equipped modifiers flow through AbilityRuntime's existing pipeline.
            var hero = Object.FindFirstObjectByType<HeroRuntime>();
            if (hero != null && hero.Basic is AbilityRuntime ability)
            {
                float dmg = ability.GetEffectiveDamage(
                    _bootstrap.Session.UpgradeInventory,
                    _bootstrap.Session.ConsumableInventory,
                    loadout.AggregatedModifiers);
                Debug.Log($"  Basic '{ability.Definition.AbilityName}' gear-inclusive effective damage: {dmg:0.#}");
            }
        }
    }
}
