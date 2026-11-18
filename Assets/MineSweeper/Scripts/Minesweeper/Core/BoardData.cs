using System;
using System.Collections.Generic;

namespace DesktopMinesweeper
{
    public sealed class BoardData
    {
        private readonly CellData[] cells;
        public int Width { get; }
        public int Height { get; }
        public int MineCount { get; }
        public IReadOnlyList<CellData> Cells { get; }

        public BoardData(int width, int height, int mineCount)
        {
            if (width < 1 || height < 1 || (long)width * height > int.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(width));
            if (mineCount < 0 || mineCount >= width * height)
                throw new ArgumentOutOfRangeException(nameof(mineCount));

            Width = width;
            Height = height;
            MineCount = mineCount;
            cells = new CellData[width * height];
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                    cells[y * width + x] = new CellData(x, y);
            Cells = Array.AsReadOnly(cells);
        }

        public CellData GetCell(int x, int y)
        {
            if (x < 0 || x >= Width || y < 0 || y >= Height)
                throw new ArgumentOutOfRangeException(nameof(x));
            return cells[y * Width + x];
        }
    }
}
