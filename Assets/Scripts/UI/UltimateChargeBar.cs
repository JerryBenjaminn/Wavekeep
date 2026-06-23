using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Wavekeep.Runtime;

namespace Wavekeep.UI
{
    /// <summary>
    /// Placeholder-tier HUD bar showing the active hero's ultimate charge (Task 21). It reads progress
    /// DIRECTLY from the ultimate's <c>IAbility.CooldownProgress01</c> / <c>IsReady</c> each frame — it
    /// owns no cooldown state of its own, so it can never disagree with the real ability (a
    /// reviewer-blocking requirement).
    ///
    /// The hero is spawned at runtime by the hero-select flow, so this lazily finds the
    /// <see cref="HeroRuntime"/> once it exists (no static singleton, no scene wiring of the hero). The
    /// fill image and label are built/wired by the Task 21 editor setup script.
    /// </summary>
    [AddComponentMenu("Wavekeep/UI/Ultimate Charge Bar")]
    public sealed class UltimateChargeBar : MonoBehaviour
    {
        [Header("Wiring")]
        [Tooltip("Filled Image whose fillAmount is driven 0→1 by ultimate charge progress.")]
        [SerializeField] private Image _fillImage;
        [Tooltip("Label showing 'charging %' or the ready text.")]
        [SerializeField] private TMP_Text _label;

        [Header("Appearance")]
        [SerializeField] private Color _chargingColor = new Color(0.20f, 0.40f, 0.90f, 1f);
        [SerializeField] private Color _readyColor = new Color(0.30f, 0.85f, 1.00f, 1f);
        [SerializeField] private string _readyText = "ULTIMATE READY (U)";
        [SerializeField] private string _chargingLabel = "Ultimate";

        private HeroRuntime _hero;

        private void Update()
        {
            // Lazily acquire the runtime-spawned hero; cheap no-op once found.
            if (_hero == null)
            {
                _hero = Object.FindFirstObjectByType<HeroRuntime>();
            }

            var ultimate = _hero != null ? _hero.Ultimate : null;
            if (ultimate == null)
            {
                Apply(0f, false); // no hero/ultimate yet — show an empty, not-ready bar
                return;
            }

            Apply(ultimate.CooldownProgress01, ultimate.IsReady);
        }

        private void Apply(float progress, bool ready)
        {
            if (_fillImage != null)
            {
                _fillImage.fillAmount = Mathf.Clamp01(progress);
                _fillImage.color = ready ? _readyColor : _chargingColor;
            }

            if (_label != null)
            {
                _label.text = ready ? _readyText : $"{_chargingLabel} {Mathf.FloorToInt(progress * 100f)}%";
            }
        }
    }
}
