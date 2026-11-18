using System;
using System.Collections.Generic;
using UnityEngine;

namespace DesktopMinesweeper
{
    [CreateAssetMenu(menuName = "Minesweeper/Fixed Board")]
    public sealed class BoardLayout : ScriptableObject
    {
        [SerializeField, Min(1)] private int width = 9;
        [SerializeField, Min(1)] private int height = 9;
        [SerializeField] private Vector2Int[] mines = Array.Empty<Vector2Int>();
        [SerializeField] private Vector2Int opening;
        [SerializeField, Min(16)] private int preferredCellSize = 32;

        public Vector2Int Opening => opening;
        public int PreferredCellSize => preferredCellSize;

        public BoardData CreateBoard(MinesweeperRules rules)
        {
            var board = new BoardData(width, height, mines.Length);
            var positions = new HashSet<Vector2Int>();
            foreach (Vector2Int mine in mines)
            {
                if (!positions.Add(mine))
                    throw new InvalidOperationException($"{name} contains duplicate mine {mine}.");
                board.GetCell(mine.x, mine.y).HasMine = true;
            }

            foreach (CellData cell in board.Cells)
                cell.AdjacentMines = rules.CalculateNumber(board, cell);

            CellData first = board.GetCell(opening.x, opening.y);
            if (first.HasMine)
                throw new InvalidOperationException($"{name} has a mine on its opening cell.");
            return board;
        }
    }
}
