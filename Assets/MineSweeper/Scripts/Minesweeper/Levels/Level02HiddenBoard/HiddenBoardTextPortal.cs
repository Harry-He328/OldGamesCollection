using OldGames.Collection;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DesktopMinesweeper
{
    // This component is created and removed by level 2; Notepad itself has no puzzle behavior.
    public sealed class HiddenBoardTextPortal : MonoBehaviour
    {
        private MinesweeperGame game;
        private HiddenBoardPuzzle puzzle;
        private DesktopNotepadWindow notepad;
        private RectTransform viewport;
        private TMP_Text[] cells;
        private TMP_InputField editor;
        private CanvasGroup editorGroup;
        private bool visible, normalInputEnabled, focusPending, configured;
        private Selectable.Transition normalInputTransition;
        private float cellSize;
        public RectTransform Board { get; private set; }
        public TMP_InputField ClueEditor => editor;

        public void Configure(MinesweeperGame current, HiddenBoardPuzzle rule, DesktopNotepadWindow app,
            RectTransform clippingArea, float size, TMP_SpriteAsset emoji)
        {
            game = current;
            puzzle = rule;
            notepad = app;
            viewport = clippingArea;
            cellSize = size;
            Board = MakeRect("Character board", (RectTransform)transform);
            Board.anchorMin = Board.anchorMax = new Vector2(.5f, .5f);
            Board.pivot = Vector2.up;
            Board.sizeDelta = new Vector2(game.Board.Width * size, game.Board.Height * size);
            cells = new TMP_Text[game.Board.Width * game.Board.Height];
            foreach (CellData cell in game.Board.Cells)
            {
                var rect = MakeRect($"Character {cell.X},{cell.Y}", Board);
                PlaceCell(rect, cell.X, cell.Y);
                var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
                label.font = notepad.Input.textComponent.font;
                label.spriteAsset = emoji;
                label.fontSize = size * .8f;
                label.alignment = TextAlignmentOptions.Center;
                label.textWrappingMode = TextWrappingModes.NoWrap;
                label.overflowMode = TextOverflowModes.Overflow;
                label.color = new Color32(30, 36, 44, 255);
                label.raycastTarget = false;
                cells[cell.Y * game.Board.Width + cell.X] = label;
            }
            if (puzzle != null) CreateEditor();
            game.Changed += Refresh;
            configured = true;
            Refresh();
        }

        private void CreateEditor()
        {
            var rect = MakeRect("Editable clue", Board);
            PlaceCell(rect, puzzle.CorruptedCell.x, puzzle.CorruptedCell.y);
            editorGroup = rect.gameObject.AddComponent<CanvasGroup>();
            // The observation parent passes clicks through; this one editable cell is the sole exception.
            editorGroup.ignoreParentGroups = true;
            var background = rect.gameObject.AddComponent<Image>();
            background.color = new Color32(255, 251, 225, 255);
            var clip = MakeRect("Text Area", rect);
            clip.gameObject.AddComponent<RectMask2D>();
            var text = MakeRect("Text", clip).gameObject.AddComponent<TextMeshProUGUI>();
            text.font = notepad.Input.textComponent.font;
            text.fontSize = cellSize * .8f;
            text.color = new Color32(30, 36, 44, 255);
            text.alignment = TextAlignmentOptions.Center;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.raycastTarget = false;
            editor = rect.gameObject.AddComponent<TMP_InputField>();
            editor.textViewport = clip;
            editor.textComponent = text;
            editor.targetGraphic = background;
            editor.characterLimit = 1;
            editor.characterValidation = TMP_InputField.CharacterValidation.Digit;
            editor.lineType = TMP_InputField.LineType.SingleLine;
            editor.richText = false;
            editor.onFocusSelectAll = false;
            editor.customCaretColor = true;
            editor.caretColor = Color.black;
            editor.caretWidth = 2;
            editor.SetTextWithoutNotify("2");
            editor.onValueChanged.AddListener(EditClue);
            rect.gameObject.SetActive(false);
        }

        private void EditClue(string text)
        {
            if (!visible || game.State == GameState.Lost || game.State == GameState.Won) return;
            if (puzzle.EditClue(text)) game.ReevaluateRules();
        }

        public void Refresh()
        {
            if (cells == null) return;
            foreach (CellData cell in game.Board.Cells)
            {
                TMP_Text label = cells[cell.Y * game.Board.Width + cell.X];
                bool mine = cell.HasMine && (cell.IsRevealed || game.State == GameState.Lost);
                label.text = mine ? "<sprite name=\"bomb\">" : cell.IsFlagged ? "<sprite name=\"flag\">" :
                    !cell.IsRevealed ? "#" : cell.AdjacentMines == 0 ? "·" : cell.AdjacentMines.ToString();
            }
        }

        private void LateUpdate()
        {
            if (!configured || editor == null) return;
            bool canEdit = !puzzle.Repaired && game.State != GameState.Lost && game.State != GameState.Won && FullyVisible();
            if (canEdit != visible)
            {
                visible = canEdit;
                editor.gameObject.SetActive(visible);
                cells[puzzle.CorruptedCell.y * game.Board.Width + puzzle.CorruptedCell.x].enabled = !visible;
                if (visible)
                {
                    normalInputEnabled = notepad.Input.interactable;
                    normalInputTransition = notepad.Input.transition;
                    notepad.Input.DeactivateInputField();
                    notepad.Input.transition = Selectable.Transition.None;
                    notepad.Input.interactable = false;
                    focusPending = true;
                }
                else ReleaseInput();
            }
            if (focusPending && visible)
            {
                focusPending = false;
                if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(editor.gameObject);
                editor.ActivateInputField();
                editor.caretPosition = editor.text.Length;
            }
        }

        private bool FullyVisible()
        {
            var rect = (RectTransform)editor.transform;
            Vector3[] corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            for (int i = 0; i < corners.Length; i++)
                if (!viewport.rect.Contains((Vector2)viewport.InverseTransformPoint(corners[i]))) return false;
            var window = notepad.GetComponent<DesktopWindow>();
            foreach (DesktopWindow other in window.transform.parent.GetComponentsInChildren<DesktopWindow>())
            {
                if (other == window || other.transform.GetSiblingIndex() <= window.transform.GetSiblingIndex()) continue;
                var otherRect = (RectTransform)other.transform;
                if (otherRect.rect.Contains((Vector2)otherRect.InverseTransformPoint(rect.position))) return false;
            }
            return true;
        }

        private void ReleaseInput()
        {
            if (editor != null) editor.DeactivateInputField();
            if (notepad != null && notepad.Input != null && (visible || !notepad.Input.interactable))
            {
                notepad.Input.transition = normalInputTransition;
                notepad.Input.interactable = normalInputEnabled;
            }
            focusPending = false;
        }

        private void OnDisable()
        {
            if (visible) ReleaseInput();
            visible = false;
            if (editor != null) editor.gameObject.SetActive(false);
            if (puzzle != null && cells != null)
                cells[puzzle.CorruptedCell.y * game.Board.Width + puzzle.CorruptedCell.x].enabled = true;
        }

        private void OnDestroy()
        {
            if (game != null) game.Changed -= Refresh;
        }

        private void PlaceCell(RectTransform rect, int x, int y)
        {
            rect.anchorMin = rect.anchorMax = Vector2.up;
            rect.pivot = new Vector2(.5f, .5f);
            rect.sizeDelta = new Vector2(cellSize, cellSize);
            rect.anchoredPosition = new Vector2((x + .5f) * cellSize, -(y + .5f) * cellSize);
        }

        private static RectTransform MakeRect(string name, RectTransform parent)
        {
            var rect = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            return rect;
        }
    }
}
