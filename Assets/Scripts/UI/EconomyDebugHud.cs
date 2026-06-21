using TMPro;
using UnityEngine;
using Wavekeep.Core;
using Wavekeep.Core.Events;
using Wavekeep.Economy;

namespace Wavekeep.UI
{
    /// <summary>
    /// TEMPORARY Task 03 verification display (NOT the final HUD). Shows the live currency total and
    /// level/XP progress so the reward pipeline is provable end-to-end. Subscribes to the session
    /// <see cref="EventBus"/> and re-reads <see cref="CurrencyManager"/>/<see cref="XPManager"/>
    /// getters; it owns no economy state itself.
    ///
    /// Refreshes on <see cref="EnemyKilledEvent"/> (so XP progress updates every kill, not only on
    /// level-ups), plus <see cref="CurrencyChangedEvent"/> and <see cref="XPLevelUpEvent"/>. Managers
    /// subscribe in Awake and this subscribes in Start, so they have already applied a kill's reward
    /// by the time this reads their state.
    /// </summary>
    [AddComponentMenu("Wavekeep/Debug/Economy Debug HUD")]
    public sealed class EconomyDebugHud : MonoBehaviour
    {
        [SerializeField] private GameSessionBootstrap _bootstrap;
        [SerializeField] private TMP_Text _currencyText;
        [SerializeField] private TMP_Text _levelXpText;

        private EventBus _events;
        private CurrencyManager _currency;
        private XPManager _xp;

        private void Start()
        {
            if (_bootstrap == null || _bootstrap.Session == null)
            {
                Debug.LogWarning("[EconomyDebugHud] No GameSessionBootstrap/Session assigned; skipping.", this);
                return;
            }

            _events = _bootstrap.Session.Events;
            _currency = _bootstrap.Session.CurrencyManager;
            _xp = _bootstrap.Session.XPManager;

            _events.Subscribe<EnemyKilledEvent>(OnEnemyKilled);
            _events.Subscribe<CurrencyChangedEvent>(OnCurrencyChanged);
            _events.Subscribe<XPLevelUpEvent>(OnLevelUp);

            Refresh();
        }

        private void OnDestroy()
        {
            if (_events == null) return;
            _events.Unsubscribe<EnemyKilledEvent>(OnEnemyKilled);
            _events.Unsubscribe<CurrencyChangedEvent>(OnCurrencyChanged);
            _events.Unsubscribe<XPLevelUpEvent>(OnLevelUp);
        }

        private void OnEnemyKilled(EnemyKilledEvent evt) => Refresh();
        private void OnCurrencyChanged(CurrencyChangedEvent evt) => Refresh();
        private void OnLevelUp(XPLevelUpEvent evt) => Refresh();

        private void Refresh()
        {
            if (_currencyText != null)
            {
                _currencyText.text = $"Currency: {_currency.CurrentCurrency}";
            }

            if (_levelXpText != null)
            {
                _levelXpText.text = $"Lv. {_xp.CurrentLevel} — {_xp.CurrentXP}/{_xp.XPToNextLevel} XP";
            }
        }
    }
}
