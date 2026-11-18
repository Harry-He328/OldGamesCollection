using UnityEngine;
using UnityEngine.EventSystems;

namespace DesktopMinesweeper
{
    public sealed class DesktopAppShortcut : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField] private DesktopWindow window;
        [SerializeField] private bool opensPinball;

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left && eventData.clickCount == 2)
            {
                if (opensPinball) OldGames.Collection.DesktopPinballApp.OpenFromDesktop(transform.parent as RectTransform);
                else if (window != null) window.ShowWindow();
            }
        }
    }
}
