using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Wavekeep.Core;

namespace Wavekeep.UI
{
    /// <summary>
    /// HUD showing each active hero's ultimate charge (Task 21, made multi-hero in Task 36). It generates
    /// one labelled bar PER active hero into a container — the same generated-bars approach as
    /// <see cref="ApexCooldownDisplay"/> — and reads progress DIRECTLY from each ultimate's
    /// <c>IAbility.CooldownProgress01</c>/<c>IsReady</c> every frame, owning no cooldown state of its own
    /// (so it can never disagree with the real ability — a reviewer-blocking requirement).
    ///
    /// Heroes are resolved through the session's hero registry (<see cref="GameSession.Heroes"/>) via
    /// <see cref="HeroLookup"/>; there is no static singleton. Bars only ever grow during a run.
    /// </summary>
    [AddComponentMenu("Wavekeep/UI/Ultimate Charge Bar")]
    public sealed class UltimateChargeBar : MonoBehaviour
    {
        [Header("Wiring")]
        [Tooltip("Parent (with a vertical layout) the per-hero ultimate bars are generated into.")]
        [SerializeField] private RectTransform _container;
        [Tooltip("Sprite used for the bar background + fill (wire the built-in UI/Skin/UISprite).")]
        [SerializeField] private Sprite _barSprite;
        [Tooltip("Task 36: session source for the active-hero registry. If unset, falls back to a scene scan.")]
        [SerializeField] private GameSessionBootstrap _bootstrap;

        [Header("Appearance")]
        [SerializeField] private Color _chargingColor = new Color(0.20f, 0.40f, 0.90f, 1f);
        [SerializeField] private Color _readyColor = new Color(0.30f, 0.85f, 1.00f, 1f);

        private sealed class Bar
        {
            public GameObject Root;
            public Image Fill;
            public TMP_Text Label;
        }

        private readonly List<Bar> _bars = new List<Bar>();
        private readonly HeroLookup _heroes = new HeroLookup();

        private void Update()
        {
            if (_container == null) return;

            var heroes = _heroes.Get(_bootstrap);
            int count = heroes != null ? heroes.Count : 0;

            while (_bars.Count < count) _bars.Add(CreateBar());

            for (int i = 0; i < _bars.Count; i++)
            {
                bool active = i < count;
                if (_bars[i].Root.activeSelf != active) _bars[i].Root.SetActive(active);
                if (!active) continue;

                var hero = heroes[i];
                var ultimate = hero != null ? hero.Ultimate : null;
                string heroName = hero != null && hero.Definition != null ? hero.Definition.HeroName : "Hero";

                if (ultimate == null)
                {
                    _bars[i].Fill.fillAmount = 0f;
                    _bars[i].Fill.color = _chargingColor;
                    _bars[i].Label.text = $"{heroName}: Ultimate";
                    continue;
                }

                float progress = Mathf.Clamp01(ultimate.CooldownProgress01);
                bool ready = ultimate.IsReady;
                _bars[i].Fill.fillAmount = progress;
                _bars[i].Fill.color = ready ? _readyColor : _chargingColor;
                _bars[i].Label.text = ready
                    ? $"{heroName}: ULTIMATE READY"
                    : $"{heroName}: Ultimate {Mathf.FloorToInt(progress * 100f)}%";
            }
        }

        private Bar CreateBar()
        {
            var rowGo = new GameObject("UltimateBar", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            rowGo.transform.SetParent(_container, false);
            ((RectTransform)rowGo.transform).sizeDelta = new Vector2(420f, 36f);
            rowGo.GetComponent<LayoutElement>().minHeight = 36f;
            var bg = rowGo.GetComponent<Image>();
            bg.sprite = _barSprite;
            bg.type = Image.Type.Sliced;
            bg.color = new Color(0f, 0f, 0f, 0.65f);

            var fillGo = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fillGo.transform.SetParent(rowGo.transform, false);
            var fillRt = (RectTransform)fillGo.transform;
            fillRt.anchorMin = Vector2.zero;
            fillRt.anchorMax = Vector2.one;
            fillRt.offsetMin = new Vector2(3f, 3f);
            fillRt.offsetMax = new Vector2(-3f, -3f);
            var fill = fillGo.GetComponent<Image>();
            fill.sprite = _barSprite;
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = (int)Image.OriginHorizontal.Left;
            fill.fillAmount = 0f;
            fill.color = _chargingColor;

            var labelGo = new GameObject("Label", typeof(RectTransform));
            labelGo.transform.SetParent(rowGo.transform, false);
            var label = labelGo.AddComponent<TextMeshProUGUI>();
            label.fontSize = 20f;
            label.color = Color.white;
            label.alignment = TextAlignmentOptions.Center;
            if (TMP_Settings.defaultFontAsset != null) label.font = TMP_Settings.defaultFontAsset;
            var lRt = label.rectTransform;
            lRt.anchorMin = Vector2.zero;
            lRt.anchorMax = Vector2.one;
            lRt.offsetMin = Vector2.zero;
            lRt.offsetMax = Vector2.zero;

            return new Bar { Root = rowGo, Fill = fill, Label = label };
        }
    }
}
