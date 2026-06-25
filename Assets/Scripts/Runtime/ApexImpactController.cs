using UnityEngine;
using UnityEngine.UI;

namespace Wavekeep.Runtime
{
    /// <summary>
    /// Task 47: the shared "weight" treatment for apex/combo-apex triggers — a brief camera shake plus a brief
    /// full-screen colour flash. Lives on the main camera (one instance, found/added by the presenters), so
    /// every apex routes through the SAME effect for a consistent feel.
    ///
    /// Deliberately NO time-dilation hit-stop: this project drives cooldowns/ability timers off
    /// <c>Time.deltaTime</c> (AbilityRuntime/HeroRuntime) and pauses via a reference-counted PauseState rather
    /// than <c>Time.timeScale</c>. Scaling <c>Time.timeScale</c> for hit-stop would therefore reduce cooldown
    /// timing accuracy during the dip — exactly the reviewer-blocking concern in the task — so per the task's
    /// "propose a screen-flash-only alternative if so" guidance, the weight is conveyed by shake + flash only.
    /// Both are driven by <see cref="Time.unscaledDeltaTime"/> so they're independent of any pause/slow.
    ///
    /// Re-triggers take the MAX of the current and requested values (they don't stack/accumulate), so rapid
    /// apexes can't build into a disorienting, ever-growing shake.
    /// </summary>
    [AddComponentMenu("Wavekeep/Runtime/Apex Impact Controller")]
    public sealed class ApexImpactController : MonoBehaviour
    {
        private Transform _camTransform;
        private Vector3 _baseLocalPos;

        private float _shakeAmp;
        private float _shakeTimer;
        private float _shakeDuration;

        private Image _flash;
        private float _flashTimer;
        private float _flashDuration;
        private float _flashPeak;
        private Color _flashColor = Color.white;

        /// <summary>Get the single controller on the main camera, adding it (and its flash overlay) on first use.
        /// Returns null only if there is no main camera (headless/tests) — callers treat that as a no-op.</summary>
        public static ApexImpactController GetOrCreate()
        {
            var cam = Camera.main;
            if (cam == null) return null;
            return cam.TryGetComponent(out ApexImpactController existing)
                ? existing
                : cam.gameObject.AddComponent<ApexImpactController>();
        }

        private void Awake()
        {
            _camTransform = transform;
            _baseLocalPos = _camTransform.localPosition;
            BuildFlashOverlay();
        }

        private void BuildFlashOverlay()
        {
            var canvasGo = new GameObject("ApexFlashCanvas");
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 5000; // above gameplay HUD so the flash reads over everything

            var imgGo = new GameObject("Flash");
            imgGo.transform.SetParent(canvasGo.transform, false);
            _flash = imgGo.AddComponent<Image>();
            _flash.raycastTarget = false; // never intercept input
            var rt = _flash.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            _flash.color = new Color(1f, 1f, 1f, 0f);
        }

        /// <summary>Trigger the weight treatment. Values are taken as a MAX against any in-flight impact.</summary>
        public void Impact(float shakeAmp, float shakeDuration, Color flashColor, float flashPeak, float flashDuration)
        {
            if (shakeDuration > 0f && shakeAmp > 0f)
            {
                _shakeAmp = Mathf.Max(_shakeAmp, shakeAmp);
                _shakeDuration = Mathf.Max(_shakeDuration, shakeDuration);
                _shakeTimer = Mathf.Max(_shakeTimer, shakeDuration);
            }

            if (flashDuration > 0f && flashPeak > 0f)
            {
                // Newer flash wins the colour; intensity/lifetime take the max so it never dims an active flash.
                _flashColor = flashColor;
                _flashPeak = Mathf.Max(_flashPeak, flashPeak);
                _flashDuration = Mathf.Max(_flashDuration, flashDuration);
                _flashTimer = Mathf.Max(_flashTimer, flashDuration);
            }
        }

        private void LateUpdate()
        {
            float dt = Time.unscaledDeltaTime;

            if (_shakeTimer > 0f)
            {
                _shakeTimer -= dt;
                float k = _shakeDuration > 0f ? Mathf.Clamp01(_shakeTimer / _shakeDuration) : 0f;
                if (_shakeTimer > 0f)
                {
                    var offset = Random.insideUnitSphere * (_shakeAmp * k);
                    offset.z = 0f; // keep the fixed 3/4 framing's depth stable
                    _camTransform.localPosition = _baseLocalPos + offset;
                }
                else
                {
                    _camTransform.localPosition = _baseLocalPos;
                }
            }

            if (_flash != null && _flashTimer > 0f)
            {
                _flashTimer -= dt;
                float k = _flashDuration > 0f ? Mathf.Clamp01(_flashTimer / _flashDuration) : 0f;
                var c = _flashColor;
                c.a = _flashPeak * k;
                _flash.color = c;
            }
        }

        private void OnDisable()
        {
            // Defensively restore the camera if disabled mid-shake (e.g. scene unload).
            if (_camTransform != null) _camTransform.localPosition = _baseLocalPos;
        }
    }
}
