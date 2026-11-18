using System;
using System.Collections.Generic;
using UnityEngine;

namespace DesktopMinesweeper
{
    // The deliberately inconsistent clue and its repair belong only to level 2.
    public sealed class HiddenBoardPuzzle : MinesweeperRules
    {
        private BoardData board;
        private bool deleted;
        public Vector2Int CorruptedCell { get; private set; }
        public bool Repaired { get; private set; }

        public BoardData CreateBoard(HiddenBoardLayout layout, int seed)
        {
            board = new BoardData(layout.Width, layout.Height, layout.MineCount);
            CorruptedCell = new Vector2Int(board.Width - 1, board.Height - 2);
            var mine = board.GetCell(board.Width - 2, board.Height - 1);
            mine.HasMine = true;
            mine.IsFlagged = true;
            var candidates = new List<CellData>();
            int openingWidth = Math.Min(layout.ViewportCells.x, board.Width - 4);
            int openingHeight = Math.Min(layout.ViewportCells.y, board.Height - 4);
            foreach (CellData cell in board.Cells)
            {
                bool puzzleArea = cell.X >= board.Width - 4 && cell.Y >= board.Height - 4;
                bool openingArea = cell.X < openingWidth && cell.Y < openingHeight;
                if (!puzzleArea && !openingArea) candidates.Add(cell);
            }
            if (board.MineCount - 1 > candidates.Count)
                throw new InvalidOperationException("Hidden board has too many mines for its protected opening and paradox area.");
            var random = new System.Random(seed);
            for (int i = 0; i < board.MineCount - 1; i++)
            {
                int chosen = random.Next(i, candidates.Count);
                CellData cell = candidates[chosen];
                candidates[chosen] = candidates[i];
                candidates[i] = cell;
                cell.HasMine = true;
            }
            foreach (CellData cell in board.Cells) cell.AdjacentMines = CalculateNumber(board, cell);
            // Both clues have the same remaining two neighbors: a flagged mine and a safe covered cell.
            for (int y = board.Height - 3; y < board.Height; y++)
                for (int x = board.Width - 3; x < board.Width; x++)
                {
                    CellData cell = board.GetCell(x, y);
                    if (!cell.HasMine && !(x == board.Width - 1 && y == board.Height - 1)) cell.IsRevealed = true;
                }
            board.GetCell(CorruptedCell.x, CorruptedCell.y).AdjacentMines = 2;
            deleted = Repaired = false;
            return board;
        }

        public bool EditClue(string text)
        {
            if (Repaired || board == null) return false;
            if (text == "") { deleted = true; return false; }
            if (!deleted || text != "1") return false;
            board.GetCell(CorruptedCell.x, CorruptedCell.y).AdjacentMines = 1;
            Repaired = true;
            return true;
        }

        public override GameState Evaluate(BoardData current)
        {
            if (base.Evaluate(current) == GameState.Lost) return GameState.Lost;
            return Repaired ? GameState.Won : GameState.Playing;
        }
    }
}
