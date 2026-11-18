using System;
using System.Collections.Generic;
using OldGames.Collection;
using UnityEngine;
using UnityEngine.UI;

namespace DesktopMinesweeper
{
    // Owns all observation effects. Applications only supply their ordinary content rectangles.
    internal sealed class HiddenBoardLevel : IDisposable
    {
        private sealed class Portal
        {
            public RectTransform Root;
            public RectTransform Board;
            public CellView[] Cells;
        }

        private readonly MinesweeperGame game;
        private readonly DesktopWindow owner;
        private readonly RectTransform desktop;
        private readonly GridLayoutGroup source;
        private readonly RectTransform sourceRect;
        private readonly RectTransform sourceArea;
        private readonly List<Portal> portals = new List<Portal>();
        private readonly HashSet<DesktopWindow> observed = new HashSet<DesktopWindow>();
        private readonly Vector2 oldAnchorMin, oldAnchorMax, oldPivot, oldSize, oldPosition, oldCellSize;
        private readonly Vector2 oldWindowSize, oldMinimum;
        private readonly bool oldSizeLocked;
        private readonly RectMask2D sourceMask;
        private readonly bool oldMaskEnabled;
        private readonly float cellSize;
        private readonly HiddenBoardPuzzle puzzle;
        private HiddenBoardEmoji emoji;
        private float nextDiscovery;
        private bool disposed;

        public HiddenBoardLevel(MinesweeperGame game, DesktopWindow owner, GridLayoutGroup source,
            HiddenBoardLayout layout, HiddenBoardPuzzle puzzle = null)
        {
            this.game = game;
            this.owner = owner;
            this.source = source;
            this.puzzle = puzzle;
            sourceRect = (RectTransform)source.transform;
            sourceArea = (RectTransform)source.transform.parent;
            desktop = (RectTransform)owner.transform.parent;
            oldAnchorMin = sourceRect.anchorMin;
            oldAnchorMax = sourceRect.anchorMax;
            oldPivot = sourceRect.pivot;
            oldSize = sourceRect.sizeDelta;
            oldPosition = sourceRect.anchoredPosition;
            oldCellSize = source.cellSize;
            oldWindowSize = owner.GetWindowRect().size;
            oldMinimum = owner.MinimumSize;
            oldSizeLocked = owner.SizeLocked;

            // Fit the fixed board inside the desktop's reachable content space, never into the small window.
            cellSize = Mathf.Max(8, Mathf.Floor(Mathf.Min(layout.CellSize,
                (desktop.rect.width - 48) / game.Board.Width,
                (desktop.rect.height - 190) / game.Board.Height)));
            owner.RestoreSize();
            Vector2 windowSize = new Vector2(Mathf.Max(260, layout.ViewportCells.x * cellSize + 32),
                layout.ViewportCells.y * cellSize + 126);
            owner.SetContentSize(windowSize, windowSize);
            owner.SizeLocked = true;
            sourceMask = sourceArea.GetComponent<RectMask2D>();
            oldMaskEnabled = sourceMask != null && sourceMask.enabled;
            if (sourceMask == null) sourceMask = sourceArea.gameObject.AddComponent<RectMask2D>();
            sourceMask.enabled = true;

            ConfigureBoard(sourceRect, source);
            DiscoverWindows();
            game.Changed += Refresh;
            Canvas.willRenderCanvases += AlignBoards;
            Canvas.ForceUpdateCanvases();
            AlignBoards();

            // Start over the upper-left part of the board so the first view is immediately useful.
            Vector3[] corners = new Vector3[4];
            sourceArea.GetWorldCorners(corners);
            Vector2 delta = (Vector2)desktop.InverseTransformPoint(sourceRect.position)
                - (Vector2)desktop.InverseTransformPoint(corners[1]);
            owner.Drag(owner.GetWindowRect(), delta, Vector2Int.zero);
            AlignBoards();
        }

        private void ConfigureBoard(RectTransform board, GridLayoutGroup layout)
        {
            board.anchorMin = board.anchorMax = new Vector2(.5f, .5f);
            board.pivot = Vector2.up;
            board.localScale = Vector3.one;
            board.sizeDelta = new Vector2(game.Board.Width * cellSize, game.Board.Height * cellSize);
            layout.cellSize = new Vector2(cellSize, cellSize);
            layout.spacing = Vector2.zero;
            layout.padding = new RectOffset();
            layout.childAlignment = TextAnchor.UpperLeft;
        }

        public void Tick()
        {
            if (Time.unscaledTime < nextDiscovery) return;
            nextDiscovery = Time.unscaledTime + .5f;
            DiscoverWindows();
        }

        private void DiscoverWindows()
        {
            foreach (DesktopWindow window in desktop.GetComponentsInChildren<DesktopWindow>(true))
            {
                if (window == owner || !observed.Add(window)) continue;
                var marker = window.GetComponentInChildren<DesktopObservationArea>(true);
                RectTransform area = marker != null ? marker.Area : window.transform.Find("Content") as RectTransform;
                if (area == null) continue;

                var root = new GameObject("Level 2 observation", typeof(RectTransform), typeof(RectMask2D), typeof(CanvasGroup));
                var rect = (RectTransform)root.transform;
                rect.SetParent(area, false);
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = rect.offsetMax = Vector2.zero;
                // An observation never takes clicks away from the application, including text editing.
                var group = root.GetComponent<CanvasGroup>();
                group.blocksRaycasts = false;
                group.interactable = false;
                if (marker != null) rect.SetAsFirstSibling();

                var notepad = window.GetComponent<DesktopNotepadWindow>();
                if (notepad != null)
                {
                    if (emoji == null) emoji = new HiddenBoardEmoji();
                    var text = root.AddComponent<HiddenBoardTextPortal>();
                    text.Configure(game, puzzle, notepad, area, cellSize, emoji.Asset);
                    portals.Add(new Portal { Root = rect, Board = text.Board, Cells = System.Array.Empty<CellView>() });
                    continue;
                }

                GameObject board = UnityEngine.Object.Instantiate(source.gameObject, rect, false);
                board.name = "Hidden board observation";
                var boardRect = (RectTransform)board.transform;
                ConfigureBoard(boardRect, board.GetComponent<GridLayoutGroup>());
                var cells = board.GetComponentsInChildren<CellView>(true);
                for (int i = 0; i < cells.Length; i++)
                    cells[i].Bind(game, game.Board.GetCell(i % game.Board.Width, i / game.Board.Width), (_, __) => { });
                foreach (Graphic graphic in board.GetComponentsInChildren<Graphic>(true)) graphic.raycastTarget = false;
                portals.Add(new Portal { Root = rect, Board = boardRect, Cells = cells });
            }
        }

        private void AlignBoards()
        {
            if (disposed || desktop == null) return;
            Rect bounds = desktop.rect;
            // The pet occupies the lower-right corner (48..120 px from the right edge), so the
            // board's right edge stays clear of it instead of hiding the paradox cell underneath.
            const float petClearance = 140f;
            float left = puzzle != null ? bounds.xMax - petClearance - game.Board.Width * cellSize :
                bounds.center.x - game.Board.Width * cellSize * .5f;
            // Keep the authored paradox near the desktop's lower-right corner, above the taskbar.
            float top = puzzle != null ? bounds.yMin + 70 + game.Board.Height * cellSize : bounds.yMax - 140;
            Vector3 topLeft = desktop.TransformPoint(new Vector3(
                left, top, 0));
            if (sourceRect != null) sourceRect.position = topLeft;
            foreach (Portal portal in portals)
                if (portal.Board != null) portal.Board.position = topLeft;
        }

        private void Refresh()
        {
            foreach (Portal portal in portals)
                foreach (CellView cell in portal.Cells)
                    if (cell != null) cell.Render();
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            game.Changed -= Refresh;
            Canvas.willRenderCanvases -= AlignBoards;
            foreach (Portal portal in portals)
                if (portal.Root != null)
                {
                    portal.Root.gameObject.SetActive(false);
                    UnityEngine.Object.Destroy(portal.Root.gameObject);
                }
            emoji?.Dispose();
            // Keep the component for immediate restarts. Destroy is deferred until the end
            // of the frame, so a new level could otherwise find a mask scheduled for removal.
            if (sourceMask != null) sourceMask.enabled = oldMaskEnabled;
            if (sourceRect != null)
            {
                sourceRect.anchorMin = oldAnchorMin;
                sourceRect.anchorMax = oldAnchorMax;
                sourceRect.pivot = oldPivot;
                sourceRect.sizeDelta = oldSize;
                sourceRect.anchoredPosition = oldPosition;
                source.cellSize = oldCellSize;
            }
            if (owner != null)
            {
                owner.SizeLocked = oldSizeLocked;
                owner.SetContentSize(oldWindowSize, oldMinimum);
            }
        }
    }
}
