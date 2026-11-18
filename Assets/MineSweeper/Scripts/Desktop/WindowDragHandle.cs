using UnityEngine;
using UnityEngine.EventSystems;

namespace DesktopMinesweeper
{
    public sealed class WindowDragHandle : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        [SerializeField] private DesktopWindow window;
        [SerializeField] private Vector2Int resizeEdges;

        public void Configure(DesktopWindow owner, Vector2Int edges) { window = owner; resizeEdges = edges; }
        private Rect initialRect;
        private Vector2 start;
        private bool dragging;

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left) return;
            dragging = TryGetPosition(eventData.pressPosition, eventData, out start);
            if (dragging) initialRect = window.GetWindowRect();
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (dragging && TryGetPosition(eventData.position, eventData, out Vector2 point))
                window.Drag(initialRect, point - start, resizeEdges);
        }

        public void OnEndDrag(PointerEventData eventData) => dragging = false;

        private bool TryGetPosition(Vector2 screenPoint, PointerEventData eventData, out Vector2 point)
        {
            point = default;
            return window != null && RectTransformUtility.ScreenPointToLocalPointInRectangle(
                (RectTransform)window.transform.parent, screenPoint, eventData.pressEventCamera, out point);
        }
    }
}
