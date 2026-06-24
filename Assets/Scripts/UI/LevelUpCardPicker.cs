using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Wavekeep.Core;
using Wavekeep.Core.Events;
using Wavekeep.Data;
using Wavekeep.Runtime;

namespace Wavekeep.UI
{
    /// <summary>
    /// The player-facing level-up flow (Task 07, migrated in Task 29). Subscribes to
    /// <see cref="XPLevelUpEvent"/>; on each level-up it pauses gameplay via the session
    /// <see cref="PauseState"/> and offers cards.
    ///
    /// Task 29 — the draw source changed: cards are drawn from the ACTIVE HERO's
    /// <see cref="UpgradeLineDefinitionSO"/> lines that are not yet at max tier (read from the live
    /// <see cref="HeroRuntime"/>). The old shared generic pool and tag-based hero-exclusive pool are gone
    /// (the generic pool's move into the shop is a separate, pending task). Picking a card advances that
    /// line one tier via <see cref="HeroRuntime.TryUpgradeLine"/>, which pushes the tier's effect into the
    /// run's <c>UpgradeInventory</c> — the same resolution path the old upgrades used, so abilities are
    /// unaffected. Selection stays random among eligible lines.
    ///
    /// Multi-level-up handling is unchanged: a burst of <see cref="XPLevelUpEvent"/>s queues picks; one
    /// card screen shows at a time and the world stays paused until the queue drains.
    /// </summary>
    [AddComponentMenu("Wavekeep/UI/Level-Up Card Picker")]
    public sealed class LevelUpCardPicker : MonoBehaviour
    {
        [Header("Dependencies")]
        [SerializeField] private GameSessionBootstrap _bootstrap;

        [Tooltip("How many cards to offer per level-up (clamped to the number of eligible lines).")]
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
            public TMP_Text BadgeText;   // Task 32: "BASIC" / "ULTIMATE"
            public Image BadgeImage;     // Task 32: per-skill colour chip behind the badge text
        }

        private EventBus _events;
        private PauseState _pause;

        // Task 29: the live hero, the single source of per-line tier state. Acquired lazily (it is spawned
        // at runtime by the hero-select flow), mirroring UltimateChargeBar — no static singleton.
        private HeroRuntime _hero;

        private readonly List<CardSlot> _slots = new List<CardSlot>();

        // Scratch buffers reused per draw so card selection doesn't allocate each level-up.
        private readonly List<UpgradeLineDefinitionSO> _eligibleLines = new List<UpgradeLineDefinitionSO>();
        private readonly List<UpgradeLineDefinitionSO> _currentDraw = new List<UpgradeLineDefinitionSO>();

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
            _pause = session.PauseState;

            BuildSlots();

            _events.Subscribe<XPLevelUpEvent>(OnLevelUp);

            SetPanelVisible(false);
        }

        private void OnDestroy()
        {
            if (_events == null) return;
            _events.Unsubscribe<XPLevelUpEvent>(OnLevelUp);
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
            // Task 32: larger card (legibility over fitting many on screen) — a vertical stack of
            // [ skill badge ][ name ][ description ][ Obtain button ].
            var cardGo = new GameObject("Card", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup));
            cardGo.transform.SetParent(_cardContainer, false);
            ((RectTransform)cardGo.transform).sizeDelta = new Vector2(340f, 440f);
            cardGo.GetComponent<Image>().color = new Color(0.15f, 0.15f, 0.2f, 0.95f);
            var vlg = cardGo.GetComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(16, 16, 16, 16);
            vlg.spacing = 12f;
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.childControlWidth = true;   // children take the card's inner width → description wraps correctly
            // Control height too, so each child gets its LayoutElement/preferred height and the layout stacks
            // them in sequence — otherwise (childControlHeight=false) heights collapse and the Obtain button
            // overlaps the description text.
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            // Skill badge (top): coloured chip with BASIC/ULTIMATE (colour + text set in PopulateSlot).
            var badgeGo = new GameObject("Badge", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            badgeGo.transform.SetParent(cardGo.transform, false);
            badgeGo.GetComponent<LayoutElement>().minHeight = 32f;
            var badgeImage = badgeGo.GetComponent<Image>();
            badgeImage.color = Color.gray;
            var badgeText = CreateText(badgeGo.transform, "BadgeLabel", 16f, FontStyles.Bold, 0f);
            badgeText.alignment = TextAlignmentOptions.Center;
            var bdRt = badgeText.rectTransform;
            bdRt.anchorMin = Vector2.zero; bdRt.anchorMax = Vector2.one;
            bdRt.offsetMin = Vector2.zero; bdRt.offsetMax = Vector2.zero;

            var nameText = CreateText(cardGo.transform, "Name", 26f, FontStyles.Bold, 44f);
            var infoText = CreateText(cardGo.transform, "Info", 19f, FontStyles.Normal, 240f);
            infoText.alignment = TextAlignmentOptions.Top;

            // Obtain button.
            var buttonGo = new GameObject("Obtain", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            buttonGo.transform.SetParent(cardGo.transform, false);
            buttonGo.GetComponent<LayoutElement>().minHeight = 52f;
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
                ObtainButton = button,
                BadgeText = badgeText,
                BadgeImage = badgeImage
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

        private void OnObtain(UpgradeLineDefinitionSO chosen)
        {
            // Ignore stray clicks once the screen is no longer presenting a choice.
            if (!_isShowing) return;

            // Task 29: advance the chosen line one tier via the hero (which feeds UpgradeInventory and
            // checks apex unlocks). No divergent add path.
            if (_hero != null && chosen != null)
            {
                _hero.TryUpgradeLine(chosen);
                Debug.Log($"[LevelUpCardPicker] Picked line '{chosen.LineName}' → tier {_hero.GetLineTier(chosen)}. " +
                          $"Pending picks left: {_pendingPicks - 1}.");
            }

            _pendingPicks--;
            AdvanceQueueOrResume();
        }

        // Shared drain step used after a pick AND after an auto-skip (empty draw), so the run never
        // soft-locks and the pause is always balanced.
        private void AdvanceQueueOrResume()
        {
            if (_pendingPicks > 0)
            {
                ShowCard(); // more queued level-ups — re-draw, keep the screen up (still paused)
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
            DrawLines(_currentDraw);

            if (_currentDraw.Count == 0)
            {
                // No eligible lines (no hero yet, hero has no lines, or all lines maxed) — auto-resolve so
                // the run never soft-locks.
                Debug.LogWarning("[LevelUpCardPicker] No eligible upgrade lines to offer; skipping pick.", this);
                _pendingPicks--;
                AdvanceQueueOrResume();
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
                    // Fewer eligible lines than slots this draw — hide the spare slot.
                    slot.Root.SetActive(false);
                }
            }

            SetPanelVisible(true);
        }

        private void PopulateSlot(CardSlot slot, UpgradeLineDefinitionSO line)
        {
            slot.NameText.text = line.LineName;
            slot.InfoText.text = BuildInfo(line);

            // Task 32: skill-source badge so the player can pattern-match Basic vs Ultimate at a glance.
            var (badgeLabel, badgeColor) = SkillBadge(line.Skill);
            slot.BadgeText.text = badgeLabel;
            slot.BadgeImage.color = badgeColor;

            slot.ObtainButton.onClick.RemoveAllListeners();
            slot.ObtainButton.onClick.AddListener(() => OnObtain(line));
        }

        // Task 32: consistent label + colour per skill so investing in the same skill looks the same each draw.
        private static (string label, Color color) SkillBadge(AbilityRole skill)
        {
            switch (skill)
            {
                case AbilityRole.Ultimate: return ("ULTIMATE", new Color(0.55f, 0.30f, 0.78f, 1f)); // purple
                case AbilityRole.Apex: return ("APEX", new Color(0.85f, 0.55f, 0.20f, 1f));          // amber (future)
                default: return ("BASIC", new Color(0.20f, 0.50f, 0.70f, 1f));                        // teal
            }
        }

        // Draw up to _cardsPerLevelUp DISTINCT not-yet-maxed lines from the active hero, via a partial
        // Fisher–Yates shuffle. Random selection among eligible lines (consistent with the old picker).
        private void DrawLines(List<UpgradeLineDefinitionSO> result)
        {
            result.Clear();

            if (_hero == null) _hero = Object.FindFirstObjectByType<HeroRuntime>();
            if (_hero == null) return;

            _eligibleLines.Clear();
            var lines = _hero.UpgradeLines;
            if (lines != null)
            {
                for (int i = 0; i < lines.Count; i++)
                {
                    var line = lines[i];
                    if (line != null && !_hero.IsLineMaxed(line)) _eligibleLines.Add(line);
                }
            }

            int want = Mathf.Min(Mathf.Clamp(_cardsPerLevelUp, 2, 3), _eligibleLines.Count);
            for (int k = 0; k < want; k++)
            {
                int swap = Random.Range(k, _eligibleLines.Count);
                (_eligibleLines[k], _eligibleLines[swap]) = (_eligibleLines[swap], _eligibleLines[k]);
                result.Add(_eligibleLines[k]);
            }
        }

        // Card body: the NEXT tier the pick would grant — its tier number and description (falling back to
        // a numeric description of the underlying effect when no text is authored).
        private string BuildInfo(UpgradeLineDefinitionSO line)
        {
            int next = (_hero != null ? _hero.GetLineTier(line) : 0) + 1;
            var tier = line.TierAt(next);

            string body;
            if (tier != null && !string.IsNullOrEmpty(tier.Description)) body = tier.Description;
            else body = DescribeEffect(tier != null ? tier.Effect : null);

            return $"<size=80%>Tier {next} / {line.TierCount}</size>\n\n{body}";
        }

        private static string DescribeEffect(UpgradeDefinitionSO upgrade)
        {
            if (upgrade == null) return "—";

            string main;
            switch (upgrade.EffectType)
            {
                case UpgradeEffectType.FlatDamageBonus: main = $"+{upgrade.EffectValue:0.#} damage"; break;
                case UpgradeEffectType.CooldownReductionPercent: main = $"-{upgrade.EffectValue:0.#}% cooldown"; break;
                case UpgradeEffectType.AoeRadiusBonus: main = $"+{upgrade.EffectValue:0.#} AoE radius"; break;
                default: main = upgrade.UpgradeName; break;
            }
            if (upgrade.AppliesStatusEffect)
                main += $"\nApplies {upgrade.StatusEffectType} ({upgrade.StatusDuration:0.#}s)";
            return main;
        }

        private void SetPanelVisible(bool visible)
        {
            if (_panel != null) _panel.SetActive(visible);
        }
    }
}
