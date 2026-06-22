using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Wavekeep.Abilities;
using Wavekeep.Core;
using Wavekeep.Core.Events;
using Wavekeep.Data;

namespace Wavekeep.UI
{
    /// <summary>
    /// The real player-facing level-up flow (Task 07), replacing Task 04's 1/2/3 debug keys. Subscribes
    /// to <see cref="XPLevelUpEvent"/> (Task 03's <c>XPManager</c>); on each level-up it pauses gameplay
    /// via the session <see cref="PauseState"/>, draws 2–3 random <see cref="UpgradeDefinitionSO"/> from
    /// the shared pool, and shows a card per choice. Picking a card adds it to <c>UpgradeInventory</c>
    /// through the SAME <see cref="UpgradeInventory.Add"/> call the debug keys use (no divergent path),
    /// then either shows the next queued pick or resumes the run.
    ///
    /// Multi-level-up handling: a single big XP gain makes <c>XPManager</c> publish several
    /// <see cref="XPLevelUpEvent"/>s synchronously (its threshold loop). Each increments a pending
    /// counter; one card screen is shown at a time and the next is shown only after the current pick,
    /// so picks queue instead of overlapping or being dropped. The world stays paused (no kills → no
    /// further level-ups) until the queue drains.
    ///
    /// Lives in Scripts/UI with the other Canvas controllers (HeroSelect/Shop). Card slots are built at
    /// runtime from the pool count, mirroring those controllers, so the editor only wires the frame.
    /// </summary>
    [AddComponentMenu("Wavekeep/UI/Level-Up Card Picker")]
    public sealed class LevelUpCardPicker : MonoBehaviour
    {
        [Header("Dependencies")]
        [SerializeField] private GameSessionBootstrap _bootstrap;

        [Header("Upgrade Pool (shared draw pool — add a UpgradeDefinitionSO to include it)")]
        [SerializeField] private List<UpgradeDefinitionSO> _upgradePool = new List<UpgradeDefinitionSO>();
        [Tooltip("How many cards to offer per level-up (clamped to the pool size).")]
        [SerializeField, Range(2, 3)] private int _cardsPerLevelUp = 3;

        [Header("UI")]
        [Tooltip("Root object shown while a card pick is pending, hidden otherwise.")]
        [SerializeField] private GameObject _panel;
        [Tooltip("Parent (with a horizontal layout) under which card slots are generated.")]
        [SerializeField] private RectTransform _cardContainer;

        private sealed class CardSlot
        {
            public GameObject Root;
            public TMP_Text NameText;
            public TMP_Text InfoText;
            public Button ObtainButton;
        }

        private EventBus _events;
        private UpgradeInventory _inventory;
        private PauseState _pause;

        private readonly List<CardSlot> _slots = new List<CardSlot>();

        // Scratch buffers reused per draw so card selection doesn't allocate each level-up.
        private readonly List<int> _drawIndices = new List<int>();
        private readonly List<UpgradeDefinitionSO> _currentDraw = new List<UpgradeDefinitionSO>();

        private int _pendingPicks;   // queued level-ups still awaiting a card choice
        private bool _isShowing;     // a card screen is currently displayed

        private void Start()
        {
            if (_bootstrap == null || _bootstrap.Session == null)
            {
                Debug.LogError("[LevelUpCardPicker] No GameSessionBootstrap/Session assigned; disabling.", this);
                enabled = false;
                return;
            }

            var session = _bootstrap.Session;
            _events = session.Events;
            _inventory = session.UpgradeInventory;
            _pause = session.PauseState;

            BuildSlots();

            _events.Subscribe<XPLevelUpEvent>(OnLevelUp);

            SetPanelVisible(false);
        }

        private void OnDestroy()
        {
            if (_events != null) _events.Unsubscribe<XPLevelUpEvent>(OnLevelUp);
        }

        private void BuildSlots()
        {
            if (_cardContainer == null) return;

            int slotCount = Mathf.Clamp(_cardsPerLevelUp, 2, 3);
            for (int i = 0; i < slotCount; i++)
            {
                _slots.Add(CreateSlot());
            }
        }

        private CardSlot CreateSlot()
        {
            // Card: a vertical stack of [ name ][ info ][ Obtain button ].
            var cardGo = new GameObject("Card", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup));
            cardGo.transform.SetParent(_cardContainer, false);
            ((RectTransform)cardGo.transform).sizeDelta = new Vector2(220f, 300f);
            cardGo.GetComponent<Image>().color = new Color(0.15f, 0.15f, 0.2f, 0.95f);
            var vlg = cardGo.GetComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(12, 12, 12, 12);
            vlg.spacing = 10f;
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.childControlWidth = true;
            vlg.childControlHeight = false;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            var nameText = CreateText(cardGo.transform, "Name", 24f, FontStyles.Bold, 40f);
            var infoText = CreateText(cardGo.transform, "Info", 18f, FontStyles.Normal, 180f);

            // Obtain button.
            var buttonGo = new GameObject("Obtain", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            buttonGo.transform.SetParent(cardGo.transform, false);
            buttonGo.GetComponent<LayoutElement>().minHeight = 48f;
            var button = buttonGo.GetComponent<Button>();

            var buttonLabel = CreateText(buttonGo.transform, "Label", 22f, FontStyles.Normal, 0f);
            buttonLabel.text = "Obtain";
            buttonLabel.color = Color.black;
            buttonLabel.alignment = TextAlignmentOptions.Center;
            var blRt = buttonLabel.rectTransform;
            blRt.anchorMin = Vector2.zero;
            blRt.anchorMax = Vector2.one;
            blRt.offsetMin = Vector2.zero;
            blRt.offsetMax = Vector2.zero;

            return new CardSlot
            {
                Root = cardGo,
                NameText = nameText,
                InfoText = infoText,
                ObtainButton = button
            };
        }

        private TextMeshProUGUI CreateText(Transform parent, string name, float fontSize, FontStyles style, float minHeight)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            if (minHeight > 0f) go.GetComponent<LayoutElement>().minHeight = minHeight;

            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.fontSize = fontSize;
            tmp.fontStyle = style;
            tmp.color = Color.white;
            tmp.alignment = TextAlignmentOptions.Top;
            if (TMP_Settings.defaultFontAsset != null) tmp.font = TMP_Settings.defaultFontAsset;
            return tmp;
        }

        // --- Queue + pause lifecycle -------------------------------------------------------------

        private void OnLevelUp(XPLevelUpEvent evt)
        {
            _pendingPicks++;

            // First pending pick of a burst: pause once and show the first card. Later events in the
            // same burst just grow the queue; the current card stays up until the player chooses.
            if (!_isShowing)
            {
                _isShowing = true;
                _pause.Pause();
                ShowCard();
            }
        }

        private void OnObtain(UpgradeDefinitionSO chosen)
        {
            // Ignore stray clicks once the screen is no longer presenting a choice.
            if (!_isShowing) return;

            // SAME path as the Task 04 debug keys — no divergent add logic (reviewer-blocking otherwise).
            _inventory.Add(chosen);
            Debug.Log($"[LevelUpCardPicker] Picked '{chosen.UpgradeName}'. Pending picks left: {_pendingPicks - 1}.");

            _pendingPicks--;

            if (_pendingPicks > 0)
            {
                // More queued level-ups — re-draw and keep the screen up (still paused).
                ShowCard();
            }
            else
            {
                _isShowing = false;
                SetPanelVisible(false);
                _pause.Resume();
            }
        }

        // --- Drawing + display -------------------------------------------------------------------

        private void ShowCard()
        {
            DrawUpgrades(_currentDraw);

            if (_currentDraw.Count == 0)
            {
                // No pool authored — can't offer a choice. Auto-resolve so the run never soft-locks.
                Debug.LogWarning("[LevelUpCardPicker] Upgrade pool is empty; skipping pick.", this);
                _pendingPicks--;
                if (_pendingPicks > 0)
                {
                    ShowCard();
                }
                else
                {
                    _isShowing = false;
                    SetPanelVisible(false);
                    _pause.Resume();
                }
                return;
            }

            for (int i = 0; i < _slots.Count; i++)
            {
                var slot = _slots[i];
                if (i < _currentDraw.Count)
                {
                    PopulateSlot(slot, _currentDraw[i]);
                    slot.Root.SetActive(true);
                }
                else
                {
                    // Pool smaller than the slot count this draw — hide the spare slot.
                    slot.Root.SetActive(false);
                }
            }

            SetPanelVisible(true);
        }

        private void PopulateSlot(CardSlot slot, UpgradeDefinitionSO upgrade)
        {
            slot.NameText.text = upgrade.UpgradeName;
            slot.InfoText.text = BuildInfo(upgrade);

            slot.ObtainButton.onClick.RemoveAllListeners();
            slot.ObtainButton.onClick.AddListener(() => OnObtain(upgrade));
        }

        // Draw up to _cardsPerLevelUp DISTINCT upgrades via a partial Fisher–Yates shuffle of indices.
        // Simple random draw (no owned-exclusion) is fine for MVP: there is no per-upgrade stack cap.
        private void DrawUpgrades(List<UpgradeDefinitionSO> result)
        {
            result.Clear();

            _drawIndices.Clear();
            for (int i = 0; i < _upgradePool.Count; i++)
            {
                if (_upgradePool[i] != null) _drawIndices.Add(i);
            }

            int want = Mathf.Min(Mathf.Clamp(_cardsPerLevelUp, 2, 3), _drawIndices.Count);
            for (int k = 0; k < want; k++)
            {
                int swap = Random.Range(k, _drawIndices.Count);
                (_drawIndices[k], _drawIndices[swap]) = (_drawIndices[swap], _drawIndices[k]);
                result.Add(_upgradePool[_drawIndices[k]]);
            }
        }

        private static string BuildInfo(UpgradeDefinitionSO upgrade)
        {
            string tags = "—";
            var t = upgrade.Tags;
            if (t != null && t.Count > 0)
            {
                var sb = new System.Text.StringBuilder();
                for (int i = 0; i < t.Count; i++)
                {
                    if (i > 0) sb.Append(", ");
                    sb.Append(t[i]);
                }
                tags = sb.ToString();
            }

            return $"<size=80%>Tags: {tags}</size>\n\n{DescribeEffect(upgrade)}";
        }

        private static string DescribeEffect(UpgradeDefinitionSO upgrade)
        {
            switch (upgrade.EffectType)
            {
                case UpgradeEffectType.FlatDamageBonus: return $"+{upgrade.EffectValue:0.#} damage";
                case UpgradeEffectType.CooldownReductionPercent: return $"-{upgrade.EffectValue:0.#}% cooldown";
                case UpgradeEffectType.AoeRadiusBonus: return $"+{upgrade.EffectValue:0.#} AoE radius";
                default: return upgrade.EffectType.ToString();
            }
        }

        private void SetPanelVisible(bool visible)
        {
            if (_panel != null) _panel.SetActive(visible);
        }
    }
}
