using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Wavekeep.Core;
using Wavekeep.Data;

namespace Wavekeep.UI
{
    /// <summary>
    /// Task 57 (Part B) — the generic, hero-agnostic screen cast-overlay system. Lives on a full-screen
    /// RectTransform under a high-sort-order overlay Canvas and shows a brief full-screen flash on demand via
    /// <see cref="Trigger"/>. It knows nothing about any specific hero: callers pass a
    /// <see cref="ScreenCastOverlayConfig"/>, so adding a second hero's overlay needs only new config + a new
    /// trigger call — never a change here (Task 57 acceptance).
    ///
    /// Each trigger animates its OWN pooled <see cref="Image"/> (fade-in → hold → fade-out up to MaxOpacity), so
    /// overlapping casts coexist and stack subtly rather than restarting one another. Images are pooled (reused),
    /// never blocking input (raycastTarget off), and animated on unscaled time so the cosmetic flash is unaffected
    /// by any timescale changes. UI scaling comes from the Canvas/CanvasScaler it sits under (CLAUDE.md §3.6).
    /// </summary>
    [AddComponentMenu("Wavekeep/UI/Screen Cast Overlay Controller")]
    public sealed class ScreenCastOverlayController : MonoBehaviour, IScreenCastOverlay
    {
        // Reusable full-screen images, one per concurrently-animating overlay (grows to the peak overlap count,
        // which is tiny — at most one per hero). Inactive entries are recycled by Trigger.
        private readonly List<Image> _pool = new List<Image>();

        /// <inheritdoc/>
        public void Trigger(ScreenCastOverlayConfig config)
        {
            if (config == null || !config.IsActive) return;
            if (!isActiveAndEnabled) return; // controller/canvas not live (e.g. scene tearing down)

            var image = GetOrCreateImage();
            image.sprite = config.Sprite;             // null is fine — renders a solid tinted quad
            SetAlpha(image, config.Tint, 0f);
            image.transform.SetAsLastSibling();       // newest overlay draws on top of any still fading
            image.gameObject.SetActive(true);
            StartCoroutine(Animate(image, config));
        }

        private IEnumerator Animate(Image image, ScreenCastOverlayConfig config)
        {
            float maxAlpha = Mathf.Clamp01(config.MaxOpacity);
            var tint = config.Tint;

            yield return Fade(image, tint, 0f, maxAlpha, config.FadeInDuration);

            if (config.HoldDuration > 0f)
            {
                SetAlpha(image, tint, maxAlpha);
                yield return new WaitForSecondsRealtime(config.HoldDuration);
            }

            yield return Fade(image, tint, maxAlpha, 0f, config.FadeOutDuration);

            image.gameObject.SetActive(false); // return to the pool
        }

        private static IEnumerator Fade(Image image, Color tint, float from, float to, float duration)
        {
            if (duration <= 0f)
            {
                SetAlpha(image, tint, to);
                yield break;
            }

            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                SetAlpha(image, tint, Mathf.Lerp(from, to, Mathf.Clamp01(t / duration)));
                yield return null;
            }
            SetAlpha(image, tint, to);
        }

        private static void SetAlpha(Image image, Color tint, float alpha)
        {
            tint.a = alpha;
            image.color = tint;
        }

        private Image GetOrCreateImage()
        {
            for (int i = 0; i < _pool.Count; i++)
                if (_pool[i] != null && !_pool[i].gameObject.activeSelf) return _pool[i];

            var go = new GameObject($"CastOverlay_{_pool.Count}", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(transform, false);

            var rect = (RectTransform)go.transform;
            rect.anchorMin = Vector2.zero;     // stretch to fill the overlay canvas
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var image = go.GetComponent<Image>();
            image.raycastTarget = false;        // never intercept touches/clicks meant for gameplay/UI
            go.SetActive(false);

            _pool.Add(image);
            return image;
        }
    }
}
