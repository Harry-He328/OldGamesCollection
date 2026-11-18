using DesktopMinesweeper;
using TMPro;
using UnityEngine;

namespace OldGames.Collection
{
    public sealed class DesktopNotepadWindow : MonoBehaviour
    {
        [SerializeField] private DesktopWindow shell;
        [SerializeField] private TMP_InputField input;
        public TMP_InputField Input => input;

        public void Configure(DesktopWindow window, TMP_InputField field)
        {
            shell = window;
            input = field;
        }

        public void Open()
        {
            shell.ShowWindow();
            input.ActivateInputField();
        }

        public void Close() => shell.CloseWindow();
    }
}
