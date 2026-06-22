using TMPro;
using UnityEngine;
using Wavekeep.Core;
using Wavekeep.Core.Events;

namespace Wavekeep.UI
{
    /// <summary>
    /// Throwaway placeholder drop notification (Task 13, §4) — there is no hub/inventory UI yet (Task 14),
    /// so this just confirms WHEN and WHAT dropped. Subscribes to <see cref="GearDroppedEvent"/> and
    /// flashes a brief line like "Dropped: [Rare] Swift Gauntlets", auto-hiding after a few seconds.
    /// Owns no gear state — purely a view (CLAUDE.md §3.3). Consistent with the Task 03/06 placeholder HUDs.
    /// </summary>
    [AddComponentMenu("Wavekeep/UI/Loot Drop HUD")]
    public sealed class LootDropHud : MonoBehaviour
    {
        [SerializeField] private GameSessionBootstrap _bootstrap;
        [SerializeField] private TMP_Text _text;
        [Tooltip("Seconds the drop line stays visible before fading out.")]
        [SerializeField, Min(0.5f)] private float _visibleSeconds = 3f;

        private EventBus _events;
        private float _hideTimer;

        private void Start()
        {
            if (_bootstrap == null || _bootstrap.Session == null)
            {
                Debug.LogWarning("[LootDropHud] No GameSessionBootstrap/Session assigned; disabling.", this);
                enabled = false;
                return;
            }

            _events = _bootstrap.Session.Events;
            _events.Subscribe<GearDroppedEvent>(OnGearDropped);

            if (_text != null) _text.text = "";
        }

        private void OnDestroy()
        {
            if (_events != null) _events.Unsubscribe<GearDroppedEvent>(OnGearDropped);
        }

        private void OnGearDropped(GearDroppedEvent evt)
        {
            if (_text == null || evt.Item == null) return;
            _text.text = $"Dropped: [{evt.Item.Rarity}] {evt.Item.ItemName}";
            _hideTimer = _visibleSeconds; // a fresh drop resets the timer (latest wins)
        }

        private void Update()
        {
            if (_hideTimer <= 0f) return;
            _hideTimer -= Time.deltaTime;
            if (_hideTimer <= 0f && _text != null) _text.text = "";
        }
    }
}
