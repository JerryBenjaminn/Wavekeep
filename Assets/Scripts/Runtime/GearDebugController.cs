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
    /// Debug stand-in for the real loot/hub flow (Task 13/14), mirroring how Task 04 granted upgrades by
    /// key before the card picker existed. Provides keyboard triggers to grant sample gear, equip/unequip
    /// it on the active hero, and inspect state — enough to verify the full data/equip/persistence/stat
    /// pipeline. Drives nothing in normal play; gated behind <see cref="_enableDebugKeys"/>.
    ///
    /// Keys: G = grant a random sample item; J = equip the first owned item on the active hero; L =
    /// unequip everything back to inventory; I = log inventory + loadout + the basic ability's
    /// gear-inclusive effective damage (proving stats flow through the AbilityRuntime pipeline).
    /// </summary>
    [AddComponentMenu("Wavekeep/Debug/Gear Debug Controller")]
    public sealed class GearDebugController : MonoBehaviour
    {
        [SerializeField] private bool _enableDebugKeys = true;
        [SerializeField] private GameSessionBootstrap _bootstrap;
        [Tooltip("Sample gear/artifact items the G key grants from (authored by the Task 12 setup).")]
        [SerializeField] private List<LootItemSO> _sampleItems = new List<LootItemSO>();

        private EventBus _events;
        private GearManager _gear;
        private HeroDefinitionSO _activeHero;

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
            if (keyboard[Key.I].wasPressedThisFrame) LogState();
        }

        private void GrantRandom()
        {
            if (_sampleItems.Count == 0) { Debug.LogWarning("[GearDebugController] No sample items wired."); return; }
            var item = _sampleItems[Random.Range(0, _sampleItems.Count)];
            if (item == null) return;
            _gear.Grant(item);
            Debug.Log($"[GearDebugController] Granted '{item.ItemName}' ({item.Rarity}, slot {item.Slot}). Saved.");
        }

        private void EquipFirstOwned()
        {
            if (_activeHero == null) { Debug.LogWarning("[GearDebugController] No active hero yet (pick a hero first)."); return; }

            LootItemSO first = null;
            foreach (var pair in _gear.Inventory.Owned) { first = pair.Key; break; }
            if (first == null) { Debug.LogWarning("[GearDebugController] Inventory empty — press G to grant first."); return; }

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
            int distinct = 0;
            foreach (var pair in _gear.Inventory.Owned) distinct += pair.Value;
            Debug.Log($"[GearDebugController] Inventory: {distinct} owned item(s).");

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
