using DesktopMinesweeper;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
namespace OldGames.Collection
{
    // Authored in the desktop scene, using the same shell and controls as Minesweeper.
    public sealed class DesktopPinballWindow : MonoBehaviour
    {
        public DesktopWindow shell;
        public RawImage viewport;
        public TMP_Text scoreLabel;
        public GameObject taskButton;
        public void NewGame()
        {
            DesktopPinballApp.OpenFromDesktop(transform.parent as RectTransform);
            var app=FindFirstObjectByType<DesktopPinballApp>();
            if(app!=null)app.RestartGame();
        }
        public void Open() => DesktopPinballApp.OpenFromDesktop(transform.parent as RectTransform);
        public void Quit() { if(shell!=null)shell.CloseWindow(); }
    }
}
