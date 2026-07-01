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
    /// Boss-reward utility shop screen (Task 80 redesign). Opens on <see cref="IntermissionStartedEvent"/> — which
    /// the WaveSpawner now fires ONLY after a boss wave is cleared — renders an OFFER of 3–4 utility items, and lets
    /// the player pick exactly ONE for FREE (no Currency, no reroll, no skip). Picking closes the shop and releases
    /// the wave gate via <see cref="WaveSpawner.ContinueAfterIntermission"/>.
    ///
    /// Owns a plain-C# <see cref="ShopController"/> (built from the scene wall + wave spawner + session services)
    /// for all logic and only renders. Currency infrastructure is untouched and may still be shown, but is never
    /// spent here (Task 80: Currency has no shop sink).
    /// </summary>
    [AddComponentMenu("Wavekeep/UI/Shop Screen Controller")]
    public sealed class ShopScreenController : MonoBehaviour
    {
        [Header("Dependencies")]
        [SerializeField] private GameSessionBootstrap _bootstrap;
        [SerializeField] private WaveSpawner _waveSpawner;
        [Tooltip("Defended wall — target of wall-utility rewards (Repair/Barricade/Aegis) via WallRuntime.")]
        [SerializeField] private WallRuntime _wall;

        [Header("Inventory (the full utility pool the boss-reward offer is drawn from)")]
        [SerializeField] private List<ConsumableDefinitionSO> _availableConsumables = new List<ConsumableDefinitionSO>();
        [Tooltip("How many utility items the boss reward offers (3–4). The player picks exactly one.")]
        [SerializeField, Min(1)] private int _offerSize = 4;

        [Tooltip("Task 24: Luck/wave tier-weighting for the offer draw. If unset, offers are uniform.")]
        [SerializeField] private TierWeightingConfigSO _tierWeightingConfig;

        [Header("UI")]
        [Tooltip("Root object shown during the boss-reward intermission, hidden otherwise.")]
        [SerializeField] private GameObject _shopPanel;
        [Tooltip("Parent (with a vertical layout) under which item rows are generated.")]
        [SerializeField] private RectTransform _itemContainer;
        [Tooltip("Optional header/currency label. Currency is retained but not spent here (Task 80).")]
        [SerializeField] private TMP_Text _currencyText;
        [Tooltip("Retained from the old shop but unused (no skip without a pick). Hidden on Start.")]
        [SerializeField] private Button _continueButton;

        [Header("Deprecated (Task 80 — reroll removed; hidden on Start)")]
        [SerializeField] private Button _rerollButton;
        [SerializeField] private TMP_Text _rerollCountLabel;

        private sealed class ItemRow
        {
            public GameObject Root;
            public ConsumableDefinitionSO Item;
            public TMP_Text InfoText;
            public Button PickButton;
            public TMP_Text PickLabel;
        }

        private EventBus _events;
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
            _shop = new ShopController(
                _wall, _waveSpawner, _events,
                _availableConsumables, _offerSize,
                session.LuckState, _tierWeightingConfig);

            BuildSlots();

            // Task 80: no reroll, no skip-continue — picking one item is the only way out of the shop.
            if (_rerollButton != null) _rerollButton.gameObject.SetActive(false);
            if (_rerollCountLabel != null) _rerollCountLabel.gameObject.SetActive(false);
            if (_continueButton != null) _continueButton.gameObject.SetActive(false);

            _events.Subscribe<IntermissionStartedEvent>(OnIntermissionStarted);

            SetPanelVisible(false);
        }

        private void OnDestroy()
        {
            _shop?.Dispose();
            if (_events == null) return;
            _events.Unsubscribe<IntermissionStartedEvent>(OnIntermissionStarted);
        }

        private void BuildSlots()
        {
            if (_itemContainer == null) return;
            int slots = Mathf.Max(1, _offerSize);
            for (int i = 0; i < slots; i++) _rows.Add(CreateSlot());
        }

        private ItemRow CreateSlot()
        {
            var rowGo = new GameObject("Row", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            rowGo.transform.SetParent(_itemContainer, false);
            ((RectTransform)rowGo.transform).sizeDelta = new Vector2(600f, 72f);
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
            ((RectTransform)info.transform).sizeDelta = new Vector2(440f, 72f);

            var buttonGo = new GameObject("Pick", typeof(RectTransform), typeof(Image), typeof(Button));
            buttonGo.transform.SetParent(rowGo.transform, false);
            ((RectTransform)buttonGo.transform).sizeDelta = new Vector2(120f, 52f);
            var button = buttonGo.GetComponent<Button>();

            var pickLabelGo = new GameObject("Label", typeof(RectTransform));
            pickLabelGo.transform.SetParent(buttonGo.transform, false);
            var pickLabel = pickLabelGo.AddComponent<TextMeshProUGUI>();
            pickLabel.text = "Pick";
            pickLabel.fontSize = 22f;
            pickLabel.color = Color.black;
            pickLabel.alignment = TextAlignmentOptions.Center;
            if (TMP_Settings.defaultFontAsset != null) pickLabel.font = TMP_Settings.defaultFontAsset;
            var plRt = pickLabel.rectTransform;
            plRt.anchorMin = Vector2.zero;
            plRt.anchorMax = Vector2.one;
            plRt.offsetMin = Vector2.zero;
            plRt.offsetMax = Vector2.zero;

            return new ItemRow { Root = rowGo, InfoText = info, PickButton = button, PickLabel = pickLabel };
        }

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
                    row.PickButton.onClick.RemoveAllListeners();
                    var captured = row.Item;
                    row.PickButton.onClick.AddListener(() => OnPick(captured));
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
            string desc = string.IsNullOrEmpty(item.Description) ? "" : $"\n<size=70%>{item.Description}</size>";
            return $"{item.DisplayName}{desc}";
        }

        private void OnPick(ConsumableDefinitionSO item)
        {
            // Single free pick: apply the utility effect, then close the shop and let the run continue. Picking
            // is the only exit — there is no skip and no second pick.
            if (_shop.Pick(item))
            {
                SetPanelVisible(false);
                if (_waveSpawner != null) _waveSpawner.ContinueAfterIntermission();
            }
        }

        private void OnIntermissionStarted(IntermissionStartedEvent evt)
        {
            _shop.GenerateOffer();
            ShowOffer();
            RefreshRows();
            RefreshCurrency();
            SetPanelVisible(true);
        }

        private void RefreshCurrency()
        {
            if (_currencyText != null && _bootstrap.Session.CurrencyManager != null)
                _currencyText.text = $"Choose a reward  (Currency: {_bootstrap.Session.CurrencyManager.CurrentCurrency})";
        }

        private void RefreshRows()
        {
            for (int i = 0; i < _rows.Count; i++)
            {
                var row = _rows[i];
                if (row.Item == null || !row.Root.activeSelf) continue;
                row.PickButton.interactable = _shop.CanPick(row.Item);
                row.PickLabel.text = "Pick";
            }
        }

        private void SetPanelVisible(bool visible)
        {
            if (_shopPanel != null) _shopPanel.SetActive(visible);
        }
    }
}
