using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Wavekeep.UI
{
    /// <summary>
    /// A single shared floating tooltip (Task 25). A plain MonoBehaviour CREATED AT RUNTIME by
    /// <see cref="HubController"/> and injected into each <see cref="TooltipTrigger"/> — deliberately not
    /// a static <c>Instance</c> (CLAUDE.md §3.5). It builds its own background + text in <see cref="Awake"/>
    /// and follows the cursor while visible; triggers call <see cref="Show"/>/<see cref="Hide"/> on hover.
    /// </summary>
    [AddComponentMenu("Wavekeep/UI/Tooltip Presenter")]
    public sealed class TooltipPresenter : MonoBehaviour
    {
        private RectTransform _rect;
        private TextMeshProUGUI _text;

        private void Awake()
        {
            _rect = (RectTransform)transform;
            _rect.anchorMin = Vector2.zero;
            _rect.anchorMax = Vector2.zero;
            _rect.pivot = Vector2.zero;
            _rect.sizeDelta = new Vector2(320f, 96f);

            var bg = gameObject.AddComponent<Image>();
            bg.color = new Color(0.05f, 0.05f, 0.07f, 0.96f);
            bg.raycastTarget = false; // must never block the hover/click it is describing

            var textGo = new GameObject("Text", typeof(RectTransform));
            textGo.transform.SetParent(transform, false);
            _text = textGo.AddComponent<TextMeshProUGUI>();
            _text.fontSize = 16f;
            _text.color = Color.white;
            _text.alignment = TextAlignmentOptions.TopLeft;
            _text.raycastTarget = false; // word-wrap is on by default in TMP, no need to set it
            if (TMP_Settings.defaultFontAsset != null) _text.font = TMP_Settings.defaultFontAsset;
            var trt = _text.rectTransform;
            trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
            trt.offsetMin = new Vector2(10f, 8f); trt.offsetMax = new Vector2(-10f, -8f);

            gameObject.SetActive(false);
        }

        /// <summary>Show the tooltip with <paramref name="text"/>, snap it to the cursor, and raise it
        /// above all other UI so it is never occluded.</summary>
        public void Show(string text)
        {
            if (_text != null) _text.text = text;
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            FollowCursor();
        }

        public void Hide()
        {
            if (gameObject.activeSelf) gameObject.SetActive(false);
        }

        private void Update()
        {
            // Disabled while hidden (SetActive(false)), so this only runs while a tooltip is showing.
            FollowCursor();
        }

        private void FollowCursor()
        {
            var mouse = Mouse.current;
            if (mouse == null || _rect == null) return;

            Vector2 p = mouse.position.ReadValue();
            float w = _rect.sizeDelta.x;
            float h = _rect.sizeDelta.y;

            // Sit up-and-right of the cursor; flip to the left / below if it would leave the screen.
            float x = p.x + 16f;
            if (x + w > Screen.width) x = p.x - w - 16f;
            float y = p.y + 16f;
            if (y + h > Screen.height) y = p.y - h - 16f;

            _rect.position = new Vector3(x, y, 0f);
        }
    }
}
