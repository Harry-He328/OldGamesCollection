using UnityEngine;
using UnityEngine.UI;

namespace DesktopMinesweeper
{
    public sealed class MinesweeperLayout : MonoBehaviour
    {
        [SerializeField] private MinesweeperController controller;
        [SerializeField] private RectTransform boardArea;
        [SerializeField] private GridLayoutGroup grid;
        private Vector2 previousArea;
        private Vector2Int previousDimensions;

        private void OnEnable() => Canvas.willRenderCanvases += RefreshLayout;

        private void OnDisable() => Canvas.willRenderCanvases -= RefreshLayout;

        private void OnRectTransformDimensionsChange() => RefreshLayout();

        private void RefreshLayout()
        {
            if (controller == null || boardArea == null || grid == null || controller.Game == null) return;
            if (controller.BoardLayoutExternallyManaged) return;
            var board = controller.Game.Board;
            var dimensions = new Vector2Int(board.Width, board.Height);
            Vector2 available = boardArea.rect.size;
            if (available == previousArea && dimensions == previousDimensions) return;
            previousArea = available;
            previousDimensions = dimensions;

            // Integer cell sizes keep the pixel artwork aligned while resizing.
            float cellSize = Mathf.Max(1, Mathf.Floor(Mathf.Min(
                available.x / board.Width, available.y / board.Height)));
            grid.cellSize = new Vector2(cellSize, cellSize);
            ((RectTransform)grid.transform).sizeDelta =
                new Vector2(board.Width * cellSize, board.Height * cellSize);
        }
    }
}
