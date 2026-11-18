using System;
using UnityEngine;

namespace DesktopMinesweeper
{
    public partial class MinesweeperController
    {
        /// <summary>Raised after a fresh level 2 board is on screen (also on restart).</summary>
        public event Action HiddenBoardStarted;
        /// <summary>Raised when level 2 is torn down, including before a restart.</summary>
        public event Action HiddenBoardEnded;

        private HiddenBoardLevel hiddenBoard;
        public bool BoardLayoutExternallyManaged => hiddenBoard != null || stage == Stage.HiddenBoard;
        public bool InHiddenBoard => stage == Stage.HiddenBoard;

        private void StartHiddenBoard()
        {
            EndHiddenBoard();
            stage = Stage.HiddenBoard;
            platform = null;
            battle = null;
            var layout = Resources.Load<HiddenBoardLayout>("Minesweeper/HiddenBoard");
            bool temporary = layout == null;
            if (temporary) layout = ScriptableObject.CreateInstance<HiddenBoardLayout>();
            var puzzle = new HiddenBoardPuzzle();
            BoardData board = puzzle.CreateBoard(layout, System.Environment.TickCount);
            LoadBoard(board, puzzle, true);
            hiddenBoard = new HiddenBoardLevel(Game, window, grid, layout, puzzle);
            if (temporary) Destroy(layout);
            RefreshHud();
            HiddenBoardStarted?.Invoke();
        }

        private void EndHiddenBoard()
        {
            bool wasActive = hiddenBoard != null;
            hiddenBoard?.Dispose();
            hiddenBoard = null;
            if (wasActive) HiddenBoardEnded?.Invoke();
        }
    }
}
