using System;

namespace DesktopMinesweeper
{
    // One instance per cell. State is written by gameplay rules, read by views.
    [Serializable]
    public sealed class CellData
    {
        public int X;
        public int Y;
        public bool HasMine;
        public bool IsRevealed;
        public bool IsFlagged;
        public bool IsDetonated;
        public int AdjacentMines;

        public CellData(int x, int y)
        {
            X = x;
            Y = y;
        }
    }
}
