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
    /// Flow (documented decision): the Hub ABSORBS hero selection. "Start Run" stores the chosen TEAM on
    /// a <see cref="RunLaunchContext"/> and loads the gameplay scene, which auto-starts that team — the
    /// player never picks twice. The chosen loadouts reach gameplay via the existing disk save.
    ///
    /// Task 37 — TEAM SELECTION: each hero row carries a checkbox-style toggle marking whether that hero
    /// joins the next run; the run launches with exactly the toggled set (one or more). Which heroes EXIST
    /// and which are toggled lives in a <see cref="TeamSelectionModel"/> the UI reads — the UI never
    /// hardcodes a fixed number of slots, so a new hero asset added to <see cref="_heroRoster"/> appears
    /// automatically. A minimum of one hero is required to start; the future per-progression slot CAP plugs
    /// into <see cref="TeamSelectionModel.CanSelect"/> (see that type) without reworking this screen.
    /// Selecting a hero (its name button) for loadout EDITING is independent of toggling it into the team.
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
        [Tooltip("Task 37: feedback line under Start Run — shows the current team count, or a clear message " +
                 "when zero heroes are selected (run start is blocked). Optional; logging still happens if unwired.")]
        [SerializeField] private TMP_Text _startFeedbackLabel;

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

        [Header("Gear economy — Dust + Salvage (Task 73)")]
        [Tooltip("Read-only display source for salvage yields / forge costs (the SAME asset wired to the " +
                 "bootstrap). Actions still route through GearManager; this is only for showing numbers pre-confirm.")]
        [SerializeField] private GearEconomyConfigSO _economyConfig;
        [SerializeField] private TMP_Text _dustLabel;
        [Tooltip("Salvage button in the detail panel — enabled only for owned, UNEQUIPPED items.")]
        [SerializeField] private Button _detailSalvageButton;

        [Header("Artifact Forge (Task 73)")]
        [SerializeField] private Button _openForgeButton;
        [SerializeField] private GameObject _forgePanel;
        [SerializeField] private RectTransform _forgeContainer;
        [SerializeField] private TMP_Text _forgeDustLabel;
        [SerializeField] private Button _forgeCloseButton;

        [Header("Overflow resolution (Task 73)")]
        [SerializeField] private Button _openOverflowButton;
        [SerializeField] private GameObject _overflowPanel;
        [SerializeField] private RectTransform _overflowContainer;
        [SerializeField] private TMP_Text _overflowFeedbackLabel;
        [SerializeField] private Button _overflowCloseButton;

        [Header("Confirm modal (Task 73 — shared by salvage + forge)")]
        [SerializeField] private GameObject _confirmPanel;
        [SerializeField] private TMP_Text _confirmText;
        [SerializeField] private Button _confirmYesButton;
        [SerializeField] private Button _confirmNoButton;

        [Header("Mass salvage (Task 74)")]
        [Tooltip("Horizontal container HubController fills with one rarity quick-filter toggle per tier.")]
        [SerializeField] private RectTransform _rarityFilterContainer;
        [SerializeField] private TMP_Text _selectionLabel;
        [SerializeField] private Button _massSalvageButton;
        [SerializeField] private Button _clearSelectionButton;

        private GearManager _gear;
        private HeroDefinitionSO _activeHero;
        private System.Action _pendingConfirm; // Task 73: action run when the confirm modal is accepted

        // Task 74: the current mass-salvage selection (by instance identity). Only ever holds INVENTORY items —
        // equipped instances live in loadouts (not the inventory list), so they can never enter this set. Pruned
        // to live inventory membership on every RefreshInventory, so an item that gets equipped/salvaged drops out.
        private readonly HashSet<GearInstance> _salvageSelection = new HashSet<GearInstance>();

        // Task 37: which heroes exist + which are toggled into the next run. The UI reads this rather than
        // hardcoding slots, so new heroes appear automatically and a future slot cap plugs into the model.
        private TeamSelectionModel _teamSelection;

        // Task 25: the item currently shown in the detail panel (null = panel closed). Clicking this same
        // item again toggles the panel closed; clicking a different item re-targets the panel.
        // Task 67: now a unique GearInstance (identity by reference), not a shared SO.
        private GearInstance _selectedItem;
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

            // Task 37 + 42: the selectable roster + current team toggles, now gated by the PERSISTENT
            // hero-slot unlock ceiling. The cap (and per-slot wave requirements for locked rows) come from the
            // disk-backed HeroSlotUnlockManager — rebuilt on this scene load, so a slot unlocked in the last
            // run is reflected here with no app restart. Seed slot 1 (always unlocked) so a fresh Hub can start
            // a run immediately (and the player can still deselect to see the ≥1 message).
            var unlocks = _bootstrap.Session.HeroSlotUnlocks;
            int maxSlots = unlocks != null ? unlocks.MaxUnlockedHeroSlots : TeamSelectionModel.MinSelectableHeroes;
            _teamSelection = new TeamSelectionModel(_heroRoster, maxSlots, BuildUnlockMilestones(unlocks));
            if (_teamSelection.Available.Count > 0)
                _teamSelection.SetSelected(_teamSelection.Available[0], true);

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

            // Task 73: salvage / forge / overflow / confirm wiring. Each panel closes by default; the Dust
            // counter and the overflow affordance refresh from live GearManager state.
            if (_detailSalvageButton != null) _detailSalvageButton.onClick.AddListener(OnDetailSalvage);
            if (_openForgeButton != null) _openForgeButton.onClick.AddListener(OpenForge);
            if (_forgeCloseButton != null) _forgeCloseButton.onClick.AddListener(CloseForge);
            if (_openOverflowButton != null) _openOverflowButton.onClick.AddListener(OpenOverflow);
            if (_overflowCloseButton != null) _overflowCloseButton.onClick.AddListener(CloseOverflow);
            if (_confirmYesButton != null) _confirmYesButton.onClick.AddListener(OnConfirmYes);
            if (_confirmNoButton != null) _confirmNoButton.onClick.AddListener(CloseConfirm);
            CloseForge();
            CloseOverflow();
            CloseConfirm();
            RefreshDust();
            RefreshOverflowAffordance();

            // Task 74: mass-salvage controls. The rarity quick-filters are built by RefreshInventory (they need
            // live per-tier counts); the Mass Salvage / Clear buttons route through the same confirm modal as
            // single-item salvage. Both start disabled — RefreshInventory → UpdateSelectionUI enables them once
            // something is selected.
            if (_massSalvageButton != null) _massSalvageButton.onClick.AddListener(OnMassSalvage);
            if (_clearSelectionButton != null) _clearSelectionButton.onClick.AddListener(ClearSelection);

            // Default to the first hero so the loadout view is populated on launch.
            for (int i = 0; i < _heroRoster.Count; i++)
            {
                if (_heroRoster[i] != null) { SelectHero(_heroRoster[i]); break; }
            }
            RefreshInventory();
            UpdateStartFeedback();
        }

        // --- Hero selection -----------------------------------------------------------------------

        // Task 37: one row per AVAILABLE hero (iterated from the TeamSelectionModel, never a fixed count) —
        // [team toggle][icon][name button]. The toggle marks team membership; the name button selects the
        // hero for loadout editing (independent concepts). Rebuilt on each toggle so the checkboxes redraw.
        private void BuildHeroButtons()
        {
            if (_heroButtonContainer == null || _teamSelection == null) return;
            ClearChildren(_heroButtonContainer);

            var heroes = _teamSelection.Available;
            for (int i = 0; i < heroes.Count; i++)
            {
                var hero = heroes[i];
                if (hero == null) continue;
                var captured = hero;

                var row = CreateRow(_heroButtonContainer, new Vector2(560f, 52f));

                // Task 42: a hero "beyond the unlocked slot count" stays visible but locked. Its toggle is
                // disabled and a "Reach wave N to unlock" label replaces the team checkbox interaction.
                bool unlocked = _teamSelection.IsUnlocked(hero);

                bool inTeam = _teamSelection.IsSelected(hero);
                var toggle = CreateButton(row.transform, !unlocked ? "-" : (inTeam ? "✓" : ""),
                    !unlocked ? new Color(0.22f, 0.16f, 0.16f)
                              : (inTeam ? new Color(0.20f, 0.55f, 0.25f) : new Color(0.28f, 0.28f, 0.34f)),
                    Color.white, new Vector2(44f, 44f), () => OnToggleTeam(captured));
                toggle.interactable = unlocked; // locked slots can't be toggled into the team

                // Hero icon (placeholder tint when no sprite is authored — same pattern as the gear panel).
                BuildHeroIcon(row.transform, hero);

                // Name button — selects this hero for loadout editing (allowed even while team-locked, so the
                // player can still inspect/prep that hero's gear for when it unlocks).
                CreateButton(row.transform, hero.HeroName, unlocked ? hero.Tint : new Color(0.45f, 0.45f, 0.50f),
                    Color.black, new Vector2(200f, 44f), () => SelectHero(captured));

                // Locked rows show their goal; unlocked rows leave the trailing space empty.
                if (!unlocked)
                {
                    int wave = _teamSelection.UnlockWaveRequirement(hero);
                    string reqText = wave > 0
                        ? $"<color=#E0A04C>[Locked] Reach wave {wave} to unlock</color>"
                        : "<color=#E0A04C>[Locked]</color>";
                    var lbl = CreateText(row.transform, reqText, 16f, TextAlignmentOptions.Left);
                    ((RectTransform)lbl.transform).sizeDelta = new Vector2(230f, 44f);
                }
            }
        }

        // Task 42: build the per-extra-slot wave-requirement array the TeamSelectionModel expects (index 0 →
        // slot 2). Reads the persistent manager's per-slot thresholds so the Hub never hardcodes 15/30/50.
        private static int[] BuildUnlockMilestones(Wavekeep.Progression.HeroSlotUnlockManager unlocks)
        {
            if (unlocks == null) return System.Array.Empty<int>();
            int extraSlots = unlocks.MaxPossibleHeroSlots - 1;
            if (extraSlots <= 0) return System.Array.Empty<int>();

            var milestones = new int[extraSlots];
            for (int i = 0; i < extraSlots; i++)
                milestones[i] = unlocks.WaveToUnlockSlot(i + 2); // index 0 → slot 2
            return milestones;
        }

        private void BuildHeroIcon(Transform parent, HeroDefinitionSO hero)
        {
            var go = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            ((RectTransform)go.transform).sizeDelta = new Vector2(44f, 44f);
            var img = go.GetComponent<Image>();
            if (hero.Icon != null) { img.sprite = hero.Icon; img.color = Color.white; }
            else img.color = hero.Tint; // no artwork yet → tint placeholder so heroes still read distinct
        }

        // Task 37: flip team membership, redraw the checkboxes, and refresh the start-availability message.
        private void OnToggleTeam(HeroDefinitionSO hero)
        {
            if (_teamSelection == null) return;
            _teamSelection.Toggle(hero);
            BuildHeroButtons();
            UpdateStartFeedback();
        }

        // Task 37: reflect current team size in the feedback line + gate the Start button so a zero-hero run
        // can't even be clicked (OnStartRun re-checks too, so the rule holds even if this label is unwired).
        private void UpdateStartFeedback()
        {
            int count = _teamSelection != null ? _teamSelection.SelectedCount : 0;
            bool canStart = _teamSelection != null && _teamSelection.CanStartRun;

            if (_startRunButton != null) _startRunButton.interactable = canStart;

            if (_startFeedbackLabel == null) return;
            if (canStart)
            {
                // Task 42: show the current team size against the unlocked cap so the goal is always visible.
                int cap = _teamSelection.MaxSelectableHeroes;
                _startFeedbackLabel.text =
                    $"<color=#9FE0A0>Team: {count}/{cap} hero{(cap == 1 ? "" : "es")} selected</color>";
            }
            else
            {
                _startFeedbackLabel.text =
                    $"<color=#E0A04C>Select at least {TeamSelectionModel.MinSelectableHeroes} hero to start a run.</color>";
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

        private void BuildSlotRow(GearSlot slot, GearInstance equipped)
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
            foreach (var item in _gear.Inventory.Items)
            {
                if (item == null || item.Slot != slot) continue; // valid-for-slot filter
                any = true;

                var capturedItem = item;
                string label = $"<color=#{RarityHex(item.Rarity)}>[{item.Rarity}]</color> {item.ItemName}";
                CreateButton(_equipPickerContainer, label, new Color(0.18f, 0.18f, 0.24f), Color.white,
                    new Vector2(420f, 40f), () => OnEquipFromPicker(slot, capturedItem));
            }

            if (!any)
            {
                CreateText(_equipPickerContainer, "(No items owned for this slot)", 18f, TextAlignmentOptions.Center);
            }

            _equipPickerPanel.SetActive(true);
        }

        private void OnEquipFromPicker(GearSlot slot, GearInstance item)
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
            // Task 74: prune the mass-salvage selection to LIVE inventory membership before drawing. An instance
            // that was equipped or salvaged elsewhere (so it left the inventory list) silently drops out — this is
            // the structural guarantee that an equipped item can never remain selected, since equipped instances
            // live in loadouts, not here.
            _salvageSelection.RemoveWhere(it => it == null || !_gear.Inventory.Contains(it));

            if (_inventoryContainer == null) { UpdateSelectionUI(); return; }
            ClearChildren(_inventoryContainer);

            // Group/sort: by slot, then rarity (descending) — simple grouping per the task. Task 67: each entry
            // is now a unique instance (no stack counts).
            var items = new List<GearInstance>(_gear.Inventory.Items);
            items.Sort((a, b) =>
            {
                int bySlot = ((int)a.Slot).CompareTo((int)b.Slot);
                if (bySlot != 0) return bySlot;
                return ((int)b.Rarity).CompareTo((int)a.Rarity);
            });

            if (items.Count == 0)
            {
                CreateText(_inventoryContainer, "(Inventory empty — kill enemies / bosses to find gear)", 18f, TextAlignmentOptions.Left);
                UpdateSelectionUI();
                return;
            }

            for (int i = 0; i < items.Count; i++)
                BuildInventoryRow(items[i]);

            UpdateSelectionUI();
        }

        // Task 74: an inventory row is now [selection checkbox][clickable label]. The checkbox toggles the item in
        // and out of the mass-salvage selection; the label still opens the Task 25 detail panel (independent
        // concepts, like the Task 37 hero rows). Only inventory items ever get a row, so equipped items are
        // structurally absent from the list — they can't be checked or selected by any path.
        private void BuildInventoryRow(GearInstance item)
        {
            var row = CreateRow(_inventoryContainer, new Vector2(560f, 34f));
            var captured = item;
            bool selected = _salvageSelection.Contains(item);

            CreateButton(row.transform, selected ? "✓" : "",
                selected ? new Color(0.20f, 0.55f, 0.25f) : new Color(0.28f, 0.28f, 0.34f),
                Color.white, new Vector2(32f, 30f), () => ToggleSelection(captured));

            string text = $"<color=#{RarityHex(item.Rarity)}>[{item.Rarity}]</color> {item.ItemName} " +
                          $"<size=80%>({item.Slot})</size>";
            // Task 25/26: clicking the label opens the detail panel (Equip available there); fromSlot=false → the
            // panel compares it against the slot's currently equipped item.
            CreateClickableRow(row.transform, text, new Vector2(516f, 30f), selected,
                () => OnItemClicked(captured, false));
        }

        // --- Gear detail panel (Task 25) ----------------------------------------------------------

        private void OnItemClicked(GearInstance item, bool fromSlot)
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
                GearInstance equipped = _activeHero != null
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
        private static void CollectStats(GearInstance item, Dictionary<int, float> dest)
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
            bool ownsUnequipped = _gear.Inventory.Contains(_selectedItem);

            if (_detailEquipButton != null) _detailEquipButton.interactable = ownsUnequipped;
            if (_detailUnequipButton != null) _detailUnequipButton.interactable = equippedOnHero;

            // Task 73: salvage is allowed ONLY for an owned, unequipped item (equipped items are disabled here,
            // reinforcing the Task 71 rule at the UI layer — there's no path to salvage an equipped item). The
            // label previews the Dust yield so the reward is visible before the confirm step.
            if (_detailSalvageButton != null)
            {
                _detailSalvageButton.interactable = ownsUnequipped;
                int yield = (ownsUnequipped && _economyConfig != null)
                    ? _economyConfig.SalvageYield(_selectedItem.Rarity) : 0;
                SetButtonLabel(_detailSalvageButton, ownsUnequipped ? $"Salvage +{yield}" : "Salvage");
            }
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

        // A clickable list row: faint Image (also the click target) + a left-aligned rich-text label. Task 74:
        // takes an explicit size (so it can sit beside a selection checkbox) and a highlight flag (brighter fill
        // when the row's item is in the mass-salvage selection).
        private void CreateClickableRow(Transform parent, string richText, Vector2 size, bool highlight,
            UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject("ItemRow", typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            ((RectTransform)go.transform).sizeDelta = size;
            go.GetComponent<Image>().color = new Color(1f, 1f, 1f, highlight ? 0.12f : 0.05f);

            var tmp = CreateText(go.transform, richText, 18f, TextAlignmentOptions.Left);
            var lrt = tmp.rectTransform;
            lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one;
            lrt.offsetMin = new Vector2(8f, 0f); lrt.offsetMax = new Vector2(-8f, 0f);
            tmp.raycastTarget = false;

            go.GetComponent<Button>().onClick.AddListener(onClick);
        }

        // --- Task 73: Dust counter ----------------------------------------------------------------

        private void RefreshDust()
        {
            string text = $"Dust: {_gear.SalvageDust}";
            if (_dustLabel != null) _dustLabel.text = text;
            if (_forgeDustLabel != null) _forgeDustLabel.text = text;
        }

        // --- Task 73: Salvage (from the detail panel) ---------------------------------------------

        private void OnDetailSalvage()
        {
            if (_selectedItem == null) return;
            // Only an owned, unequipped instance is salvageable (equipped lives in a loadout, not inventory).
            if (!_gear.Inventory.Contains(_selectedItem)) return;

            var item = _selectedItem;
            int yield = _economyConfig != null ? _economyConfig.SalvageYield(item.Rarity) : 0;
            AskConfirm($"Salvage <color=#{RarityHex(item.Rarity)}>[{item.Rarity}]</color> {item.ItemName} " +
                       $"for <color=#9FE0A0>+{yield} Dust</color>?",
                () =>
                {
                    int got = _gear.Salvage(item.ItemId);
                    if (_selectedItem == item) CloseDetail(); // the item no longer exists
                    RefreshInventory();
                    RefreshDust();
                    Debug.Log($"[HubController] Salvaged '{item.ItemName}' → +{got} Dust (total {_gear.SalvageDust}).");
                });
        }

        // --- Task 74: Mass salvage (multi-select + rarity quick-filter) ---------------------------

        // Chosen behaviors (flagged in the summary):
        // - Equipped items are HIDDEN from selection (they aren't in the inventory list at all), not shown disabled.
        // - A rarity quick-filter is a SELECTION aid: clicking it toggles ALL currently-eligible items of that tier
        //   in the selection. If every eligible item of that tier is already selected it deselects them; otherwise it
        //   selects them (extending any manual selection). Manually deselecting one item of a "fully selected" tier
        //   just clears that tier's all-selected highlight — re-clicking the filter re-selects the whole tier.
        // - Both paths feed the same selection set and the same confirm step (no instant/skip-confirm path).

        // Toggle a single inventory item in/out of the mass-salvage selection. Guarded to inventory membership so
        // an equipped instance can never enter the set even if a stale row callback fires.
        private void ToggleSelection(GearInstance item)
        {
            if (item == null || !_gear.Inventory.Contains(item)) return;
            if (!_salvageSelection.Remove(item)) _salvageSelection.Add(item);
            RefreshInventory(); // redraw checkmarks/highlights + selection totals
        }

        // Add or remove every eligible (== every inventory) item of the given rarity as one action.
        private void OnRarityFilter(Rarity rarity)
        {
            var eligible = new List<GearInstance>();
            foreach (var it in _gear.Inventory.Items)
                if (it != null && it.Rarity == rarity) eligible.Add(it);
            if (eligible.Count == 0) return;

            bool allSelected = true;
            for (int i = 0; i < eligible.Count; i++)
                if (!_salvageSelection.Contains(eligible[i])) { allSelected = false; break; }

            for (int i = 0; i < eligible.Count; i++)
            {
                if (allSelected) _salvageSelection.Remove(eligible[i]);
                else _salvageSelection.Add(eligible[i]);
            }
            RefreshInventory();
        }

        private void ClearSelection()
        {
            if (_salvageSelection.Count == 0) return;
            _salvageSelection.Clear();
            RefreshInventory();
        }

        // Single confirm step showing total count + total Dust, then salvages each selected item through the SAME
        // GearManager.Salvage(instanceId) path as single-item salvage (no yield logic duplicated). Snapshotting the
        // selection means salvages mutating the inventory mid-loop can't invalidate the iteration.
        private void OnMassSalvage()
        {
            if (_salvageSelection.Count == 0) return;

            var snapshot = new List<GearInstance>(_salvageSelection);
            int count = snapshot.Count;
            int totalDust = 0;
            for (int i = 0; i < snapshot.Count; i++)
                totalDust += _economyConfig != null ? _economyConfig.SalvageYield(snapshot[i].Rarity) : 0;

            AskConfirm($"Salvage <color=#9FE0A0>{count} item{(count == 1 ? "" : "s")}</color> " +
                       $"for <color=#9FE0A0>+{totalDust} Dust</color>?",
                () =>
                {
                    int got = 0;
                    for (int i = 0; i < snapshot.Count; i++)
                    {
                        var it = snapshot[i];
                        if (it == null || !_gear.Inventory.Contains(it)) continue; // safety: skip anything no longer free
                        got += _gear.Salvage(it.ItemId);
                    }
                    _salvageSelection.Clear();
                    // If the detail panel was inspecting one of the salvaged items, it no longer exists → close it.
                    if (_selectedItem != null && snapshot.Contains(_selectedItem)) CloseDetail();
                    RefreshInventory();
                    RefreshDust();
                    Debug.Log($"[HubController] Mass-salvaged {count} item(s) → +{got} Dust (total {_gear.SalvageDust}).");
                });
        }

        // Live selection summary + rarity-filter redraw. Called from RefreshInventory on every selection change.
        private void UpdateSelectionUI()
        {
            int count = _salvageSelection.Count;
            int totalDust = 0;
            foreach (var it in _salvageSelection)
                totalDust += _economyConfig != null ? _economyConfig.SalvageYield(it.Rarity) : 0;

            if (_selectionLabel != null)
            {
                _selectionLabel.text = count == 0
                    ? "<color=#888888>No items selected for salvage</color>"
                    : $"<color=#9FE0A0>Selected: {count} item{(count == 1 ? "" : "s")} — +{totalDust} Dust</color>";
            }

            if (_massSalvageButton != null)
            {
                _massSalvageButton.interactable = count > 0;
                SetButtonLabel(_massSalvageButton, count > 0 ? $"Mass Salvage ({count})" : "Mass Salvage");
            }
            if (_clearSelectionButton != null) _clearSelectionButton.interactable = count > 0;

            BuildRarityFilters();
        }

        // One quick-filter button per rarity tier. Fill colour reflects selection state: green = all of that tier
        // selected, olive = some selected, neutral = none, dark/disabled = no items of that tier owned.
        private void BuildRarityFilters()
        {
            if (_rarityFilterContainer == null) return;
            ClearChildren(_rarityFilterContainer);

            foreach (Rarity rarity in System.Enum.GetValues(typeof(Rarity)))
            {
                int total = 0, sel = 0;
                foreach (var it in _gear.Inventory.Items)
                {
                    if (it == null || it.Rarity != rarity) continue;
                    total++;
                    if (_salvageSelection.Contains(it)) sel++;
                }

                bool allSel = total > 0 && sel == total;
                Color bg = total == 0 ? new Color(0.20f, 0.20f, 0.24f)
                    : allSel ? new Color(0.20f, 0.55f, 0.25f)
                    : (sel > 0 ? new Color(0.40f, 0.42f, 0.26f) : new Color(0.28f, 0.28f, 0.34f));

                var captured = rarity;
                string label = $"<color=#{RarityHex(rarity)}>{rarity}</color> <size=80%>({total})</size>";
                var btn = CreateButton(_rarityFilterContainer, label, bg, Color.white,
                    new Vector2(120f, 40f), () => OnRarityFilter(captured));
                btn.interactable = total > 0;
            }
        }

        // --- Task 73: Artifact Forge --------------------------------------------------------------

        private void OpenForge()
        {
            if (_forgePanel == null || _forgeContainer == null) return;
            BuildForgeRows();
            RefreshDust();
            _forgePanel.SetActive(true);
        }

        // One row per rarity: "[Rarity]  N Dust" + a Forge button that's disabled (and greyed) when unaffordable.
        // Deterministic — the player explicitly picks the rarity; no RNG on the outcome (Task 71 rule).
        private void BuildForgeRows()
        {
            ClearChildren(_forgeContainer);
            foreach (Rarity rarity in System.Enum.GetValues(typeof(Rarity)))
            {
                int cost = _economyConfig != null ? _economyConfig.ForgeCost(rarity) : 0;
                bool affordable = cost > 0 && _gear.SalvageDust >= cost;
                var captured = rarity;

                var row = CreateRow(_forgeContainer, new Vector2(540f, 48f));
                var label = CreateText(row.transform,
                    $"<color=#{RarityHex(rarity)}>[{rarity}]</color>  <size=85%>{cost} Dust</size>",
                    20f, TextAlignmentOptions.Left);
                ((RectTransform)label.transform).sizeDelta = new Vector2(340f, 44f);

                var btn = CreateButton(row.transform, affordable ? "Forge" : "Need Dust",
                    affordable ? new Color(0.2f, 0.5f, 0.8f) : new Color(0.3f, 0.3f, 0.34f),
                    Color.white, new Vector2(160f, 40f), () => OnForgeRarity(captured));
                btn.interactable = affordable;
            }
        }

        private void OnForgeRarity(Rarity rarity)
        {
            int cost = _economyConfig != null ? _economyConfig.ForgeCost(rarity) : 0;
            if (cost <= 0 || _gear.SalvageDust < cost) return; // button is already disabled, but guard anyway

            AskConfirm($"Forge a <color=#{RarityHex(rarity)}>[{rarity}]</color> Artifact for " +
                       $"<color=#E0C060>{cost} Dust</color>?",
                () =>
                {
                    var instance = _gear.ForgeArtifact(rarity);
                    if (instance != null) Debug.Log($"[HubController] Forged '{instance.ItemName}'.");
                    RefreshInventory();
                    RefreshDust();
                    RefreshOverflowAffordance();
                    if (_forgePanel != null && _forgePanel.activeSelf) BuildForgeRows(); // re-evaluate affordability
                });
        }

        private void CloseForge()
        {
            if (_forgePanel != null) _forgePanel.SetActive(false);
        }

        // --- Task 73: Overflow resolution ---------------------------------------------------------

        // The "Resolve Overflow (N)" entry point only appears while the buffer is non-empty.
        private void RefreshOverflowAffordance()
        {
            int count = _gear.Overflow.Count;
            if (_openOverflowButton != null)
            {
                _openOverflowButton.gameObject.SetActive(count > 0);
                SetButtonLabel(_openOverflowButton, $"Overflow ({count})");
            }
        }

        private void OpenOverflow()
        {
            if (_overflowPanel == null || _overflowContainer == null) return;
            if (_overflowFeedbackLabel != null) _overflowFeedbackLabel.text = "";
            BuildOverflowRows();
            _overflowPanel.SetActive(true);
        }

        private void BuildOverflowRows()
        {
            ClearChildren(_overflowContainer);
            var items = _gear.Overflow;
            if (items.Count == 0)
            {
                CreateText(_overflowContainer, "(No pending items)", 18f, TextAlignmentOptions.Left);
                return;
            }

            // Snapshot so a claim/salvage that mutates the buffer mid-rebuild is safe.
            var snapshot = new List<GearInstance>(items);
            for (int i = 0; i < snapshot.Count; i++)
            {
                var item = snapshot[i];
                if (item == null) continue;
                var captured = item;

                var row = CreateRow(_overflowContainer, new Vector2(580f, 44f));
                var label = CreateText(row.transform,
                    $"<color=#{RarityHex(item.Rarity)}>[{item.Rarity}]</color> {item.ItemName} <size=80%>({item.Slot})</size>",
                    18f, TextAlignmentOptions.Left);
                ((RectTransform)label.transform).sizeDelta = new Vector2(330f, 40f);

                CreateButton(row.transform, "Claim", new Color(0.2f, 0.5f, 0.8f), Color.white,
                    new Vector2(90f, 36f), () => OnClaimOverflow(captured));
                int yield = _economyConfig != null ? _economyConfig.SalvageYield(item.Rarity) : 0;
                CreateButton(row.transform, $"Salvage +{yield}", new Color(0.5f, 0.4f, 0.25f), Color.white,
                    new Vector2(130f, 36f), () => OnSalvageOverflow(captured));
            }
        }

        private void OnClaimOverflow(GearInstance item)
        {
            if (item == null) return;
            if (_gear.ClaimOverflow(item.ItemId))
            {
                if (_overflowFeedbackLabel != null)
                    _overflowFeedbackLabel.text = $"<color=#9FE0A0>Claimed {item.ItemName} into inventory.</color>";
                AfterOverflowChange();
            }
            else
            {
                // Full-inventory feedback (acceptance criterion) — never a silent failure.
                if (_overflowFeedbackLabel != null)
                    _overflowFeedbackLabel.text =
                        $"<color=#E0706F>Inventory full ({_gear.Inventory.Count}/{_gear.Capacity}). " +
                        "Salvage something first.</color>";
            }
        }

        private void OnSalvageOverflow(GearInstance item)
        {
            if (item == null) return;
            int yield = _economyConfig != null ? _economyConfig.SalvageYield(item.Rarity) : 0;
            AskConfirm($"Salvage <color=#{RarityHex(item.Rarity)}>[{item.Rarity}]</color> {item.ItemName} " +
                       $"for <color=#9FE0A0>+{yield} Dust</color>?",
                () =>
                {
                    int got = _gear.Salvage(item.ItemId);
                    if (_overflowFeedbackLabel != null)
                        _overflowFeedbackLabel.text = $"<color=#9FE0A0>Salvaged for +{got} Dust.</color>";
                    AfterOverflowChange();
                });
        }

        // Refresh everything overflow-related after a claim/salvage; auto-close when the buffer empties.
        private void AfterOverflowChange()
        {
            RefreshInventory();
            RefreshDust();
            RefreshOverflowAffordance();
            if (_gear.Overflow.Count == 0) CloseOverflow();
            else if (_overflowPanel != null && _overflowPanel.activeSelf) BuildOverflowRows();
        }

        private void CloseOverflow()
        {
            if (_overflowPanel != null) _overflowPanel.SetActive(false);
        }

        // --- Task 73: shared confirm modal --------------------------------------------------------

        private void AskConfirm(string message, System.Action onYes)
        {
            _pendingConfirm = onYes;
            if (_confirmText != null) _confirmText.text = message;
            if (_confirmPanel != null)
            {
                _confirmPanel.transform.SetAsLastSibling(); // render above any other open modal
                _confirmPanel.SetActive(true);
            }
            else
            {
                // No confirm UI wired (older scene) → execute immediately so the action still works.
                var a = _pendingConfirm; _pendingConfirm = null; a?.Invoke();
            }
        }

        private void OnConfirmYes()
        {
            var action = _pendingConfirm;
            _pendingConfirm = null;
            CloseConfirm();
            action?.Invoke();
        }

        private void CloseConfirm()
        {
            _pendingConfirm = null;
            if (_confirmPanel != null) _confirmPanel.SetActive(false);
        }

        private static void SetButtonLabel(Button button, string text)
        {
            if (button == null) return;
            var lbl = button.GetComponentInChildren<TMP_Text>();
            if (lbl != null) lbl.text = text;
        }

        // --- Start run ----------------------------------------------------------------------------

        private void OnStartRun()
        {
            // Task 37: launch the TOGGLED TEAM, not the loadout-editing hero. Enforce the ≥1 minimum here
            // too (not just via the disabled button) so a zero-hero run can never start — reviewer-blocking.
            if (_teamSelection == null || !_teamSelection.CanStartRun)
            {
                Debug.LogWarning("[HubController] Cannot start: select at least " +
                                 $"{TeamSelectionModel.MinSelectableHeroes} hero first.");
                UpdateStartFeedback();
                return;
            }

            var team = _teamSelection.GetSelectedTeam();

            // Carry the chosen team across the scene load; the gameplay scene auto-starts them (no second pick).
            RunLaunchContext.GetOrCreate().SetTeam(team);
            Debug.Log($"[HubController] Starting run with {team.Count} hero(es).");
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
