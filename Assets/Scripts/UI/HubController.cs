using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Wavekeep.Core;
using Wavekeep.Data;
using Wavekeep.Gear;

namespace Wavekeep.UI
{
    /// <summary>
    /// The Hub/main-menu screen (Task 14): browse the persistent <see cref="GearInventory"/>, pick a
    /// hero, view/edit that hero's <see cref="HeroLoadout"/> across all six slots, and launch a run.
    /// Placeholder-tier UI built at runtime (like the other Wavekeep controllers).
    ///
    /// CRITICAL: every equip/unequip routes through Task 12's EXISTING <see cref="GearManager"/> API
    /// (<see cref="GearManager.Equip"/>/<see cref="GearManager.Unequip"/>), which preserve
    /// replace-not-destroy and persist to disk — no parallel mutation path. The Hub reads inventory via
    /// <see cref="GearManager.Inventory"/> and loadouts via <see cref="GearManager.GetLoadout(HeroDefinitionSO)"/>.
    ///
    /// Flow (documented decision): the Hub ABSORBS hero selection. "Start Run" stores the chosen hero on
    /// a <see cref="RunLaunchContext"/> and loads the gameplay scene, which auto-starts that hero — the
    /// player never picks twice. The chosen loadout reaches gameplay via the existing disk save.
    /// </summary>
    [AddComponentMenu("Wavekeep/UI/Hub Controller")]
    public sealed class HubController : MonoBehaviour
    {
        [Header("Dependencies")]
        [SerializeField] private GameSessionBootstrap _bootstrap;
        [SerializeField] private List<HeroDefinitionSO> _heroRoster = new List<HeroDefinitionSO>();
        [SerializeField] private string _gameplaySceneName = "SampleScene";

        [Header("UI containers (filled at runtime)")]
        [SerializeField] private RectTransform _heroButtonContainer;
        [SerializeField] private TMP_Text _selectedHeroLabel;
        [SerializeField] private RectTransform _slotContainer;
        [SerializeField] private RectTransform _inventoryContainer;
        [SerializeField] private Button _startRunButton;

        [Header("Equip picker (modal)")]
        [SerializeField] private GameObject _equipPickerPanel;
        [SerializeField] private TMP_Text _equipPickerTitle;
        [SerializeField] private RectTransform _equipPickerContainer;
        [SerializeField] private Button _equipPickerCloseButton;

        private GearManager _gear;
        private HeroDefinitionSO _activeHero;

        private void Start()
        {
            if (_bootstrap == null || _bootstrap.Session == null)
            {
                Debug.LogError("[HubController] No GameSessionBootstrap/Session assigned; disabling.", this);
                enabled = false;
                return;
            }

            _gear = _bootstrap.Session.GearManager;

            BuildHeroButtons();
            if (_startRunButton != null) _startRunButton.onClick.AddListener(OnStartRun);
            if (_equipPickerCloseButton != null) _equipPickerCloseButton.onClick.AddListener(ClosePicker);
            ClosePicker();

            // Default to the first hero so the loadout view is populated on launch.
            for (int i = 0; i < _heroRoster.Count; i++)
            {
                if (_heroRoster[i] != null) { SelectHero(_heroRoster[i]); break; }
            }
            RefreshInventory();
        }

        // --- Hero selection -----------------------------------------------------------------------

        private void BuildHeroButtons()
        {
            if (_heroButtonContainer == null) return;
            for (int i = 0; i < _heroRoster.Count; i++)
            {
                var hero = _heroRoster[i];
                if (hero == null) continue;
                var captured = hero;
                CreateButton(_heroButtonContainer, hero.HeroName, hero.Tint, Color.black,
                    new Vector2(200f, 48f), () => SelectHero(captured));
            }
        }

        private void SelectHero(HeroDefinitionSO hero)
        {
            _activeHero = hero;
            if (_selectedHeroLabel != null) _selectedHeroLabel.text = $"Editing: {hero.HeroName}";
            ClosePicker();
            RefreshSlots();
        }

        // --- Loadout (per hero, 6 slots) ----------------------------------------------------------

        private void RefreshSlots()
        {
            if (_slotContainer == null || _activeHero == null) return;
            ClearChildren(_slotContainer);

            var loadout = _gear.GetLoadout(_activeHero);
            foreach (GearSlot slot in System.Enum.GetValues(typeof(GearSlot)))
            {
                var equipped = loadout.GetEquipped(slot);
                BuildSlotRow(slot, equipped);
            }
        }

        private void BuildSlotRow(GearSlot slot, LootItemSO equipped)
        {
            var row = CreateRow(_slotContainer, new Vector2(560f, 44f));

            string equippedText = equipped != null
                ? $"<color=#{RarityHex(equipped.Rarity)}>[{equipped.Rarity}]</color> {equipped.ItemName}"
                : "<color=#888888>(Empty)</color>";
            var label = CreateText(row.transform, $"{slot}: {equippedText}", 18f, TextAlignmentOptions.Left);
            ((RectTransform)label.transform).sizeDelta = new Vector2(360f, 40f);

            var capturedSlot = slot;
            CreateButton(row.transform, "Equip", new Color(0.3f, 0.5f, 0.8f), Color.white,
                new Vector2(80f, 36f), () => OpenPicker(capturedSlot));
            CreateButton(row.transform, "Unequip", new Color(0.5f, 0.3f, 0.3f), Color.white,
                new Vector2(90f, 36f), () => OnUnequip(capturedSlot));
        }

        private void OnUnequip(GearSlot slot)
        {
            // Task 12 path: returns the item to inventory + persists (replace-not-destroy preserved).
            _gear.Unequip(_activeHero, slot);
            RefreshSlots();
            RefreshInventory();
        }

        // --- Equip picker (filtered by slot) ------------------------------------------------------

        private void OpenPicker(GearSlot slot)
        {
            if (_equipPickerPanel == null || _equipPickerContainer == null) return;
            ClearChildren(_equipPickerContainer);

            if (_equipPickerTitle != null) _equipPickerTitle.text = $"Equip — {slot}";

            bool any = false;
            foreach (var pair in _gear.Inventory.Owned)
            {
                var item = pair.Key;
                if (item == null || item.Slot != slot) continue; // valid-for-slot filter
                any = true;

                var capturedItem = item;
                string label = $"<color=#{RarityHex(item.Rarity)}>[{item.Rarity}]</color> {item.ItemName} ×{pair.Value}";
                CreateButton(_equipPickerContainer, label, new Color(0.18f, 0.18f, 0.24f), Color.white,
                    new Vector2(420f, 40f), () => OnEquipFromPicker(slot, capturedItem));
            }

            if (!any)
            {
                CreateText(_equipPickerContainer, "(No items owned for this slot)", 18f, TextAlignmentOptions.Center);
            }

            _equipPickerPanel.SetActive(true);
        }

        private void OnEquipFromPicker(GearSlot slot, LootItemSO item)
        {
            // Task 12 path: consumes from inventory, returns any displaced item, persists. The item's own
            // Slot decides placement (== the slot we filtered by), so no extra validation is needed.
            if (_gear.Equip(_activeHero, item))
            {
                RefreshSlots();
                RefreshInventory();
            }
            ClosePicker();
        }

        private void ClosePicker()
        {
            if (_equipPickerPanel != null) _equipPickerPanel.SetActive(false);
        }

        // --- Inventory list -----------------------------------------------------------------------

        private void RefreshInventory()
        {
            if (_inventoryContainer == null) return;
            ClearChildren(_inventoryContainer);

            // Group/sort: by slot, then rarity (descending) — simple grouping per the task.
            var items = new List<KeyValuePair<LootItemSO, int>>(_gear.Inventory.Owned);
            items.Sort((a, b) =>
            {
                int bySlot = ((int)a.Key.Slot).CompareTo((int)b.Key.Slot);
                if (bySlot != 0) return bySlot;
                return ((int)b.Key.Rarity).CompareTo((int)a.Key.Rarity);
            });

            if (items.Count == 0)
            {
                CreateText(_inventoryContainer, "(Inventory empty — kill enemies / bosses to find gear)", 18f, TextAlignmentOptions.Left);
                return;
            }

            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i].Key;
                string text = $"<color=#{RarityHex(item.Rarity)}>[{item.Rarity}]</color> {item.ItemName} " +
                              $"<size=80%>({item.Slot})</size> ×{items[i].Value}";
                CreateText(_inventoryContainer, text, 18f, TextAlignmentOptions.Left);
            }
        }

        // --- Start run ----------------------------------------------------------------------------

        private void OnStartRun()
        {
            if (_activeHero == null)
            {
                Debug.LogWarning("[HubController] No hero selected; pick a hero before starting a run.");
                return;
            }

            // Carry the chosen hero across the scene load; the gameplay scene auto-starts it (no second pick).
            RunLaunchContext.GetOrCreate().SetHero(_activeHero);
            SceneManager.LoadScene(_gameplaySceneName);
        }

        // --- UI helpers ---------------------------------------------------------------------------

        private static string RarityHex(Rarity rarity)
        {
            switch (rarity)
            {
                case Rarity.Uncommon: return "4CAF50";  // green
                case Rarity.Rare: return "4F8DF7";       // blue
                case Rarity.Epic: return "A659E0";       // purple
                case Rarity.Legendary: return "F5A623";  // orange
                case Rarity.Unique: return "E0474C";     // red
                default: return "CCCCCC";                // Common — gray
            }
        }

        private GameObject CreateRow(Transform parent, Vector2 size)
        {
            var row = new GameObject("Row", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            row.transform.SetParent(parent, false);
            ((RectTransform)row.transform).sizeDelta = size;
            var hlg = row.GetComponent<HorizontalLayoutGroup>();
            hlg.spacing = 8f;
            hlg.childAlignment = TextAnchor.MiddleLeft;
            hlg.childControlWidth = false;
            hlg.childControlHeight = false;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = false;
            return row;
        }

        private TextMeshProUGUI CreateText(Transform parent, string text, float fontSize, TextAlignmentOptions alignment)
        {
            var go = new GameObject("Text", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.color = Color.white;
            tmp.alignment = alignment;
            tmp.richText = true;
            if (TMP_Settings.defaultFontAsset != null) tmp.font = TMP_Settings.defaultFontAsset;
            ((RectTransform)tmp.transform).sizeDelta = new Vector2(540f, fontSize + 8f);
            return tmp;
        }

        private Button CreateButton(Transform parent, string label, Color bg, Color fg, Vector2 size, UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject($"Button_{label}", typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            ((RectTransform)go.transform).sizeDelta = size;
            go.GetComponent<Image>().color = bg;

            var labelGo = new GameObject("Label", typeof(RectTransform));
            labelGo.transform.SetParent(go.transform, false);
            var tmp = labelGo.AddComponent<TextMeshProUGUI>();
            tmp.text = label;
            tmp.fontSize = 18f;
            tmp.color = fg;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.richText = true;
            if (TMP_Settings.defaultFontAsset != null) tmp.font = TMP_Settings.defaultFontAsset;
            var lrt = tmp.rectTransform;
            lrt.anchorMin = Vector2.zero;
            lrt.anchorMax = Vector2.one;
            lrt.offsetMin = Vector2.zero;
            lrt.offsetMax = Vector2.zero;

            var button = go.GetComponent<Button>();
            button.onClick.AddListener(onClick);
            return button;
        }

        private static void ClearChildren(Transform parent)
        {
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                Object.Destroy(parent.GetChild(i).gameObject);
            }
        }
    }
}
