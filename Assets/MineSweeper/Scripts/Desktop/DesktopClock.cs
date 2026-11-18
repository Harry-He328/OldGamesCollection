using TMPro;
using UnityEngine;

namespace DesktopMinesweeper
{
    [RequireComponent(typeof(TMP_Text))]
    public sealed class DesktopClock : MonoBehaviour
    {
        private void OnEnable() => GetComponent<TMP_Text>().text = "03:25";
    }
}
