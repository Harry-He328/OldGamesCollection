using UnityEngine;

namespace DesktopMinesweeper
{
    [CreateAssetMenu(menuName = "Minesweeper/Levels/Hidden Board")]
    public sealed class HiddenBoardLayout : ScriptableObject
    {
        [SerializeField, Min(9)] private int width = 30;
        [SerializeField, Min(9)] private int height = 16;
        [SerializeField, Min(1)] private int mineCount = 20;
        [SerializeField, Min(16)] private int cellSize = 24;
        [SerializeField] private Vector2Int viewportCells = new Vector2Int(9, 9);

        public int Width => width;
        public int Height => height;
        public int MineCount => Mathf.Clamp(mineCount, 1, width * height - 1);
        public int CellSize => cellSize;
        public Vector2Int ViewportCells => viewportCells;
    }
}
