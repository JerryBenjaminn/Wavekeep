using UnityEngine;
using UnityEngine.EventSystems;

namespace Wavekeep.UI
{
    /// <summary>
    /// Shows a shared <see cref="TooltipPresenter"/> while the pointer hovers this UI element (Task 25).
    /// The presenter + text are INJECTED by whoever builds the row (HubController) via
    /// <see cref="Configure"/> — no static lookup, no per-trigger tooltip data baked into a prefab.
    /// Requires a raycast-target Graphic on (or under) the same object for the pointer events to fire.
    /// </summary>
    [AddComponentMenu("Wavekeep/UI/Tooltip Trigger")]
    public sealed class TooltipTrigger : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        private TooltipPresenter _presenter;
        private string _text;

        public void Configure(TooltipPresenter presenter, string text)
        {
            _presenter = presenter;
            _text = text;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (_presenter != null && !string.IsNullOrEmpty(_text)) _presenter.Show(_text);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (_presenter != null) _presenter.Hide();
        }

        private void OnDisable()
        {
            // Safety: if the row is torn down (RefreshInventory/RefreshSlots rebuilds) while hovered,
            // the exit event may not fire — make sure the shared tooltip doesn't linger.
            if (_presenter != null) _presenter.Hide();
        }
    }
}
