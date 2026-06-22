using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Wavekeep.Core;
using Wavekeep.Core.Events;
using Wavekeep.Data;
using Wavekeep.Economy;
using Wavekeep.Runtime;
using Wavekeep.Waves;

namespace Wavekeep.UI
{
    /// <summary>
    /// Between-wave shop screen (Task 06 §4). Opens on <see cref="IntermissionStartedEvent"/> — the
    /// general pause hook the <see cref="WaveSpawner"/> raises between waves — lists the run's fixed
    /// consumable assortment with name/price/description and a buy button each, shows the live currency
    /// total, and a Continue button that closes the shop and resumes spawning via
    /// <see cref="WaveSpawner.ContinueAfterIntermission"/>.
    ///
    /// It owns a plain-C# <see cref="ShopController"/> (built from <see cref="GameSession"/> services +
    /// the scene wall) for purchase logic, and only renders — no economy or wave state lives here. Item
    /// rows are generated at runtime from <see cref="_availableConsumables"/> (a loop, no per-item code),
    /// mirroring <see cref="HeroSelectController"/>, so a new shop item is purely a new SO in the list.
    ///
    /// Affordability/ownership UI refreshes off <see cref="CurrencyChangedEvent"/> (fired by every
    /// successful spend) so no dedicated purchase event is needed.
    /// </summary>
    [AddComponentMenu("Wavekeep/UI/Shop Screen Controller")]
    public sealed class ShopScreenController : MonoBehaviour
    {
        [Header("Dependencies")]
        [SerializeField] private GameSessionBootstrap _bootstrap;
        [SerializeField] private WaveSpawner _waveSpawner;
        [Tooltip("Defended wall — target of HealWall consumables (routed through WallRuntime.Heal).")]
        [SerializeField] private WallRuntime _wall;

        [Header("Inventory (fixed per-run assortment — add a ConsumableDefinitionSO to stock it)")]
        [SerializeField] private List<ConsumableDefinitionSO> _availableConsumables = new List<ConsumableDefinitionSO>();

        [Header("UI")]
        [Tooltip("Root object shown during the between-wave intermission, hidden otherwise.")]
        [SerializeField] private GameObject _shopPanel;
        [Tooltip("Parent (with a vertical layout) under which item rows are generated.")]
        [SerializeField] private RectTransform _itemContainer;
        [SerializeField] private TMP_Text _currencyText;
        [SerializeField] private Button _continueButton;

        private sealed class ItemRow
        {
            public ConsumableDefinitionSO Item;
            public Button BuyButton;
            public TMP_Text BuyLabel;
        }

        private EventBus _events;
        private CurrencyManager _currency;
        private ShopController _shop;
        private readonly List<ItemRow> _rows = new List<ItemRow>();

        private void Start()
        {
            if (_bootstrap == null || _bootstrap.Session == null)
            {
                Debug.LogError("[ShopScreenController] No GameSessionBootstrap/Session assigned; disabling.", this);
                enabled = false;
                return;
            }

            var session = _bootstrap.Session;
            _events = session.Events;
            _currency = session.CurrencyManager;
            _shop = new ShopController(_currency, session.ConsumableInventory, _wall);

            BuildRows();

            if (_continueButton != null)
            {
                _continueButton.onClick.AddListener(OnContinue);
            }

            _events.Subscribe<IntermissionStartedEvent>(OnIntermissionStarted);
            _events.Subscribe<CurrencyChangedEvent>(OnCurrencyChanged);

            SetPanelVisible(false);
        }

        private void OnDestroy()
        {
            if (_events == null) return;
            _events.Unsubscribe<IntermissionStartedEvent>(OnIntermissionStarted);
            _events.Unsubscribe<CurrencyChangedEvent>(OnCurrencyChanged);
        }

        private void BuildRows()
        {
            if (_itemContainer == null) return;

            for (int i = 0; i < _availableConsumables.Count; i++)
            {
                var item = _availableConsumables[i];
                if (item == null) continue;
                _rows.Add(CreateRow(item));
            }
        }

        private ItemRow CreateRow(ConsumableDefinitionSO item)
        {
            // Row container with a horizontal layout: [ info label ][ buy button ].
            var rowGo = new GameObject($"Row_{item.DisplayName}", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            rowGo.transform.SetParent(_itemContainer, false);
            var rowRt = (RectTransform)rowGo.transform;
            rowRt.sizeDelta = new Vector2(560f, 64f);
            var hlg = rowGo.GetComponent<HorizontalLayoutGroup>();
            hlg.spacing = 12f;
            hlg.childAlignment = TextAnchor.MiddleLeft;
            hlg.childControlWidth = false;
            hlg.childControlHeight = false;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = false;

            // Info label: name, price, and description.
            var infoGo = new GameObject("Info", typeof(RectTransform));
            infoGo.transform.SetParent(rowGo.transform, false);
            var info = infoGo.AddComponent<TextMeshProUGUI>();
            info.text = BuildItemInfo(item);
            info.fontSize = 20f;
            info.color = Color.white;
            info.alignment = TextAlignmentOptions.Left;
            if (TMP_Settings.defaultFontAsset != null) info.font = TMP_Settings.defaultFontAsset;
            ((RectTransform)info.transform).sizeDelta = new Vector2(420f, 64f);

            // Buy button.
            var buttonGo = new GameObject("Buy", typeof(RectTransform), typeof(Image), typeof(Button));
            buttonGo.transform.SetParent(rowGo.transform, false);
            ((RectTransform)buttonGo.transform).sizeDelta = new Vector2(120f, 48f);
            var button = buttonGo.GetComponent<Button>();

            var buyLabelGo = new GameObject("Label", typeof(RectTransform));
            buyLabelGo.transform.SetParent(buttonGo.transform, false);
            var buyLabel = buyLabelGo.AddComponent<TextMeshProUGUI>();
            buyLabel.text = "Buy";
            buyLabel.fontSize = 22f;
            buyLabel.color = Color.black;
            buyLabel.alignment = TextAlignmentOptions.Center;
            if (TMP_Settings.defaultFontAsset != null) buyLabel.font = TMP_Settings.defaultFontAsset;
            var blRt = buyLabel.rectTransform;
            blRt.anchorMin = Vector2.zero;
            blRt.anchorMax = Vector2.one;
            blRt.offsetMin = Vector2.zero;
            blRt.offsetMax = Vector2.zero;

            // Capture the item in the closure so the handler stays data-driven, not per-item.
            button.onClick.AddListener(() => OnBuy(item));

            return new ItemRow { Item = item, BuyButton = button, BuyLabel = buyLabel };
        }

        private static string BuildItemInfo(ConsumableDefinitionSO item)
        {
            string desc = string.IsNullOrEmpty(item.Description) ? "" : $"\n<size=70%>{item.Description}</size>";
            return $"{item.DisplayName}  —  {item.Price}g{desc}";
        }

        private void OnBuy(ConsumableDefinitionSO item)
        {
            // ShopController validates funds (via CurrencyManager.TrySpend) and stackable rules; we only
            // refresh the UI. A successful spend also fires CurrencyChangedEvent → OnCurrencyChanged.
            bool bought = _shop.TryPurchase(item);
            if (!bought)
            {
                Debug.Log($"[ShopScreenController] Purchase of '{item.DisplayName}' rejected (funds or stackable rule).");
            }
            RefreshRows();
        }

        private void OnContinue()
        {
            SetPanelVisible(false);
            if (_waveSpawner != null) _waveSpawner.ContinueAfterIntermission();
        }

        private void OnIntermissionStarted(IntermissionStartedEvent evt)
        {
            SetPanelVisible(true);
            RefreshCurrency();
            RefreshRows();
        }

        private void OnCurrencyChanged(CurrencyChangedEvent evt)
        {
            RefreshCurrency();
            RefreshRows();
        }

        private void RefreshCurrency()
        {
            if (_currencyText != null) _currencyText.text = $"Currency: {_currency.CurrentCurrency}";
        }

        private void RefreshRows()
        {
            for (int i = 0; i < _rows.Count; i++)
            {
                var row = _rows[i];
                bool canBuy = _shop.CanPurchase(row.Item);
                row.BuyButton.interactable = canBuy;

                // Distinguish "already owned" (non-stackable) from merely "can't afford".
                bool ownedNonStackable = !row.Item.Stackable && _bootstrap.Session.ConsumableInventory.Owns(row.Item);
                row.BuyLabel.text = ownedNonStackable ? "Owned" : "Buy";
            }
        }

        private void SetPanelVisible(bool visible)
        {
            if (_shopPanel != null) _shopPanel.SetActive(visible);
        }
    }
}
