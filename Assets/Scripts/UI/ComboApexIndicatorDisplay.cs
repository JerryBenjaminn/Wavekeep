using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Wavekeep.Core;

namespace Wavekeep.UI
{
    /// <summary>
    /// Task 38: HUD indicator for UNLOCKED cross-hero combo apexes (Frozen Lightning), extending the Task 32
    /// apex-indicator pattern. Deliberately DISTINCT from <see cref="ApexCooldownDisplay"/>: a passive combo
    /// has no cooldown of its own, so each unlocked combo shows a simple static "ACTIVE" badge (a solid
    /// coloured label, NO fill bar) rather than the cooldown-fill bars used for the single-hero apexes
    /// (Remorseless Winter / Permafrost Eruption / Thunderstorm / Lethal Surge). Badges are generated on
    /// demand and only appear once a combo unlocks — i.e. both prerequisite apexes are live in the same run.
    ///
    /// Reads the run's <see cref="ComboApexState"/> from the session via the bootstrap (no static singleton,
    /// CLAUDE.md §3.5); shows nothing until a session and at least one unlocked combo exist.
    /// </summary>
    [AddComponentMenu("Wavekeep/UI/Combo Apex Indicator Display")]
    public sealed class ComboApexIndicatorDisplay : MonoBehaviour
    {
        [Tooltip("Parent (with a vertical layout) the per-combo badges are generated into.")]
        [SerializeField] private RectTransform _container;
        [Tooltip("Sprite used for the badge background (wire the built-in UI/Skin/UISprite).")]
        [SerializeField] private Sprite _badgeSprite;
        [Tooltip("Session source for the combo-apex resolver. Required — no fallback scene scan, since the " +
                 "combo state is a run service rather than a scene object.")]
        [SerializeField] private GameSessionBootstrap _bootstrap;

        [Header("Appearance (solid badge — no cooldown fill, to read as 'passive/always-on')")]
        [SerializeField] private Color _badgeColor = new Color(0.25f, 0.6f, 1f, 0.9f);

        private sealed class Badge
        {
            public GameObject Root;
            public TMP_Text Label;
        }

        private readonly List<Badge> _badges = new List<Badge>();

        private void Update()
        {
            if (_container == null) return;

            var session = _bootstrap != null ? _bootstrap.Session : null;
            var combos = session != null ? session.ComboApex : null;
            var list = combos != null ? combos.Combos : null;

            int index = 0;
            for (int i = 0; list != null && i < list.Count; i++)
            {
                var combo = list[i];
                if (combo == null || !combos.IsUnlocked(combo)) continue;

                if (_badges.Count <= index) _badges.Add(CreateBadge());
                var badge = _badges[index++];
                if (!badge.Root.activeSelf) badge.Root.SetActive(true);

                string comboName = string.IsNullOrEmpty(combo.ComboName) ? "Combo Apex" : combo.ComboName;
                badge.Label.text = $"{comboName} — ACTIVE";
            }

            // Deactivate any surplus badges (combo locked again / none unlocked).
            for (int i = index; i < _badges.Count; i++)
            {
                if (_badges[i].Root.activeSelf) _badges[i].Root.SetActive(false);
            }
        }

        private Badge CreateBadge()
        {
            var rowGo = new GameObject("ComboBadge", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            rowGo.transform.SetParent(_container, false);
            ((RectTransform)rowGo.transform).sizeDelta = new Vector2(360f, 30f);
            rowGo.GetComponent<LayoutElement>().minHeight = 30f;
            var bg = rowGo.GetComponent<Image>();
            bg.sprite = _badgeSprite;
            bg.type = Image.Type.Sliced;
            bg.color = _badgeColor; // solid fill — no Filled bar, so it never reads as a charging cooldown

            var labelGo = new GameObject("Label", typeof(RectTransform));
            labelGo.transform.SetParent(rowGo.transform, false);
            var label = labelGo.AddComponent<TextMeshProUGUI>();
            label.fontSize = 16f;
            label.color = Color.white;
            label.alignment = TextAlignmentOptions.Center;
            if (TMP_Settings.defaultFontAsset != null) label.font = TMP_Settings.defaultFontAsset;
            var lRt = label.rectTransform;
            lRt.anchorMin = Vector2.zero;
            lRt.anchorMax = Vector2.one;
            lRt.offsetMin = Vector2.zero;
            lRt.offsetMax = Vector2.zero;

            return new Badge { Root = rowGo, Label = label };
        }
    }
}
