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

        [Header("Gear detail panel (Task 25)")]
        [Tooltip("Root toggled on when an item is selected, off by default / on toggle-close.")]
        [SerializeField] private GameObject _detailPanel;
        [Tooltip("Icon slot — bound to the item's sprite reference (placeholder tint when no sprite yet).")]
        [SerializeField] private Image _detailIcon;
        [SerializeField] private TMP_Text _detailNameText;
        [SerializeField] private TMP_Text _detailRarityText;
        [Tooltip("Vertical container the per-stat rows (with hover tooltips) are generated into.")]
        [SerializeField] private RectTransform _detailStatContainer;
        [SerializeField] private Button _detailEquipButton;
        [SerializeField] private Button _detailUnequipButton;

        private GearManager _gear;
        private HeroDefinitionSO _activeHero;

        // Task 25: the item currently shown in the detail panel (null = panel closed). Clicking this same
        // item again toggles the panel closed; clicking a different item re-targets the panel.
        private LootItemSO _selectedItem;
        private TooltipPresenter _tooltip;

        // Task 26: how the current selection was opened. true = clicked an equipped slot (an already-equipped
        // item → NO comparison); false = clicked an inventory item (compare it against the slot's equipped item).
        private bool _selectedFromSlot;

        // Comparison/aggregation key for the non-StatModifier Luck stat (sits after all AbilityModifierType values).
        private static readonly int LuckStatKey = System.Enum.GetValues(typeof(AbilityModifierType)).Length;

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

            // Task 25: detail panel — one shared floating tooltip + the equip/unequip entry points. Panel
            // starts closed (no grid-based "first item" to auto-open per Task 14's list layout).
            _tooltip = CreateTooltipPresenter();
            if (_detailEquipButton != null) _detailEquipButton.onClick.AddListener(OnDetailEquip);
            if (_detailUnequipButton != null) _detailUnequipButton.onClick.AddListener(OnDetailUnequip);
            CloseDetail();

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
            // Equip availability AND the comparison baseline are per-hero, so re-render the open panel.
            if (_selectedItem != null) RefreshDetail();
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

            if (equipped != null)
            {
                // Task 25/26: clicking an equipped item opens its detail panel (Unequip available there).
                // fromSlot=true marks it as already-equipped, so no upgrade comparison is shown.
                var captured = equipped;
                var btn = CreateButton(row.transform, $"{slot}: {equippedText}", new Color(0.16f, 0.16f, 0.20f),
                    Color.white, new Vector2(360f, 40f), () => OnItemClicked(captured, true));
                var lbl = btn.GetComponentInChildren<TMP_Text>();
                if (lbl != null) lbl.alignment = TextAlignmentOptions.Left;
            }
            else
            {
                var label = CreateText(row.transform, $"{slot}: {equippedText}", 18f, TextAlignmentOptions.Left);
                ((RectTransform)label.transform).sizeDelta = new Vector2(360f, 40f);
            }

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
            if (_selectedItem != null) RefreshDetail(); // keep an open detail panel (+ comparison) in sync
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
                if (_selectedItem != null) RefreshDetail(); // keep an open detail panel (+ comparison) in sync
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
                // Task 25/26: clicking an inventory item opens its detail panel (Equip available there).
                // fromSlot=false → the panel compares it against the slot's currently equipped item.
                var captured = item;
                CreateClickableRow(_inventoryContainer, text, () => OnItemClicked(captured, false));
            }
        }

        // --- Gear detail panel (Task 25) ----------------------------------------------------------

        private void OnItemClicked(LootItemSO item, bool fromSlot)
        {
            if (item == null) return;
            // Toggle: clicking the already-selected item from the same source again closes the panel.
            // Clicking the same item from the OTHER source (e.g. its inventory copy vs its equipped slot)
            // re-targets the panel so the comparison view updates instead of closing.
            if (_selectedItem == item && _selectedFromSlot == fromSlot)
            {
                CloseDetail();
                return;
            }
            _selectedItem = item;
            _selectedFromSlot = fromSlot;
            ShowDetail();
        }

        private void ShowDetail()
        {
            if (_selectedItem == null || _detailPanel == null) return;
            _detailPanel.SetActive(true);

            if (_detailIcon != null)
            {
                // No real artwork yet — show the sprite if authored, else a visible placeholder tint.
                _detailIcon.sprite = _selectedItem.Icon;
                _detailIcon.color = _selectedItem.Icon != null ? Color.white : new Color(0.30f, 0.30f, 0.38f, 1f);
            }
            if (_detailNameText != null) _detailNameText.text = _selectedItem.ItemName;
            if (_detailRarityText != null)
            {
                _detailRarityText.text =
                    $"<color=#{RarityHex(_selectedItem.Rarity)}>[{_selectedItem.Rarity}]</color>  " +
                    $"<size=80%>{_selectedItem.Slot}</size>";
            }

            RefreshDetail();
        }

        // Re-render the parts that depend on live equip state (stat rows + comparison, button availability)
        // without re-setting the static header. Safe to call after any equip/unequip or hero switch.
        private void RefreshDetail()
        {
            if (_selectedItem == null) return;
            BuildStatRows();
            RefreshDetailButtons();
        }

        private void CloseDetail()
        {
            _selectedItem = null;
            if (_detailPanel != null) _detailPanel.SetActive(false);
            if (_tooltip != null) _tooltip.Hide();
        }

        // Reads stat values LIVE from the item's SO fields (one shared access path — CollectStats — used for
        // both the plain list and the comparison). When inspecting an unequipped inventory item (Task 26), each
        // row also shows a coloured delta versus the item equipped in that slot (green = higher value,
        // red = lower; note a lower Cooldown multiplier is actually better despite reading red). An empty slot
        // compares against a zero baseline. Already-equipped items (opened from a slot) show no comparison.
        private void BuildStatRows()
        {
            if (_detailStatContainer == null) return;
            ClearChildren(_detailStatContainer);

            var inspected = new Dictionary<int, float>();
            CollectStats(_selectedItem, inspected);

            // Decide whether to compare: only for inventory items, and not against an empty self-reference.
            bool compare = !_selectedFromSlot;
            var equippedStats = new Dictionary<int, float>();
            if (compare)
            {
                LootItemSO equipped = _activeHero != null
                    ? _gear.GetLoadout(_activeHero).GetEquipped(_selectedItem.Slot) : null;
                if (equipped == _selectedItem) compare = false; // owned-and-equipped duplicate → nothing to compare
                else CollectStats(equipped, equippedStats); // equipped == null → empty → zero baseline (criterion 3)
            }

            // Plain list (no comparison): one row per stat on the inspected item, ordered by stat key.
            if (!compare)
            {
                if (inspected.Count == 0) { ShowNoBonuses(); return; }
                foreach (var kv in Ordered(inspected.Keys))
                    CreateStatRow(StatLabel(kv, inspected[kv]), StatTooltip(kv));
                return;
            }

            // Comparison: union of stat keys on either item; missing side counts as zero (criterion 4).
            var keys = new SortedSet<int>(inspected.Keys);
            keys.UnionWith(equippedStats.Keys);
            if (keys.Count == 0) { ShowNoBonuses(); return; }

            foreach (int key in keys)
            {
                inspected.TryGetValue(key, out float inspVal);
                equippedStats.TryGetValue(key, out float equipVal);
                string label = StatLabel(key, inspVal) + DeltaSuffix(key, inspVal - equipVal);
                CreateStatRow(label, StatTooltip(key));
            }
        }

        // Single stat-access path (reused by the plain list AND the comparison) — reads StatModifiers +
        // luckBonus straight off the SO, summing any duplicate-typed modifiers into one comparable value.
        private static void CollectStats(LootItemSO item, Dictionary<int, float> dest)
        {
            if (item == null) return;
            var mods = item.StatModifiers;
            for (int i = 0; i < mods.Count; i++)
            {
                int key = (int)mods[i].ModifierType;
                dest.TryGetValue(key, out float v);
                dest[key] = v + mods[i].Value;
            }
            if (item.LuckBonus != 0f)
            {
                dest.TryGetValue(LuckStatKey, out float v);
                dest[LuckStatKey] = v + item.LuckBonus;
            }
        }

        private static IEnumerable<int> Ordered(IEnumerable<int> keys)
        {
            var sorted = new SortedSet<int>(keys);
            return sorted;
        }

        private static string StatLabel(int key, float value) =>
            key == LuckStatKey ? GearStatInfo.LuckLabel(value) : GearStatInfo.Label((AbilityModifierType)key, value);

        private static string StatTooltip(int key) =>
            key == LuckStatKey ? GearStatInfo.LuckTooltip : GearStatInfo.Tooltip((AbilityModifierType)key);

        // Coloured "(+5)" / "(-3%)" suffix appended to a comparison row. Green = higher, red = lower, grey = same.
        private static string DeltaSuffix(int key, float delta)
        {
            string text = key == LuckStatKey ? GearStatInfo.LuckDelta(delta) : GearStatInfo.Delta((AbilityModifierType)key, delta);
            string color = delta > 0f ? "6FCF6F" : (delta < 0f ? "E0706F" : "999999");
            return $"  <color=#{color}>({text})</color>";
        }

        private void ShowNoBonuses() =>
            CreateText(_detailStatContainer, "<color=#888888>(No stat bonuses)</color>", 18f, TextAlignmentOptions.Left);

        private void OnDetailEquip()
        {
            if (_selectedItem == null || _activeHero == null) return;
            // Task 12/14 path — same GearManager.Equip the picker uses (consume + replace-not-destroy + save).
            if (_gear.Equip(_activeHero, _selectedItem))
            {
                RefreshSlots();
                RefreshInventory();
                RefreshDetail(); // keep inspecting the same item; refresh comparison + button availability
            }
        }

        private void OnDetailUnequip()
        {
            if (_selectedItem == null || _activeHero == null) return;
            // Only unequip when THIS item actually occupies its slot on the active hero.
            var loadout = _gear.GetLoadout(_activeHero);
            if (loadout.GetEquipped(_selectedItem.Slot) != _selectedItem) return;

            _gear.Unequip(_activeHero, _selectedItem.Slot); // Task 12 path: returns to inventory + saves
            RefreshSlots();
            RefreshInventory();
            RefreshDetail();
        }

        // Equip enabled while an unequipped copy is owned; Unequip enabled while equipped on the active hero.
        // Driven by current state (not the click source), so it stays correct after an equip/unequip action.
        private void RefreshDetailButtons()
        {
            if (_selectedItem == null) return;
            var loadout = _activeHero != null ? _gear.GetLoadout(_activeHero) : null;
            bool equippedOnHero = loadout != null && loadout.GetEquipped(_selectedItem.Slot) == _selectedItem;
            bool ownsUnequipped = _gear.Inventory.CountOf(_selectedItem) > 0;

            if (_detailEquipButton != null) _detailEquipButton.interactable = ownsUnequipped;
            if (_detailUnequipButton != null) _detailUnequipButton.interactable = equippedOnHero;
        }

        private TooltipPresenter CreateTooltipPresenter()
        {
            // Build a single shared tooltip under the canvas so it can float above every other element.
            Canvas canvas = null;
            if (_detailPanel != null) canvas = _detailPanel.GetComponentInParent<Canvas>();
            if (canvas == null && _inventoryContainer != null) canvas = _inventoryContainer.GetComponentInParent<Canvas>();
            if (canvas == null) return null;

            var go = new GameObject("GearTooltip", typeof(RectTransform));
            go.transform.SetParent(canvas.transform, false);
            return go.AddComponent<TooltipPresenter>();
        }

        private void CreateStatRow(string label, string tooltip)
        {
            // Row needs a raycast-target graphic for hover events; a faint Image doubles as that + a divider.
            var go = new GameObject("StatRow", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(_detailStatContainer, false);
            ((RectTransform)go.transform).sizeDelta = new Vector2(420f, 30f);
            var bg = go.GetComponent<Image>();
            bg.color = new Color(1f, 1f, 1f, 0.04f);
            bg.raycastTarget = true;

            var tmp = CreateText(go.transform, label, 18f, TextAlignmentOptions.Left);
            var lrt = tmp.rectTransform;
            lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one;
            lrt.offsetMin = new Vector2(8f, 0f); lrt.offsetMax = new Vector2(-8f, 0f);
            tmp.raycastTarget = false; // let the row Image receive the hover

            go.AddComponent<TooltipTrigger>().Configure(_tooltip, tooltip);
        }

        // A clickable list row: faint Image (also the click target) + a left-aligned rich-text label.
        private void CreateClickableRow(Transform parent, string richText, UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject("ItemRow", typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            ((RectTransform)go.transform).sizeDelta = new Vector2(560f, 34f);
            go.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.05f);

            var tmp = CreateText(go.transform, richText, 18f, TextAlignmentOptions.Left);
            var lrt = tmp.rectTransform;
            lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one;
            lrt.offsetMin = new Vector2(8f, 0f); lrt.offsetMax = new Vector2(-8f, 0f);
            tmp.raycastTarget = false;

            go.GetComponent<Button>().onClick.AddListener(onClick);
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
