using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace DesktopMinesweeper
{
    /// <summary>Windows-style icon dragging: follows the cursor, snaps to the desktop grid on release.</summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class DesktopIconDrag : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        /// <summary>Raised after any desktop icon is dropped, with the drop screen position.</summary>
        public static event Action<DesktopIcon, Vector2> Dropped;

        public RectTransform area;
        public RectTransform dropTarget;
        public bool dropClosesGame;
        public Vector2 cellSpacing = new Vector2(108, 87);
        public Vector2 firstCell = new Vector2(54, -49);
        public float taskbarHeight = 38;
        public float edgeMargin = 6;

        RectTransform rect;
        DesktopIcon icon;
        Vector2 startAnchored;
        Vector2 startPointer;
        bool dragging;

        void Awake()
        {
            rect = (RectTransform)transform;
            icon = GetComponent<DesktopIcon>();
        }

        public void OnBeginDrag(PointerEventData e)
        {
            if (e.button != PointerEventData.InputButton.Left || area == null) return;
            dragging = TryPoint(e.position, e.pressEventCamera, out startPointer);
            if (dragging) startAnchored = rect.anchoredPosition;
        }

        public void OnDrag(PointerEventData e)
        {
            if (!dragging) return;
            if (TryPoint(e.position, e.pressEventCamera, out Vector2 point))
                rect.anchoredPosition = startAnchored + (point - startPointer);
        }

        public void OnEndDrag(PointerEventData e)
        {
            if (!dragging) return;
            dragging = false;
            if (dropClosesGame && dropTarget != null &&
                RectTransformUtility.RectangleContainsScreenPoint(dropTarget, e.position, e.pressEventCamera))
            {
                QuitGame();
                return;
            }
            Snap();
            Dropped?.Invoke(icon, e.position);
        }

        void Snap()
        {
            Vector2 size = area.rect.size;
            Vector2 target = rect.anchoredPosition;
            int column = Mathf.RoundToInt((target.x - firstCell.x) / cellSpacing.x);
            int row = Mathf.RoundToInt((firstCell.y - target.y) / cellSpacing.y);
            int maxColumn = Mathf.Max(0, Mathf.FloorToInt((size.x - firstCell.x - rect.rect.width * .5f - edgeMargin) / cellSpacing.x));
            int maxRow = Mathf.Max(0, Mathf.FloorToInt((size.y - taskbarHeight - rect.rect.height * .5f - edgeMargin + firstCell.y) / cellSpacing.y));
            column = Mathf.Clamp(column, 0, maxColumn);
            row = Mathf.Clamp(row, 0, maxRow);
            rect.anchoredPosition = firstCell + new Vector2(column * cellSpacing.x, -row * cellSpacing.y);
        }

        bool TryPoint(Vector2 screenPoint, Camera camera, out Vector2 point)
        {
            point = default;
            return area != null && RectTransformUtility.ScreenPointToLocalPointInRectangle(area, screenPoint, camera, out point);
        }

        static void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }

    static class DesktopIconGrid
    {
        // Installs dragging on the authored desktop icons without editing the scene.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            var icons = UnityEngine.Object.FindObjectsByType<DesktopIcon>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            RectTransform bin = null;
            foreach (var icon in icons)
                if (icon.name == "Recycle Bin") { bin = (RectTransform)icon.transform; break; }
            if (bin == null) return;
            RectTransform area = bin.parent as RectTransform;
            if (area == null) return;
            foreach (var icon in icons)
            {
                if (icon.transform.parent != area) continue; // Skip taskbar entries.
                var drag = icon.GetComponent<DesktopIconDrag>();
                if (drag == null) drag = icon.gameObject.AddComponent<DesktopIconDrag>();
                drag.area = area;
                if (icon.name == "Computer")
                {
                    drag.dropTarget = bin;
                    drag.dropClosesGame = true;
                }
            }
        }
    }
}
