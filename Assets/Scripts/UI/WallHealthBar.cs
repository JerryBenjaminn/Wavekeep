using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Wavekeep.Runtime;

namespace Wavekeep.UI
{
    /// <summary>
    /// HUD bar showing the defended wall's remaining HP — the run's lose condition (wall HP 0 = defeat,
    /// CLAUDE.md §2). Polls the scene <see cref="WallRuntime"/>'s <c>CurrentHP</c>/<c>MaxHP</c> every frame
    /// and drives a horizontally-filled <see cref="Image"/> plus an optional numeric label, owning NO HP
    /// state of its own — so it can never disagree with the real wall (the same direct-read approach
    /// <see cref="UltimateChargeBar"/> uses for ability state). The fill colour lerps from healthy to
    /// critical as HP drops, so a failing wall reads at a glance.
    ///
    /// The wall reference is wired by the Task 64 setup; if left null it falls back to a runtime scene scan
    /// (the wall is a scene object, so the scan resolves once and is cached).
    /// </summary>
    [AddComponentMenu("Wavekeep/UI/Wall Health Bar")]
    public sealed class WallHealthBar : MonoBehaviour
    {
        [Header("Wiring")]
        [Tooltip("The defended wall to read HP from. If unset, found via a scene scan at runtime.")]
        [SerializeField] private WallRuntime _wall;
        [Tooltip("Horizontally-filled Image whose fillAmount tracks CurrentHP/MaxHP.")]
        [SerializeField] private Image _fill;
        [Tooltip("Optional label showing numeric HP (e.g. '240 / 300'). Leave null to hide the text.")]
        [SerializeField] private TMP_Text _label;

        [Header("Appearance")]
        [Tooltip("Fill colour at full HP.")]
        [SerializeField] private Color _healthyColor = new Color(0.30f, 0.80f, 0.30f, 1f);
        [Tooltip("Fill colour at zero HP (the fill lerps toward this as HP drops).")]
        [SerializeField] private Color _criticalColor = new Color(0.85f, 0.20f, 0.15f, 1f);

        private void Update()
        {
            if (_fill == null) return;
            if (_wall == null) _wall = Object.FindFirstObjectByType<WallRuntime>();
            if (_wall == null || _wall.MaxHP <= 0f) return;

            float ratio = Mathf.Clamp01(_wall.CurrentHP / _wall.MaxHP);
            _fill.fillAmount = ratio;
            _fill.color = Color.Lerp(_criticalColor, _healthyColor, ratio);

            if (_label != null)
                _label.text = $"{Mathf.CeilToInt(_wall.CurrentHP)} / {Mathf.CeilToInt(_wall.MaxHP)}";
        }
    }
}
