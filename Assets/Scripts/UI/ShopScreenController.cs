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
    /// Between-wave shop screen (Task 06 §4, extended in Task 09). Opens on
    /// <see cref="IntermissionStartedEvent"/>, renders a per-visit OFFER of items (a fixed-size random
    /// subset of the pool, drawn by <see cref="ShopController"/>), shows the live currency total and the
    /// run's reroll-point count, and offers Reroll + Continue actions.
    ///
    /// It owns a plain-C# <see cref="ShopController"/> (built from <see cref="GameSession"/> services +
    /// the scene wall) for all logic and only renders. Card slots are pre-built once (count =
    /// <see cref="_offerSize"/>) and repopulated from <see cref="ShopController.CurrentOffer"/> on open
    /// and after each reroll — mirroring <see cref="LevelUpCardPicker"/>.
    ///
    /// Resource independence (Task 09): rerolling spends reroll points only (never currency); a Reroll
    /// Potion purchase goes through the normal <see cref="ShopController.TryPurchase"/> path and adds
    /// reroll points via the effect. The UI refreshes off <see cref="CurrencyChangedEvent"/> and
    /// <see cref="RerollPointsChangedEvent"/> so the two resources update independently.
    /// </summary>
    [AddComponentMenu("Wavekeep/UI/Shop Screen Controller")]
    public sealed class ShopScreenController : MonoBehaviour
    {
        [Header("Dependencies")]
        [SerializeField] private GameSessionBootstrap _bootstrap;
        [SerializeField] private WaveSpawner _waveSpawner;
        [Tooltip("Defended wall — target of HealWall consumables (routed through WallRuntime.Heal).")]
        [SerializeField] private WallRuntime _wall;

        [Header("Inventory (the full pool the per-visit offer is drawn from)")]
        [SerializeField] private List<ConsumableDefinitionSO> _availableConsumables = new List<ConsumableDefinitionSO>();
        [Tooltip("How many items the shop offers per visit (Task 06 count; reroll re-draws this many).")]
        [SerializeField, Min(1)] private int _offerSize = 4;

        [Header("UI")]
        [Tooltip("Root object shown during the between-wave intermission, hidden otherwise.")]
        [SerializeField] private GameObject _shopPanel;
        [Tooltip("Parent (with a vertical layout) under which item rows are generated.")]
        [SerializeField] private RectTransform _itemContainer;
        [SerializeField] private TMP_Text _currencyText;
        [SerializeField] private Button _continueButton;

        [Header("Reroll (Task 09)")]
        [SerializeField] private Button _rerollButton;
        [Tooltip("Label on the reroll button; shows the current reroll-point count.")]
        [SerializeField] private TMP_Text _rerollCountLabel;

        private sealed class ItemRow
        {
            public GameObject Root;
            public ConsumableDefinitionSO Item;
            public TMP_Text InfoText;
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
            _shop = new ShopController(
                _currency, session.ConsumableInventory, _wall, session.RerollManager,
                _availableConsumables, _offerSize);

            BuildSlots();

            if (_continueButton != null) _continueButton.onClick.AddListener(OnContinue);
            if (_rerollButton != null) _rerollButton.onClick.AddListener(OnReroll);

            _events.Subscribe<IntermissionStartedEvent>(OnIntermissionStarted);
            _events.Subscribe<CurrencyChangedEvent>(OnCurrencyChanged);
            _events.Subscribe<RerollPointsChangedEvent>(OnRerollPointsChanged);

            SetPanelVisible(false);
        }

        private void OnDestroy()
        {
            if (_events == null) return;
            _events.Unsubscribe<IntermissionStartedEvent>(OnIntermissionStarted);
            _events.Unsubscribe<CurrencyChangedEvent>(OnCurrencyChanged);
            _events.Unsubscribe<RerollPointsChangedEvent>(OnRerollPointsChanged);
        }

        private void BuildSlots()
        {
            if (_itemContainer == null) return;

            // Pre-build offerSize empty slots; offers are smaller only if the pool is smaller.
            int slots = Mathf.Max(1, _offerSize);
            for (int i = 0; i < slots; i++)
            {
                _rows.Add(CreateSlot());
            }
        }

        private ItemRow CreateSlot()
        {
            // Row container with a horizontal layout: [ info label ][ buy button ].
            var rowGo = new GameObject("Row", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            rowGo.transform.SetParent(_itemContainer, false);
            ((RectTransform)rowGo.transform).sizeDelta = new Vector2(560f, 64f);
            var hlg = rowGo.GetComponent<HorizontalLayoutGroup>();
            hlg.spacing = 12f;
            hlg.childAlignment = TextAnchor.MiddleLeft;
            hlg.childControlWidth = false;
            hlg.childControlHeight = false;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = false;

            var infoGo = new GameObject("Info", typeof(RectTransform));
            infoGo.transform.SetParent(rowGo.transform, false);
            var info = infoGo.AddComponent<TextMeshProUGUI>();
            info.fontSize = 20f;
            info.color = Color.white;
            info.alignment = TextAlignmentOptions.Left;
            if (TMP_Settings.defaultFontAsset != null) info.font = TMP_Settings.defaultFontAsset;
            ((RectTransform)info.transform).sizeDelta = new Vector2(420f, 64f);

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

            return new ItemRow { Root = rowGo, InfoText = info, BuyButton = button, BuyLabel = buyLabel };
        }

        // Bind the current offer into the pre-built slots; hide any spare slots (pool < offerSize).
        private void ShowOffer()
        {
            var offer = _shop.CurrentOffer;
            for (int i = 0; i < _rows.Count; i++)
            {
                var row = _rows[i];
                if (i < offer.Count && offer[i] != null)
                {
                    row.Item = offer[i];
                    row.InfoText.text = BuildItemInfo(row.Item);
                    row.BuyButton.onClick.RemoveAllListeners();
                    var captured = row.Item; // capture per-slot so the handler stays data-driven
                    row.BuyButton.onClick.AddListener(() => OnBuy(captured));
                    row.Root.SetActive(true);
                }
                else
                {
                    row.Item = null;
                    row.Root.SetActive(false);
                }
            }
        }

        private static string BuildItemInfo(ConsumableDefinitionSO item)
        {
            string tier = TierLabel(item.Tier);
            string desc = string.IsNullOrEmpty(item.Description) ? "" : $"\n<size=70%>{item.Description}</size>";
            return $"[{tier}] {item.DisplayName}  —  {item.Price}g{desc}";
        }

        private static string TierLabel(ConsumableTier tier)
        {
            switch (tier)
            {
                case ConsumableTier.Tier2: return "T2";
                case ConsumableTier.Tier3: return "T3";
                default: return "T1";
            }
        }

        private void OnBuy(ConsumableDefinitionSO item)
        {
            // ShopController validates funds (via CurrencyManager.TrySpend) and stackable rules; we only
            // refresh. A successful spend fires CurrencyChangedEvent; a Reroll Potion also fires
            // RerollPointsChangedEvent — both routed through the normal TryPurchase effect path.
            if (!_shop.TryPurchase(item))
            {
                Debug.Log($"[ShopScreenController] Purchase of '{item.DisplayName}' rejected (funds or stackable rule).");
            }
            RefreshRows();
        }

        private void OnReroll()
        {
            // Spends a reroll point only (never currency) and re-draws the offer. No-op at 0 points.
            if (_shop.TryReroll())
            {
                ShowOffer();
                RefreshRows();
            }
            RefreshReroll();
        }

        private void OnContinue()
        {
            SetPanelVisible(false);
            if (_waveSpawner != null) _waveSpawner.ContinueAfterIntermission();
        }

        private void OnIntermissionStarted(IntermissionStartedEvent evt)
        {
            // A fresh offer each visit is free — it does NOT touch the reroll pool (Task 09 core rule).
            _shop.GenerateOffer();
            ShowOffer();
            SetPanelVisible(true);
            RefreshCurrency();
            RefreshRows();
            RefreshReroll();
        }

        private void OnCurrencyChanged(CurrencyChangedEvent evt)
        {
            RefreshCurrency();
            RefreshRows();
        }

        private void OnRerollPointsChanged(RerollPointsChangedEvent evt) => RefreshReroll();

        private void RefreshCurrency()
        {
            if (_currencyText != null) _currencyText.text = $"Currency: {_currency.CurrentCurrency}";
        }

        private void RefreshReroll()
        {
            if (_rerollCountLabel != null) _rerollCountLabel.text = $"Reroll ({_shop.RerollPoints})";
            if (_rerollButton != null) _rerollButton.interactable = _shop.CanReroll;
        }

        private void RefreshRows()
        {
            var inventory = _bootstrap.Session.ConsumableInventory;
            for (int i = 0; i < _rows.Count; i++)
            {
                var row = _rows[i];
                if (row.Item == null || !row.Root.activeSelf) continue;

                row.BuyButton.interactable = _shop.CanPurchase(row.Item);

                // Distinguish "already owned" (non-stackable) from merely "can't afford".
                bool ownedNonStackable = !row.Item.Stackable && inventory.Owns(row.Item);
                row.BuyLabel.text = ownedNonStackable ? "Owned" : "Buy";
            }
        }

        private void SetPanelVisible(bool visible)
        {
            if (_shopPanel != null) _shopPanel.SetActive(visible);
        }
    }
}
