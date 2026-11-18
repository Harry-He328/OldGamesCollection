using UnityEngine;
using UnityEngine.EventSystems;
namespace OldGames.Collection
{
    public sealed class DesktopPinballEntry : MonoBehaviour, IPointerClickHandler
    {
        public void OnPointerClick(PointerEventData e)
        {
            if(e.button==PointerEventData.InputButton.Left && e.clickCount==2)
                DesktopPinballApp.OpenFromDesktop(transform.parent as RectTransform);
        }
    }
}
