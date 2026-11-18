using System;
using UnityEngine;
using UnityEngine.Events;

namespace DesktopMinesweeper
{
    [RequireComponent(typeof(RectTransform))]
    public sealed class DesktopWindow : MonoBehaviour
    {
        /// <summary>Raised after the window is shown by its shortcut or taskbar button.</summary>
        public event Action Shown;

        [SerializeField] private Vector2 minimumSize = new Vector2(260, 360);
        [SerializeField] private float bottomInset = 38;
        [SerializeField] private GameObject taskButton;
        [SerializeField] private UnityEvent reopened = new UnityEvent();

        private RectTransform Window => (RectTransform)transform;
        private RectTransform Desktop => (RectTransform)transform.parent;
        private Rect restoreRect;
        private bool maximized;
        private bool closed;

        public bool SizeLocked { get; set; }
        public Vector2 MinimumSize => minimumSize;

        public void RestoreSize()
        {
            if (maximized) ToggleMaximize();
        }

        public void Configure(Vector2 minimum, GameObject task) { minimumSize = minimum; taskButton = task; }
        public void HideWindow() => gameObject.SetActive(false);

        public void CloseWindow()
        {
            closed = true;
            if (taskButton != null) taskButton.SetActive(false);
            gameObject.SetActive(false);
        }

        public void ShowWindow()
        {
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            if (taskButton != null) taskButton.SetActive(true);
            FitToDesktop();
            if (closed)
            {
                closed = false;
                reopened.Invoke();
            }
            Shown?.Invoke();
        }

        public void ToggleMaximize()
        {
            if (SizeLocked) return;
            if (maximized)
            {
                maximized = false;
                Apply(Constrain(restoreRect, Workspace()));
            }
            else
            {
                restoreRect = GetWindowRect();
                maximized = true;
                Apply(Workspace());
            }
        }

        public void Drag(Rect initial, Vector2 delta, Vector2Int edges)
        {
            if (maximized) return;
            if (SizeLocked && edges != Vector2Int.zero) return;
            Rect bounds = Workspace();
            Vector2 min = Vector2.Min(minimumSize, bounds.size);
            if (edges == Vector2Int.zero)
            {
                initial.position += delta;
                Apply(Constrain(initial, bounds));
                return;
            }

            if (edges.x < 0)
                initial.xMin = Mathf.Clamp(initial.xMin + delta.x, bounds.xMin, initial.xMax - min.x);
            if (edges.x > 0)
                initial.xMax = Mathf.Clamp(initial.xMax + delta.x, initial.xMin + min.x, bounds.xMax);
            if (edges.y < 0)
                initial.yMin = Mathf.Clamp(initial.yMin + delta.y, bounds.yMin, initial.yMax - min.y);
            if (edges.y > 0)
                initial.yMax = Mathf.Clamp(initial.yMax + delta.y, initial.yMin + min.y, bounds.yMax);

            Apply(Constrain(initial, bounds));
        }

        public Rect GetWindowRect()
        {
            var rect = Window.rect;
            rect.position += (Vector2)Window.localPosition;
            return rect;
        }

        public void SetContentSize(Vector2 preferredSize, Vector2 minimum)
        {
            minimumSize = minimum;
            Rect current = maximized ? restoreRect : GetWindowRect();
            var resized = new Rect(current.center - preferredSize * .5f, preferredSize);
            Rect bounds = Workspace();
            if (bounds.width <= 0 || bounds.height <= 0) return;
            if (maximized) restoreRect = Constrain(resized, bounds);
            else Apply(Constrain(resized, bounds));
        }

        private void Start() => FitToDesktop();

        private void OnRectTransformDimensionsChange()
        {
            if (Application.isPlaying && isActiveAndEnabled && transform.parent != null)
                FitToDesktop();
        }

        private void FitToDesktop()
        {
            Rect bounds = Workspace();
            if (bounds.width <= 0 || bounds.height <= 0) return;
            Apply(maximized ? bounds : Constrain(GetWindowRect(), bounds));
        }

        private Rect Workspace()
        {
            Rect bounds = Desktop.rect;
            bounds.yMin = Mathf.Min(bounds.yMax, bounds.yMin + bottomInset);
            return bounds;
        }

        private static Rect Constrain(Rect rect, Rect bounds)
        {
            rect.size = Vector2.Min(rect.size, bounds.size);
            rect.x = Mathf.Clamp(rect.x, bounds.xMin, bounds.xMax - rect.width);
            rect.y = Mathf.Clamp(rect.y, bounds.yMin, bounds.yMax - rect.height);
            return rect;
        }

        private void Apply(Rect rect)
        {
            if (Window.sizeDelta != rect.size)
                Window.sizeDelta = rect.size;
            Vector2 pivot = rect.min + Vector2.Scale(rect.size, Window.pivot);
            Window.localPosition = new Vector3(pivot.x, pivot.y, Window.localPosition.z);
        }
    }
}
