using System;
using System.Collections.Generic;

namespace DesktopMinesweeper
{
    public enum GameState { Ready, Playing, Won, Lost }
    public enum CellAction { Reveal, ToggleFlag, Chord }

    // Override individual rules for future variants without changing input or UI.
    public class MinesweeperRules
    {
        public virtual IEnumerable<CellData> GetNeighbors(BoardData board, CellData cell)
        {
            for (int y = Math.Max(0, cell.Y - 1); y <= Math.Min(board.Height - 1, cell.Y + 1); y++)
                for (int x = Math.Max(0, cell.X - 1); x <= Math.Min(board.Width - 1, cell.X + 1); x++)
                    if (x != cell.X || y != cell.Y)
                        yield return board.GetCell(x, y);
        }

        public virtual void PlaceMines(BoardData board, CellData firstCell, Random random)
        {
            var candidates = new List<CellData>();
            foreach (var cell in board.Cells)
                if (cell != firstCell)
                    candidates.Add(cell);

            // Partial shuffle guarantees a bounded number of draws even on dense boards.
            for (int i = 0; i < board.MineCount; i++)
            {
                int selected = random.Next(i, candidates.Count);
                var cell = candidates[selected];
                candidates[selected] = candidates[i];
                candidates[i] = cell;
                cell.HasMine = true;
            }
            foreach (var cell in board.Cells)
                cell.AdjacentMines = CalculateNumber(board, cell);
        }

        public virtual int CalculateNumber(BoardData board, CellData cell)
        {
            int count = 0;
            foreach (var neighbor in GetNeighbors(board, cell))
                if (neighbor.HasMine) count++;
            return count;
        }

        public virtual void ToggleFlag(BoardData board, CellData cell)
        {
            if (!cell.IsRevealed) cell.IsFlagged = !cell.IsFlagged;
        }

        public virtual void Reveal(BoardData board, CellData cell)
        {
            if (cell.IsFlagged || cell.IsRevealed) return;
            var pending = new Queue<CellData>();
            cell.IsRevealed = true;
            pending.Enqueue(cell);
            while (pending.Count > 0)
            {
                var current = pending.Dequeue();
                if (current.HasMine)
                {
                    OnMineRevealed(board, current);
                    continue;
                }
                if (current.AdjacentMines != 0) continue;
                foreach (var neighbor in GetNeighbors(board, current))
                {
                    if (neighbor.IsRevealed || neighbor.IsFlagged || neighbor.HasMine) continue;
                    neighbor.IsRevealed = true;
                    pending.Enqueue(neighbor);
                }
            }
        }

        public virtual void OnMineRevealed(BoardData board, CellData cell)
        {
            cell.IsDetonated = true;
        }

        protected virtual bool CountsForChord(CellData cell) => cell.IsFlagged;

        public virtual void Chord(BoardData board, CellData cell)
        {
            if (!cell.IsRevealed || cell.HasMine || cell.AdjacentMines == 0) return;
            int marked = 0;
            foreach (var neighbor in GetNeighbors(board, cell))
                if (CountsForChord(neighbor)) marked++;
            if (marked != cell.AdjacentMines)
            {
                // Easy flag: when every covered neighbour must be a mine, the number marks them all.
                int covered = 0;
                foreach (var neighbor in GetNeighbors(board, cell))
                    if (!neighbor.IsRevealed && !neighbor.IsFlagged) covered++;
                if (covered > 0 && marked + covered == cell.AdjacentMines)
                    foreach (var neighbor in GetNeighbors(board, cell))
                        if (!neighbor.IsRevealed && !neighbor.IsFlagged) ToggleFlag(board, neighbor);
                return;
            }
            foreach (var neighbor in GetNeighbors(board, cell))
            {
                Reveal(board, neighbor);
                if (Evaluate(board) == GameState.Lost) return;
            }
        }

        public virtual GameState Evaluate(BoardData board)
        {
            bool allSafeRevealed = true;
            foreach (var cell in board.Cells)
            {
                if (cell.IsDetonated) return GameState.Lost;
                if (!cell.HasMine && !cell.IsRevealed) allSafeRevealed = false;
            }
            return allSafeRevealed ? GameState.Won : GameState.Playing;
        }
    }
}
