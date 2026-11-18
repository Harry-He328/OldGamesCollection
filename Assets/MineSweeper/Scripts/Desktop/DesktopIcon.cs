using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DesktopMinesweeper
{
    [RequireComponent(typeof(Image))]
    public sealed class DesktopIcon : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler,
        IPointerDownHandler, IPointerUpHandler, ISelectHandler, IDeselectHandler
    {
        [SerializeField] private Color hoverColor = new Color(0.65f, 0.85f, 1f, 0.25f);
        [SerializeField] private Color selectedColor = new Color(0.65f, 0.85f, 1f, 0.45f);
        private bool hovered;
        private bool selected;
        private bool pressed;

        private void OnEnable() => Refresh();
        public void OnPointerEnter(PointerEventData e) { hovered = true; Refresh(); }
        public void OnPointerExit(PointerEventData e) { hovered = false; Refresh(); }
        public void OnPointerDown(PointerEventData e)
        {
            if (e.button != PointerEventData.InputButton.Left) return;
            e.selectedObject = gameObject;
            pressed = true;
            Refresh();
        }
        public void OnPointerUp(PointerEventData e) { pressed = false; Refresh(); }
        public void OnSelect(BaseEventData e) { selected = true; Refresh(); }
        public void OnDeselect(BaseEventData e) { selected = false; Refresh(); }
        private void OnDisable() { hovered = selected = pressed = false; }

        private void Refresh()
        {
            GetComponent<Image>().color = pressed ? selectedColor * new Color(0.8f, 0.8f, 0.8f, 1f)
                : selected ? selectedColor : hovered ? hoverColor : Color.clear;
        }
    }
}
