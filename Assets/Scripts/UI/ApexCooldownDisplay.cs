using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Wavekeep.Abilities;
using Wavekeep.Runtime;

namespace Wavekeep.UI
{
    /// <summary>
    /// Task 32: a HUD that shows one cooldown bar per UNLOCKED apex talent, reusing the ultimate charge
    /// bar's fill/empty convention (Task 21). It lazily finds the runtime-spawned <see cref="HeroRuntime"/>
    /// (no static singleton) and reads each apex's live <c>CooldownProgress01</c>/<c>IsReady</c> directly
    /// from its runtime <c>IAbility</c> — never a duplicated timer.
    ///
    /// Bars are generated on demand: none exist before any apex unlocks (so nothing shows pre-unlock), and
    /// the layout stacks naturally, supporting two or more simultaneous apexes. The bar fill sprite is
    /// injected by the editor setup (the built-in UISprite) so runtime-built bars render the same as the
    /// ultimate bar.
    /// </summary>
    [AddComponentMenu("Wavekeep/UI/Apex Cooldown Display")]
    public sealed class ApexCooldownDisplay : MonoBehaviour
    {
        [Tooltip("Parent (with a vertical layout) the per-apex bars are generated into.")]
        [SerializeField] private RectTransform _container;
        [Tooltip("Sprite used for the bar background + fill (wire the built-in UI/Skin/UISprite).")]
        [SerializeField] private Sprite _barSprite;

        [Header("Appearance (mirrors the ultimate bar)")]
        [SerializeField] private Color _chargingColor = new Color(0.85f, 0.55f, 0.20f, 1f);
        [SerializeField] private Color _readyColor = new Color(1f, 0.82f, 0.35f, 1f);

        private sealed class Bar
        {
            public GameObject Root;
            public Image Fill;
            public TMP_Text Label;
        }

        private readonly List<Bar> _bars = new List<Bar>();
        private HeroRuntime _hero;

        private void Update()
        {
            if (_container == null) return;
            if (_hero == null) _hero = Object.FindFirstObjectByType<HeroRuntime>();

            var apexes = _hero != null ? _hero.ApexAbilities : null;
            int count = apexes != null ? apexes.Count : 0;

            // Grow the bar pool to match the number of unlocked apexes (only ever grows during a run).
            while (_bars.Count < count) _bars.Add(CreateBar());

            for (int i = 0; i < _bars.Count; i++)
            {
                bool active = i < count;
                if (_bars[i].Root.activeSelf != active) _bars[i].Root.SetActive(active);
                if (!active) continue;

                var apex = apexes[i];
                float progress = Mathf.Clamp01(apex.CooldownProgress01);
                bool ready = apex.IsReady;

                _bars[i].Fill.fillAmount = progress;
                _bars[i].Fill.color = ready ? _readyColor : _chargingColor;

                string label = apex.Definition != null ? apex.Definition.AbilityName : "Apex";
                _bars[i].Label.text = ready ? $"{label} — READY" : $"{label} {Mathf.FloorToInt(progress * 100f)}%";
            }
        }

        private Bar CreateBar()
        {
            var rowGo = new GameObject("ApexBar", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            rowGo.transform.SetParent(_container, false);
            ((RectTransform)rowGo.transform).sizeDelta = new Vector2(360f, 30f);
            rowGo.GetComponent<LayoutElement>().minHeight = 30f;
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
            label.fontSize = 16f;
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
