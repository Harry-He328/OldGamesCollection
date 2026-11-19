using System.Collections.Generic;
using System.Reflection;
using DesktopMinesweeper;
using OldGames.Collection;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace OldGames.Breakout
{
    /// <summary>
    /// Puts Breakout on the desktop. The window, task button and shortcut are runtime clones of
    /// the authored 3D Pinball shell, so they share its XP frame, fonts and title buttons; the
    /// scene file is not modified. Bricks and the paddle are drawn inside the window; the ball,
    /// desktop folders and particles are drawn on a layer directly above the window.
    /// </summary>
    [DefaultExecutionOrder(210)]
    public sealed class BreakoutApp : MonoBehaviour
    {
        public static bool OwnsKeyboard { get; private set; }
        public static BreakoutApp Instance { get; private set; }

        static readonly Vector2 BoardSize = new Vector2(400, 470);
        const float TaskbarHeight = 38;

        RectTransform desktop, frame, boardArea, board, layer, deskLayer, fxLayer, recycleBin, taskbar;
        DesktopWindow shell;
        GameObject taskButton;
        TMP_Text scoreLabel, titleLabel, ringLabel;
        TMP_FontAsset font;
        Image tint, ringFill, cursorTrack, cursorFill, ballImage, paddle, dim;
        RectTransform ringTrack;
        TextMeshProUGUI banner, bannerSub, overTitle, overScore;
        // Wordless cues: a bobbing arrow over the ball before launch, and on level 1 a ghost
        // cursor that shows a drag of the window until the player has dragged once.
        Image launchCue, ghostCursor, ghostPress, ghostFrame;
        bool taughtDrag;
        float cueTime;
        readonly List<RectTransform> taskButtons = new List<RectTransform>();
        float taskLeft;
        AudioSource audioSource;
        readonly Dictionary<Cue, AudioClip> clips = new Dictionary<Cue, AudioClip>();

        BreakoutSimulation sim;
        bool focused, hasPrev;
        Vector2 prevOrigin, lastOrigin;
        Vector2 dragStart;
        // Plain window moves and resizes, once the game is finished.
        int windowPointer = int.MinValue;
        Vector2Int windowEdges;
        Vector2 windowGrab;
        Rect windowInitial;
        Rect dragInitial;
        int dragPointer = int.MinValue;
        Vector2 pointerDown;   // last pointer position, desktop coordinates (y down)

        readonly List<RaycastResult> hits = new List<RaycastResult>();
        readonly Vector3[] corners = new Vector3[4];

        sealed class BrickView { public Brick brick; public RectTransform rt; public Image img; public Image deco; }
        sealed class DeskView
        {
            public DeskItem item; public RectTransform root; public Image icon; public Image selection; public Image star; public TMP_Text label;
            public float dying = -1; public Vector2 from;
        }
        sealed class Particle { public Image img; public Vector2 p, v; public float life; }
        sealed class PopupText { public TextMeshProUGUI text; public Vector2 p; public float life; }

        readonly List<BrickView> bricks = new List<BrickView>();
        readonly List<DeskView> desk = new List<DeskView>();
        readonly List<Particle> particles = new List<Particle>();
        readonly List<PopupText> popups = new List<PopupText>();
        readonly Image[] trail = new Image[10];
        readonly Vector2[] trailPos = new Vector2[10];
        int trailHead;
        RectTransform bricksRoot;
        DeskView held;              // icon being dragged with the mouse
        Vector2 holdOffset;
        int holdPointer = int.MinValue;

        // ---------------------------------------------------------------- install

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            if (Instance != null) return;
            DesktopPinballWindow pinball = null;
            foreach (var w in FindObjectsByType<DesktopPinballWindow>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            { pinball = w; break; }
            if (pinball == null || !(pinball.transform.parent is RectTransform desk)) return;
            var app = new GameObject("Desktop Breakout application").AddComponent<BreakoutApp>();
            Instance = app;
            app.Create(desk, pinball);
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            OwnsKeyboard = false;
        }

        void Create(RectTransform desk, DesktopPinballWindow pinball)
        {
            desktop = desk;
            font = pinball.scoreLabel != null ? pinball.scoreLabel.font : TMP_Settings.defaultFontAsset;
            foreach (Transform child in desk)
            {
                if (child.name == "Recycle Bin") recycleBin = (RectTransform)child;
                if (child.name == "Taskbar") taskbar = (RectTransform)child;
            }

            BuildWindow(pinball);
            BuildTaskButton(pinball);
            BuildShortcut();
            BuildLayer();
            BuildBoard();
            BuildSounds();

            sim = new BreakoutSimulation();
            sim.Layout(BoardSize.x, BoardSize.y);
            sim.Sound += Play;
            sim.Burst += SpawnBurst;
            sim.Popup += SpawnPopup;
            sim.LevelBuilt += RebuildViews;
            sim.DeskItemDestroyed += OnDeskItemDestroyed;
            sim.NewGame();
        }

        void BuildWindow(DesktopPinballWindow pinball)
        {
            var clone = Instantiate(pinball.gameObject, desktop, false);
            clone.name = "Breakout Window";
            var authored = clone.GetComponent<DesktopPinballWindow>();
            boardArea = authored.viewport.rectTransform;
            scoreLabel = authored.scoreLabel;
            // Remove immediately so the Pinball app can never bind to this copy.
            DestroyImmediate(authored);

            frame = (RectTransform)clone.transform;
            shell = clone.GetComponent<DesktopWindow>();
            // Breakout's window is a game of its own, never a view onto Minesweeper's hidden board.
            clone.AddComponent<DesktopObservationArea>().Exclude();

            // Window title and icon.
            foreach (var t in clone.GetComponentsInChildren<TMP_Text>(true))
                if (t.name == "Title") { titleLabel = t; t.text = "Breakout"; }
            foreach (var img in clone.GetComponentsInChildren<Image>(true))
                if (img.name == "App Icon") img.sprite = BreakoutArt.AppIcon;

            // Menu and score-bar buttons pointed at the Pinball window: point them here instead.
            foreach (var b in clone.GetComponentsInChildren<Button>(true))
                Rewire(b.onClick, clone.transform, method => method == "NewGame" ? NewGame : method == "Open" ? (UnityAction)Show : null);
            var reopenedField = typeof(DesktopWindow).GetField("reopened", BindingFlags.Instance | BindingFlags.NonPublic);
            if (reopenedField?.GetValue(shell) is UnityEvent reopened)
            {
                for (int i = 0; i < reopened.GetPersistentEventCount(); i++)
                    reopened.SetPersistentListenerState(i, UnityEventCallState.Off);
                reopened.AddListener(NewGame);
            }

            // The window only moves while bullet time has energy, so the shell's own drag and
            // resize handles become Breakout drag surfaces.
            // Their resize edges are kept: once the game is cleared they work as normal handles again.
            var edgesField = typeof(WindowDragHandle).GetField("resizeEdges", BindingFlags.Instance | BindingFlags.NonPublic);
            foreach (var handle in clone.GetComponentsInChildren<WindowDragHandle>(true))
            {
                var go = handle.gameObject;
                var edges = edgesField != null ? (Vector2Int)edgesField.GetValue(handle) : Vector2Int.zero;
                DestroyImmediate(handle);
                var surface = go.AddComponent<BreakoutDragSurface>();
                surface.app = this;
                surface.edges = edges;
            }
            shell.SizeLocked = true;

            // The Pinball viewport becomes the playfield.
            var raw = boardArea.GetComponent<RawImage>();
            raw.texture = BreakoutArt.Background.texture;
            raw.uvRect = new Rect(0, 0, 1, 1);
            raw.color = Color.white;
            raw.raycastTarget = true;
            foreach (Transform child in boardArea) Destroy(child.gameObject);
            boardArea.gameObject.AddComponent<RectMask2D>();
            boardArea.gameObject.AddComponent<BreakoutDragSurface>().app = this;

            // Resize the shell so the playfield is exactly BoardSize.
            Vector2 chrome = frame.sizeDelta - boardArea.rect.size;
            Vector2 size = BoardSize + chrome;
            frame.sizeDelta = size;
            frame.localPosition = new Vector3(Mathf.Min(220, desktop.rect.width * .18f), 12, frame.localPosition.z);
            shell.Configure(size, pinball.taskButton);   // task button replaced below

            if (scoreLabel != null)
            {
                scoreLabel.alignment = TextAlignmentOptions.Left;
                var lr = scoreLabel.rectTransform;
                lr.offsetMin = new Vector2(lr.offsetMin.x + 52, lr.offsetMin.y);
                scoreLabel.fontSize = 14;
                BuildEnergyRing(lr.parent);
            }
        }

        void BuildEnergyRing(Transform scoreBar)
        {
            var track = Img(scoreBar, "Energy Ring", BreakoutArt.Ring, new Color(0, 0, 0, .18f));
            Place(track.rectTransform, new Vector2(0, .5f), new Vector2(34, 0), new Vector2(40, 40));
            ringTrack = track.rectTransform;
            ringFill = Img(track.transform, "Fill", BreakoutArt.Ring, Color.white);
            Stretch(ringFill.rectTransform);
            ringFill.type = Image.Type.Filled;
            ringFill.fillMethod = Image.FillMethod.Radial360;
            ringFill.fillOrigin = (int)Image.Origin360.Top;
            ringFill.fillClockwise = true;
            var label = Text(track.transform, "Percent", 11, new Color(.1f, .1f, .1f), TextAlignmentOptions.Center);
            Stretch(label.rectTransform);
            label.fontStyle = FontStyles.Bold;
            ringLabel = label;
        }

        void BuildTaskButton(DesktopPinballWindow pinball)
        {
            if (pinball.taskButton == null) return;
            var source = pinball.taskButton;
            taskButton = Instantiate(source, source.transform.parent, false);
            taskButton.name = "Breakout Task";
            foreach (var t in taskButton.GetComponentsInChildren<TMP_Text>(true)) t.text = "Breakout";
            foreach (var img in taskButton.GetComponentsInChildren<Image>(true))
                if (img.name == "App Icon") img.sprite = BreakoutArt.AppIcon;
            var button = taskButton.GetComponent<Button>();
            if (button != null) Rewire(button.onClick, taskButton.transform, _ => Show);

            // The authored slots overlap and leave gaps, so the open task buttons are packed
            // left to right from the first slot every frame (see ArrangeTaskButtons).
            taskLeft = float.MaxValue;
            foreach (Transform sibling in source.transform.parent)
                if (sibling.name.EndsWith(" Task") && sibling is RectTransform rt)
                {
                    taskButtons.Add(rt);
                    if (sibling != taskButton.transform) taskLeft = Mathf.Min(taskLeft, rt.anchoredPosition.x - rt.sizeDelta.x * .5f);
                }
            if (taskLeft == float.MaxValue) taskLeft = 116;
            taskButton.SetActive(false);
            shell.Configure(frame.sizeDelta, taskButton);
        }

        void BuildShortcut()
        {
            Transform source = null;
            foreach (Transform child in desktop)
                if (child.name == "3D Pinball Shortcut") source = child;
            if (source == null)
                foreach (var s in desktop.GetComponentsInChildren<DesktopAppShortcut>(true)) { source = s.transform; break; }
            if (source == null) return;

            var go = Instantiate(source.gameObject, desktop, false);
            go.name = "Breakout Shortcut";
            foreach (var c in go.GetComponents<DesktopAppShortcut>()) DestroyImmediate(c);
            foreach (var c in go.GetComponents<DesktopPinballEntry>()) DestroyImmediate(c);
            foreach (var c in go.GetComponents<PinballDesktopShortcut>()) DestroyImmediate(c);
            foreach (var c in go.GetComponents<DesktopIconDrag>()) DestroyImmediate(c);
            go.AddComponent<BreakoutShortcut>().app = this;
            foreach (var t in go.GetComponentsInChildren<TMP_Text>(true)) if (t.name == "Label") t.text = "Breakout";
            foreach (var img in go.GetComponentsInChildren<Image>(true)) if (img.name == "Icon") { img.sprite = BreakoutArt.AppIcon; img.preserveAspect = true; }

            var drag = go.AddComponent<DesktopIconDrag>();
            drag.area = desktop;
            ((RectTransform)go.transform).anchoredPosition = FreeIconCell(drag, (RectTransform)go.transform);
        }

        Vector2 FreeIconCell(DesktopIconDrag grid, RectTransform self)
        {
            var taken = new List<Vector2>();
            foreach (Transform child in desktop)
                if (child != self && child.GetComponent<DesktopIcon>() != null && child is RectTransform rt)
                    taken.Add(rt.anchoredPosition);
            float usable = desktop.rect.height - grid.taskbarHeight - self.rect.height * .5f - grid.edgeMargin + grid.firstCell.y;
            int rows = Mathf.Max(1, Mathf.FloorToInt(usable / grid.cellSpacing.y) + 1);
            for (int col = 0; col < 8; col++)
                for (int row = 0; row < rows; row++)
                {
                    Vector2 cell = grid.firstCell + new Vector2(col * grid.cellSpacing.x, -row * grid.cellSpacing.y);
                    bool free = true;
                    foreach (var p in taken) if ((p - cell).sqrMagnitude < 400) { free = false; break; }
                    if (free) return cell;
                }
            return grid.firstCell;
        }

        static void Rewire(UnityEventBase ev, Transform owner, System.Func<string, UnityAction> map)
        {
            if (!(ev is UnityEvent unityEvent)) return;
            for (int i = 0; i < ev.GetPersistentEventCount(); i++)
            {
                Object target = ev.GetPersistentTarget(i);
                // Unity's null check first: a destroyed component is still a C# object.
                bool inside = target != null && target is Component c && c.transform.IsChildOf(owner);
                if (inside) continue;   // e.g. this window's own Close / Minimize
                ev.SetPersistentListenerState(i, UnityEventCallState.Off);
                var action = map(ev.GetPersistentMethodName(i));
                if (action != null) unityEvent.AddListener(action);
            }
        }

        void BuildLayer()
        {
            layer = NewRect("Breakout Layer", desktop);
            Stretch(layer);
            tint = Img(layer, "Bullet Time Tint", BreakoutArt.Pixel, Color.clear);
            Stretch(tint.rectTransform);
            deskLayer = NewRect("Desktop Items", layer); Stretch(deskLayer);
            fxLayer = NewRect("Effects", layer); Stretch(fxLayer);
            for (int i = 0; i < trail.Length; i++)
            {
                trail[i] = Img(fxLayer, "Trail", BreakoutArt.Circle, Color.clear);
                Place(trail[i].rectTransform, new Vector2(0, 1), Vector2.zero, Vector2.one * 14);
            }
            ballImage = Img(fxLayer, "Ball", BreakoutArt.Ball, Color.white);
            Place(ballImage.rectTransform, new Vector2(0, 1), Vector2.zero, Vector2.one * BreakoutSimulation.BallRadius * 2);
            launchCue = Img(fxLayer, "Launch Cue", BreakoutArt.Chevron, Color.clear);
            Place(launchCue.rectTransform, new Vector2(0, 1), Vector2.zero, new Vector2(20, 12));
            ghostFrame = Img(fxLayer, "Drag Cue Frame", BreakoutArt.Frame, Color.clear);
            ghostFrame.type = Image.Type.Sliced;
            Place(ghostFrame.rectTransform, new Vector2(0, 1), Vector2.zero, Vector2.one * 10, new Vector2(0, 1));
            ghostPress = Img(fxLayer, "Drag Cue Press", BreakoutArt.Ring, Color.clear);
            Place(ghostPress.rectTransform, new Vector2(0, 1), Vector2.zero, Vector2.one * 22);
            ghostCursor = Img(fxLayer, "Drag Cue Cursor", BreakoutArt.Cursor, Color.clear);
            Place(ghostCursor.rectTransform, new Vector2(0, 1), Vector2.zero, new Vector2(20, 30), new Vector2(0, 1));
            cursorTrack = Img(fxLayer, "Cursor Ring", BreakoutArt.Ring, new Color(0, 0, 0, .25f));
            Place(cursorTrack.rectTransform, new Vector2(0, 1), Vector2.zero, Vector2.one * 40);
            cursorFill = Img(cursorTrack.transform, "Fill", BreakoutArt.Ring, Color.white);
            Stretch(cursorFill.rectTransform);
            cursorFill.type = Image.Type.Filled;
            cursorFill.fillMethod = Image.FillMethod.Radial360;
            cursorFill.fillOrigin = (int)Image.Origin360.Top;
            layer.gameObject.SetActive(false);
        }

        void BuildBoard()
        {
            board = NewRect("Breakout Board", boardArea);
            Stretch(board);
            var danger = Img(board, "Bottom Edge", BreakoutArt.Danger, Color.white);
            var dr = danger.rectTransform;
            dr.anchorMin = new Vector2(0, 0); dr.anchorMax = new Vector2(1, 0); dr.pivot = new Vector2(.5f, 0);
            dr.sizeDelta = new Vector2(0, 40); dr.anchoredPosition = Vector2.zero;
            bricksRoot = NewRect("Bricks", board); Stretch(bricksRoot);
            paddle = Img(board, "Paddle", BreakoutArt.Gloss, new Color(.33f, .72f, .3f));
            paddle.type = Image.Type.Sliced;

            banner = Text(board, "Banner", 30, Color.white, TextAlignmentOptions.Center);
            banner.fontStyle = FontStyles.Bold;
            bannerSub = Text(board, "Banner Name", 18, new Color(.75f, .88f, 1f), TextAlignmentOptions.Center);
            dim = Img(board, "Game Over Dim", BreakoutArt.Pixel, new Color(0, .04f, .16f, .72f));
            Stretch(dim.rectTransform);
            overTitle = Text(dim.transform, "Game Over", 32, new Color(1f, .55f, .45f), TextAlignmentOptions.Center);
            overTitle.fontStyle = FontStyles.Bold;
            overScore = Text(dim.transform, "Final Score", 15, Color.white, TextAlignmentOptions.Center);
            Layout(banner, .36f, 0, 40);
            Layout(bannerSub, .36f, 40, 26);
            Layout(overTitle, .4f, 0, 44);
            Layout(overScore, .4f, 50, 50);
        }

        void Layout(TextMeshProUGUI t, float yFraction, float offset, float height)
        {
            var rt = t.rectTransform;
            rt.anchorMin = new Vector2(0, 1); rt.anchorMax = new Vector2(1, 1); rt.pivot = new Vector2(.5f, 1);
            rt.sizeDelta = new Vector2(-40, height);
            rt.anchoredPosition = new Vector2(0, -(BoardSize.y * yFraction + offset));
        }

        // ---------------------------------------------------------------- public actions

        public void Show()
        {
            if (shell == null) return;
            shell.ShowWindow();
            Focus();
        }

        public void NewGame()
        {
            if (Finished) return;   // cleared for good: the window is only a window now
            sim?.NewGame();
        }

        /// <summary>True once all levels are cleared. The game then stays on its ALL CLEAR screen
        /// for good, and the window is an ordinary window: it moves and resizes freely (no energy,
        /// no bullet time) and no longer takes the keyboard.</summary>
        public bool Finished { get; private set; }

        void Finish()
        {
            Finished = true;
            if (sim.Dragging) sim.EndDrag();
            dragPointer = int.MinValue;
            EndHold(); holdPointer = int.MinValue;
            shell.SizeLocked = false;
            shell.Configure(new Vector2(170, 120), taskButton);
            OwnsKeyboard = false;
        }

        /// <summary>Jumps to a level (1-based). Handy for testing from the console or a debugger.</summary>
        public void GotoLevel(int level) => sim?.GotoLevel(level);

        public void Focus()
        {
            focused = true;
            frame.SetAsLastSibling();
            OwnsKeyboard = !Finished;
        }

        // ---------------------------------------------------------------- input

        internal void Press(PointerEventData e, Vector2Int edges = default)
        {
            Focus();
            if (Finished)
            {
                if (TryPoint(e.position, e.pressEventCamera, out windowGrab))
                {
                    windowPointer = e.pointerId;
                    windowEdges = edges;
                    windowInitial = shell.GetWindowRect();
                }
                return;
            }
            if (sim.State != BreakoutSimulation.Phase.Play) { sim.NewGame(); return; }
            if (sim.Stuck) { sim.Launch(); return; }
            if (!TryPoint(e.position, e.pressEventCamera, out dragStart)) return;
            if (sim.TryBeginDrag())
            {
                dragPointer = e.pointerId;
                dragInitial = shell.GetWindowRect();
            }
        }

        internal void DragTo(PointerEventData e)
        {
            if (Finished)
            {
                if (e.pointerId == windowPointer && TryPoint(e.position, e.pressEventCamera, out Vector2 at))
                    shell.Drag(windowInitial, at - windowGrab, windowEdges);
                return;
            }
            if (!sim.Dragging || e.pointerId != dragPointer) return;
            if (TryPoint(e.position, e.pressEventCamera, out Vector2 p))
                MoveWindow(dragInitial, p - dragStart);
        }

        // Like DesktopWindow.Drag, but the window may leave the screen by up to 60% on the left,
        // right and bottom, and 30% at the top, so every part of the desktop stays reachable.
        void MoveWindow(Rect initial, Vector2 delta)
        {
            Rect b = desktop.rect;
            b.yMin += TaskbarHeight;
            Rect r = initial;
            r.position += delta;
            r.x = Mathf.Clamp(r.x, b.xMin - r.width * .6f, b.xMax - r.width * .4f);
            r.y = Mathf.Clamp(r.y, b.yMin - r.height * .6f, b.yMax - r.height * .7f);
            Vector2 pivot = r.min + Vector2.Scale(r.size, frame.pivot);
            frame.localPosition = new Vector3(pivot.x, pivot.y, frame.localPosition.z);
        }

        // ---- dragging an icon (only icons marked draggable). This costs no energy.

        internal void PressItem(DeskItem item, PointerEventData e)
        {
            Focus();
            if (!TryPoint(e.position, e.pressEventCamera, out Vector2 p)) return;
            if (BeginHold(item, LocalToDown(p))) holdPointer = e.pointerId;
        }

        internal void DragItem(PointerEventData e)
        {
            if (held == null || e.pointerId != holdPointer) return;
            if (TryPoint(e.position, e.pressEventCamera, out Vector2 p)) MoveHold(LocalToDown(p));
        }

        internal void ReleaseItem(PointerEventData e)
        {
            if (e.pointerId != holdPointer) return;
            holdPointer = int.MinValue;
            EndHold();
        }

        /// <summary>Picks up a draggable icon at a desktop point (y down). Public for tests.</summary>
        public bool BeginHold(DeskItem item, Vector2 at)
        {
            if (item == null || !item.Alive || !item.spec.draggable || sim.State != BreakoutSimulation.Phase.Play) return false;
            foreach (var v in desk) if (v.item == item) held = v;
            if (held == null) return false;
            holdOffset = item.Center - at;
            item.pos = item.Center;
            item.bound = false; item.placed = true; item.held = true;
            sim.Moved = true;
            Play(Cue.DragEnd);
            return true;
        }

        public void MoveHold(Vector2 at)
        {
            if (held == null) return;
            Rect r = desktop.rect;
            Vector2 c = at + holdOffset;
            held.item.pos = new Vector2(Mathf.Clamp(c.x, 20, r.width - 20), Mathf.Clamp(c.y, 20, r.height - TaskbarHeight - 20));
        }

        /// <summary>Drops the held icon. Dropped on the playfield it moves with the window again;
        /// dropped anywhere else it stays on the desktop.</summary>
        public void EndHold()
        {
            if (held == null) return;
            var item = held.item;
            held = null;
            item.held = false;
            if (!item.Alive) return;
            if (new Rect(lastOrigin, boardArea.rect.size).Contains(item.pos))
            {
                item.bound = true;
                item.local = item.pos - lastOrigin;
            }
            Play(Cue.DragStart);
        }

        internal void Release(PointerEventData e)
        {
            if (e.pointerId == windowPointer) windowPointer = int.MinValue;
            if (e.pointerId != dragPointer) return;
            dragPointer = int.MinValue;
            sim.EndDrag();
        }

        bool TryPoint(Vector2 screen, Camera cam, out Vector2 local) =>
            RectTransformUtility.ScreenPointToLocalPointInRectangle(desktop, screen, cam, out local);

        Vector2 LocalToDown(Vector2 p) => new Vector2(p.x - desktop.rect.xMin, desktop.rect.yMax - p.y);

        void UpdateFocus(bool visible)
        {
            var mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.wasPressedThisFrame && EventSystem.current != null)
            {
                var e = new PointerEventData(EventSystem.current) { position = mouse.position.ReadValue() };
                hits.Clear();
                EventSystem.current.RaycastAll(e, hits);
                if (hits.Count > 0) focused = hits[0].gameObject.transform.IsChildOf(frame) || hits[0].gameObject.transform.IsChildOf(layer);
            }
            OwnsKeyboard = visible && focused && !Finished;
            if (mouse != null && TryPoint(mouse.position.ReadValue(), null, out Vector2 p))
                pointerDown = LocalToDown(p);
        }

        // ---------------------------------------------------------------- frame

        void LateUpdate()
        {
            if (frame == null || sim == null) return;
            ArrangeTaskButtons();
            bool visible = frame.gameObject.activeInHierarchy;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // Testing shortcut: Ctrl+Shift+B clears every level at once, from anywhere.
            var cheat = Keyboard.current;
            if (!Finished && cheat != null && cheat.ctrlKey.isPressed && cheat.shiftKey.isPressed && cheat.bKey.wasPressedThisFrame)
            {
                sim.Win();
                Finish();
            }
#endif
            UpdateFocus(visible);
            if (layer.gameObject.activeSelf != visible) layer.gameObject.SetActive(visible);
            if (!visible)
            {
                if (sim.Dragging) sim.EndDrag();
                dragPointer = int.MinValue;
                EndHold(); holdPointer = int.MinValue;
                hasPrev = false;
                return;
            }
            KeepLayerAboveWindow();

            if (!Finished && sim.State == BreakoutSimulation.Phase.Won) Finish();
            if (Finished)
            {
                // Frozen on the ALL CLEAR screen; the board just follows the window.
                PlaceDeskItems(ContentOrigin());
                Render(Mathf.Min(Time.unscaledDeltaTime, 1 / 30f), 0);
                return;
            }

            var keys = Keyboard.current;
            if (keys != null && OwnsKeyboard)
            {
                if (keys.spaceKey.wasPressedThisFrame)
                {
                    if (sim.State != BreakoutSimulation.Phase.Play) sim.NewGame();
                    else sim.Launch();
                }
                if (keys.f2Key.wasPressedThisFrame) sim.NewGame();
            }

            Vector2 origin = ContentOrigin();
            if (!hasPrev)
            {
                // The window may have moved while hidden; keep the ball where it was relative to it.
                sim.Ball += origin - lastOrigin;
                prevOrigin = origin;
                hasPrev = true;
            }
            Vector2 size = boardArea.rect.size;
            sim.Layout(size.x, size.y);
            PlaceDeskItems(origin);

            float dt = Mathf.Min(Time.unscaledDeltaTime, 1 / 30f);
            sim.Step(dt, prevOrigin, origin);
            if (!sim.Dragging) dragPointer = int.MinValue;
            prevOrigin = lastOrigin = origin;

            Render(dt, dt * sim.TimeScale);
        }

        void ArrangeTaskButtons()
        {
            float x = taskLeft;
            foreach (var rt in taskButtons)
            {
                if (rt == null || !rt.gameObject.activeSelf) continue;
                var p = new Vector2(x + rt.sizeDelta.x * .5f, rt.anchoredPosition.y);
                if (rt.anchoredPosition != p) rt.anchoredPosition = p;
                x += rt.sizeDelta.x + 3;
            }
        }

        void KeepLayerAboveWindow()
        {
            int wi = frame.GetSiblingIndex(), li = layer.GetSiblingIndex();
            if (li != wi + 1) layer.SetSiblingIndex(li < wi ? wi : wi + 1);
            // The window can be dragged under the taskbar, so keep the taskbar on top, as in XP.
            if (taskbar != null && taskbar.GetSiblingIndex() < layer.GetSiblingIndex()) taskbar.SetAsLastSibling();
        }

        Vector2 ContentOrigin()
        {
            boardArea.GetWorldCorners(corners);
            return ToDown(corners[1]);
        }

        Vector2 ToDown(Vector3 world)
        {
            Vector3 p = desktop.InverseTransformPoint(world);
            Rect r = desktop.rect;
            return new Vector2(p.x - r.xMin, r.yMax - p.y);
        }

        void PlaceDeskItems(Vector2 origin)
        {
            float w = desktop.rect.width, h = desktop.rect.height - TaskbarHeight;
            foreach (var v in desk)
            {
                var item = v.item;
                var spec = item.spec;
                // The window can be dragged partly off screen, so any spot clear of the icon column works.
                Vector2 c = item.bound ? origin + item.local
                          : item.placed ? item.pos
                          : new Vector2(Mathf.Clamp(spec.fx * w, 150, w - 50), Mathf.Clamp(spec.fy * h, 50, h - 50));
                if (v.dying < 0) v.root.anchoredPosition = new Vector2(c.x, -c.y);
                // Hit area: the icon only, not its label.
                v.item.rect = v.item.Kind == DeskKind.Disc
                    ? new Rect(c.x - 19, c.y - 19, 38, 38)
                    : new Rect(c.x - 19, c.y - 15, 38, 32);
            }
        }

        // ---------------------------------------------------------------- views

        void RebuildViews()
        {
            foreach (var v in bricks) Destroy(v.rt.gameObject);
            bricks.Clear();
            foreach (var b in sim.Bricks)
            {
                var img = Img(bricksRoot, "Brick", BreakoutArt.Gloss, Color.white);
                img.type = Image.Type.Sliced;
                var view = new BrickView { brick = b, rt = img.rectTransform, img = img };
                if (b.kind == BrickKind.Bomb)
                {
                    view.deco = Img(img.transform, "Fuse", BreakoutArt.Circle, new Color(.2f, .05f, .03f));
                    Place(view.deco.rectTransform, new Vector2(.5f, .5f), Vector2.zero, Vector2.one * b.h * .55f, new Vector2(.5f, .5f));
                }
                else if (b.kind == BrickKind.Energy)
                {
                    view.deco = Img(img.transform, "Bolt", BreakoutArt.Bolt, Color.white);
                    Place(view.deco.rectTransform, new Vector2(.5f, .5f), Vector2.zero, new Vector2(b.h * .55f, b.h * .75f), new Vector2(.5f, .5f));
                }
                else if (b.kind == BrickKind.Target)
                {
                    view.deco = Img(img.transform, "Star", BreakoutArt.Star, new Color(.75f, .35f, 0));
                    Place(view.deco.rectTransform, new Vector2(.5f, .5f), Vector2.zero, Vector2.one * b.h * .9f, new Vector2(.5f, .5f));
                }
                else if (b.kind == BrickKind.Steel)
                {
                    view.deco = Img(img.transform, "Rivets", BreakoutArt.Outline, new Color(.25f, .27f, .32f, .5f));
                    view.deco.type = Image.Type.Sliced;
                    Stretch(view.deco.rectTransform, 3);
                }
                bricks.Add(view);
            }

            foreach (var v in desk) Destroy(v.root.gameObject);
            desk.Clear();
            foreach (var item in sim.Desk)
            {
                var root = NewRect(item.spec.label, deskLayer);
                Place(root, new Vector2(0, 1), Vector2.zero, new Vector2(96, 76));
                var sel = Img(root, "Selection", BreakoutArt.Pixel, Color.clear);
                Place(sel.rectTransform, new Vector2(.5f, .5f), new Vector2(0, 2), new Vector2(46, 42), new Vector2(.5f, .5f));
                Sprite sprite = item.Kind == DeskKind.Folder ? BreakoutArt.Folder : item.Kind == DeskKind.Locked ? BreakoutArt.LockedFolder
                              : item.Kind == DeskKind.File ? BreakoutArt.Document : BreakoutArt.Disc;
                var icon = Img(root, "Icon", sprite, Color.white);
                Place(icon.rectTransform, new Vector2(.5f, .5f), new Vector2(0, 2), new Vector2(42, 42), new Vector2(.5f, .5f));
                if (item.spec.draggable)
                {
                    icon.raycastTarget = true;
                    icon.gameObject.AddComponent<BreakoutIconDrag>().Bind(this, item);
                }
                Image star = null;
                if (item.spec.goal)
                {
                    star = Img(root, "Goal Star", BreakoutArt.Star, Color.white);
                    Place(star.rectTransform, new Vector2(.5f, .5f), new Vector2(19, 19), Vector2.one * 20, new Vector2(.5f, .5f));
                }
                var shadow = Text(root, "Label Shadow", 12, new Color(0, 0, 0, .75f), TextAlignmentOptions.Top);
                Place(shadow.rectTransform, new Vector2(.5f, .5f), new Vector2(1, -25), new Vector2(110, 34), new Vector2(.5f, 1));
                shadow.text = item.spec.label;
                var label = Text(root, "Label", 12, item.spec.goal ? new Color(1f, .9f, .45f) : Color.white, TextAlignmentOptions.Top);
                Place(label.rectTransform, new Vector2(.5f, .5f), new Vector2(0, -24), new Vector2(110, 34), new Vector2(.5f, 1));
                label.text = item.spec.label;
                desk.Add(new DeskView { item = item, root = root, icon = icon, selection = sel, star = star, label = label });
            }
            held = null; holdPointer = int.MinValue;
            PlaceDeskItems(ContentOrigin());
            foreach (var p in particles) p.life = 0;
        }

        void OnDeskItemDestroyed(DeskItem item)
        {
            foreach (var v in desk)
                if (v.item == item) { v.dying = 0; v.from = v.root.anchoredPosition; }
        }

        void Render(float dt, float gdt)
        {
            float slow = sim.SlowAmount;
            tint.color = new Color(.12f, .3f, .75f, .16f * slow);
            if (titleLabel != null) titleLabel.text = slow > .05f ? "Breakout  -  0.25x" : "Breakout";
            if (scoreLabel != null)
                scoreLabel.text = $"SCORE {sim.Score:N0}      LIVES {Mathf.Max(0, sim.Lives)}      LEVEL {sim.Level}";

            Color energyColor = sim.Energy > .35f ? new Color(.2f, .72f, .25f) : sim.Energy > .15f ? new Color(1f, .62f, .15f) : new Color(.9f, .2f, .15f);
            if (ringFill != null)
            {
                ringFill.fillAmount = sim.Energy;
                ringFill.color = energyColor;
                float shake = sim.EmptyFlash > 0 ? Mathf.Sin(sim.EmptyFlash * 40) * 3 * sim.EmptyFlash : 0;
                ringTrack.anchoredPosition = new Vector2(34 + shake, 0);
                ringLabel.text = Mathf.RoundToInt(sim.Energy * 100) + "%";
                ringLabel.color = sim.EmptyFlash > 0 ? new Color(.85f, .1f, .1f) : new Color(.1f, .1f, .1f);
            }

            // Screen shake applies to what is drawn, not to the physics.
            Vector2 shakeOffset = Random.insideUnitCircle * sim.Shake * 3;
            board.anchoredPosition = shakeOffset;

            foreach (var v in bricks)
            {
                var b = v.brick;
                bool on = b.Alive;
                if (v.rt.gameObject.activeSelf != on) v.rt.gameObject.SetActive(on);
                if (!on) continue;
                Place(v.rt, new Vector2(0, 1), new Vector2(b.x, -b.y), new Vector2(b.w, b.h), new Vector2(0, 1));
                Color c = BreakoutSimulation.BrickColor(b);
                if (b.kind == BrickKind.Normal && b.hp > 1) c *= 1 - .17f * (b.hp - 1);
                if (b.kind == BrickKind.Ghost)
                {
                    bool solid = sim.GhostSolid;
                    v.img.sprite = solid ? BreakoutArt.Gloss : BreakoutArt.Outline;
                    c.a = solid ? 1 : .55f;
                }
                if (b.kind == BrickKind.Bomb) c = Color.Lerp(c, new Color(1, .55f, .3f), .5f + .5f * Mathf.Sin(sim.GameTime * 8 + b.x));
                if (b.kind == BrickKind.Target) c = Color.Lerp(c, new Color(1f, .97f, .75f), .25f + .25f * Mathf.Sin(Time.unscaledTime * 6));
                c = Color.Lerp(c, Color.white, b.flash * .8f);
                c.a = b.kind == BrickKind.Ghost ? c.a : 1;
                v.img.color = c;
            }

            Place(paddle.rectTransform, new Vector2(0, 1),
                new Vector2(sim.PaddleX - sim.PaddleWidth * .5f, -sim.PaddleY),
                new Vector2(sim.PaddleWidth, BreakoutSimulation.PaddleHeight), new Vector2(0, 1));

            // Desktop icons
            foreach (var v in desk)
            {
                var item = v.item;
                if (v.dying >= 0)
                {
                    v.dying += dt;
                    float t = Mathf.Clamp01(v.dying / .55f);
                    Vector2 target = recycleBin != null ? recycleBin.anchoredPosition : v.from + new Vector2(0, 60);
                    float e = t * t * (3 - 2 * t);
                    v.root.anchoredPosition = Vector2.Lerp(v.from, target, e) + new Vector2(0, Mathf.Sin(t * Mathf.PI) * 60);
                    v.root.localScale = Vector3.one * Mathf.Lerp(1, .3f, e);
                    if (t >= 1 && v.root.gameObject.activeSelf) v.root.gameObject.SetActive(false);
                    continue;
                }
                bool inWindow = item.rect.Overlaps(new Rect(lastOrigin, boardArea.rect.size));
                // Draggable icons answer the mouse like real XP icons: a light highlight on hover.
                bool hover = item.spec.draggable && item.rect.Contains(pointerDown);
                v.selection.color = item.held ? new Color(.25f, .5f, 1f, .6f) : hover ? new Color(.45f, .65f, 1f, .5f)
                                  : inWindow ? new Color(.2f, .4f, .85f, .45f) : Color.clear;
                if (!item.spec.goal) v.label.color = inWindow ? Color.white : new Color(1, 1, 1, .92f);
                if (v.star != null)
                {
                    float s = 1 + .15f * Mathf.Sin(Time.unscaledTime * 6);
                    v.star.rectTransform.localScale = Vector3.one * s;
                    v.star.rectTransform.localEulerAngles = new Vector3(0, 0, Mathf.Sin(Time.unscaledTime * 2) * 12);
                }
                float pulse = item.Kind == DeskKind.Folder ? .03f * Mathf.Sin(Time.unscaledTime * 5 + item.spec.fx * 10) : 0;
                v.icon.rectTransform.localScale = Vector3.one * (1 + item.flash * (item.Kind == DeskKind.Disc ? .3f : .15f) + pulse);
                // No text tip on the vault level: the untouched file gives a small shake every few
                // seconds instead. Desktop folders give no hint at all.
                float wiggle = 0;
                if (item.spec.draggable && item.spec.inWindow && !sim.Moved && sim.LevelTime > 12)
                {
                    float ph = (sim.LevelTime - 12) % 5;
                    if (ph < .6f) wiggle = Mathf.Sin(ph * 40) * 7 * (1 - ph / .6f);
                }
                v.icon.rectTransform.localEulerAngles = new Vector3(0, 0, wiggle);
                v.icon.color = Color.Lerp(item.Kind == DeskKind.Folder && item.hp > 1 ? new Color(1, .82f, .6f) : Color.white, new Color(1.5f, 1.5f, 1.5f), item.flash);
            }

            // Ball and trail (desktop space)
            bool showBall = sim.State == BreakoutSimulation.Phase.Play && sim.Clearing <= 0;
            ballImage.enabled = showBall;
            ballImage.rectTransform.anchoredPosition = new Vector2(sim.Ball.x, -sim.Ball.y);
            trailPos[trailHead] = sim.Ball;
            trailHead = (trailHead + 1) % trail.Length;
            for (int i = 0; i < trail.Length; i++)
            {
                int idx = (trailHead + i) % trail.Length;
                float a = i / (float)trail.Length;
                trail[i].enabled = showBall && !sim.Stuck;
                trail[i].color = new Color(.85f, .92f, 1f, a * .35f);
                trail[i].rectTransform.anchoredPosition = new Vector2(trailPos[idx].x, -trailPos[idx].y);
                trail[i].rectTransform.sizeDelta = Vector2.one * BreakoutSimulation.BallRadius * 2 * (.4f + .6f * a);
            }

            // Cursor ring while dragging
            cursorTrack.enabled = cursorFill.enabled = sim.Dragging;
            if (sim.Dragging)
            {
                cursorTrack.rectTransform.anchoredPosition = new Vector2(pointerDown.x, -pointerDown.y);
                cursorFill.fillAmount = sim.Energy;
                cursorFill.color = energyColor;
            }

            RenderCues(dt);

            // Texts
            bool clearing = sim.Clearing > 0;
            float bannerAlpha = sim.State == BreakoutSimulation.Phase.Play ? clearing ? 1 : Mathf.Min(1, sim.Banner) : 0;
            SetText(banner, clearing ? "Level clear!" : "Level " + sim.Level + " / " + sim.LevelCount, bannerAlpha);
            SetText(bannerSub, clearing ? "+" + 100 * sim.Level : sim.Spec.name, bannerAlpha);
            bool over = sim.State != BreakoutSimulation.Phase.Play;
            if (dim.gameObject.activeSelf != over) dim.gameObject.SetActive(over);
            if (over)
            {
                bool won = sim.State == BreakoutSimulation.Phase.Won;
                overTitle.text = won ? "ALL CLEAR!" : "GAME OVER";
                overTitle.color = won ? new Color(1f, .85f, .35f) : new Color(1f, .55f, .45f);
                overScore.text = won ? $"Score {sim.Score:N0}  -  lives left {sim.Lives}"
                                     : $"Score {sim.Score:N0}  -  reached level {sim.Level}\nClick or press Space to play again";
            }

            // Particles and popups follow game time, so they slow down in bullet time too.
            foreach (var p in particles)
            {
                if (p.life <= 0) { if (p.img.enabled) p.img.enabled = false; continue; }
                p.life -= gdt;
                p.v.y += 500 * gdt;
                p.p += p.v * gdt;
                var col = p.img.color; col.a = Mathf.Clamp01(p.life / .6f); p.img.color = col;
                p.img.rectTransform.anchoredPosition = new Vector2(p.p.x, -p.p.y);
            }
            foreach (var p in popups)
            {
                if (p.life <= 0) { if (p.text.enabled) p.text.enabled = false; continue; }
                p.life -= gdt;
                p.p.y -= 40 * gdt;
                var col = p.text.color; col.a = Mathf.Clamp01(p.life); p.text.color = col;
                p.text.rectTransform.anchoredPosition = new Vector2(p.p.x, -p.p.y);
            }
        }

        void RenderCues(float dt)
        {
            float now = Time.unscaledTime;

            // Before launch: the ball breathes and an arrow bobs above it.
            bool waiting = sim.State == BreakoutSimulation.Phase.Play && sim.Stuck && sim.Banner < 1;
            float bob = Mathf.Sin(now * 5);
            float wa = waiting ? Mathf.Clamp01((1 - sim.Banner) / .4f) : 0;
            launchCue.color = new Color(1, 1, 1, wa * (.55f + .3f * bob));
            launchCue.rectTransform.anchoredPosition = new Vector2(sim.Ball.x, -(sim.Ball.y - BreakoutSimulation.BallRadius - 14 - 3 * bob));
            ballImage.rectTransform.localScale = Vector3.one * (1 + (waiting ? .12f * (.5f + .5f * bob) : 0));

            // Level 1: a ghost cursor grabs the playfield and slides it sideways, with a ghost
            // outline of the window following, until the player drags the window once.
            if (sim.Dragging) taughtDrag = true;
            bool show = !taughtDrag && sim.Level == 1 && sim.State == BreakoutSimulation.Phase.Play
                        && !sim.Stuck && sim.Clearing <= 0 && sim.LevelTime > 2.5f && held == null;
            cueTime = show ? cueTime + dt : 0;
            float fade = 0, move = 0;
            bool press = false;
            if (show)
            {
                float c = cueTime % 2.8f;
                fade = c < .3f ? c / .3f : c > 2.1f ? Mathf.Clamp01(1 - (c - 2.1f) / .4f) : 1;
                float m = Mathf.Clamp01((c - .6f) / 1.1f);
                move = m * m * (3 - 2 * m) * 80;
                press = c > .4f && c < 2.1f;
            }
            Vector2 tip = lastOrigin + new Vector2(BoardSize.x * .5f + 20, BoardSize.y * .66f) + new Vector2(move, 0);
            ghostCursor.color = new Color(1, 1, 1, .85f * fade);
            ghostCursor.rectTransform.anchoredPosition = new Vector2(tip.x, -tip.y);
            ghostCursor.rectTransform.localScale = Vector3.one * (press ? .9f : 1);
            ghostPress.color = new Color(1, 1, 1, press ? .5f * fade : 0);
            ghostPress.rectTransform.anchoredPosition = new Vector2(tip.x, -tip.y);
            frame.GetWorldCorners(corners);
            Vector2 topLeft = ToDown(corners[1]), bottomRight = ToDown(corners[3]);
            ghostFrame.color = new Color(1, 1, 1, press ? .45f * fade * Mathf.Clamp01(move / 20) : 0);
            ghostFrame.rectTransform.anchoredPosition = new Vector2(topLeft.x + move, -topLeft.y);
            ghostFrame.rectTransform.sizeDelta = bottomRight - topLeft;
        }

        static void SetText(TMP_Text t, string s, float alpha)
        {
            bool on = alpha > .01f;
            if (t.enabled != on) t.enabled = on;
            if (!on) return;
            if (t.text != s) t.text = s;
            t.alpha = alpha;
        }

        void SpawnBurst(Vector2 at, Color color, int count, float spread)
        {
            for (int i = 0; i < count; i++)
            {
                Particle p = null;
                foreach (var q in particles) if (q.life <= 0) { p = q; break; }
                if (p == null)
                {
                    if (particles.Count >= 220) return;
                    p = new Particle { img = Img(fxLayer, "Particle", BreakoutArt.Pixel, Color.white) };
                    Place(p.img.rectTransform, new Vector2(0, 1), Vector2.zero, Vector2.one * 3, new Vector2(.5f, .5f));
                    particles.Add(p);
                }
                float a = Random.value * Mathf.PI * 2, s = 60 + Random.value * 260;
                p.p = at + new Vector2((Random.value - .5f) * spread, 0);
                p.v = new Vector2(Mathf.Cos(a) * s, Mathf.Sin(a) * s - 80);
                p.life = .6f + Random.value * .6f;
                p.img.color = color;
                p.img.enabled = true;
                p.img.rectTransform.sizeDelta = Vector2.one * (2 + Random.value * 3);
            }
        }

        void SpawnPopup(Vector2 at, string s, Color color)
        {
            PopupText p = null;
            foreach (var q in popups) if (q.life <= 0) { p = q; break; }
            if (p == null)
            {
                if (popups.Count >= 12) return;
                p = new PopupText { text = Text(fxLayer, "Popup", 15, Color.white, TextAlignmentOptions.Center) };
                p.text.fontStyle = FontStyles.Bold;
                Place(p.text.rectTransform, new Vector2(0, 1), Vector2.zero, new Vector2(140, 24), new Vector2(.5f, .5f));
                popups.Add(p);
            }
            p.text.text = s; p.text.color = color; p.text.enabled = true;
            p.p = at; p.life = 1;
        }

        // ---------------------------------------------------------------- sound

        void BuildSounds()
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0;
            clips[Cue.Wall] = Tone(300, .04f, 1, .18f);
            clips[Cue.Paddle] = Tone(440, .06f, 2, .3f);
            clips[Cue.Hit] = Tone(600, .05f, 1, .2f);
            clips[Cue.Break] = Tone(900, .07f, 1, .22f);
            clips[Cue.Steel] = Tone(1500, .03f, 1, .15f);
            clips[Cue.Energy] = Tone(1320, .15f, 2, .3f);
            clips[Cue.Bomb] = Tone(90, .4f, 3, .35f);
            clips[Cue.Bump] = Tone(1200, .06f, 0, .3f);
            clips[Cue.Folder] = Tone(1040, .12f, 2, .32f);
            clips[Cue.Lose] = Tone(160, .35f, 3, .3f);
            clips[Cue.Launch] = Tone(660, .08f, 2, .3f);
            clips[Cue.Level] = Tone(990, .3f, 2, .3f);
            clips[Cue.DragStart] = Tone(220, .15f, 0, .3f);
            clips[Cue.DragEnd] = Tone(520, .1f, 0, .25f);
            clips[Cue.Empty] = Tone(120, .12f, 3, .2f);
        }

        // wave: 0 sine, 1 square, 2 triangle, 3 saw
        static AudioClip Tone(float freq, float duration, int wave, float volume)
        {
            const int rate = 44100;
            int n = Mathf.CeilToInt(rate * (duration + .02f));
            var data = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)rate;
                float ph = Mathf.Repeat(freq * t, 1);
                float s = wave == 0 ? Mathf.Sin(ph * Mathf.PI * 2)
                        : wave == 1 ? (ph < .5f ? 1 : -1)
                        : wave == 2 ? 1 - 4 * Mathf.Abs(ph - .5f)
                        : 2 * ph - 1;
                float env = Mathf.Exp(-6 * t / duration) * Mathf.Clamp01(t * 800);
                data[i] = s * env * volume;
            }
            var clip = AudioClip.Create("Breakout tone " + freq, n, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        void Play(Cue cue)
        {
            if (audioSource == null || !clips.TryGetValue(cue, out var clip)) return;
            audioSource.pitch = sim != null && sim.Dragging ? .6f : 1;
            audioSource.PlayOneShot(clip);
        }

        // ---------------------------------------------------------------- UI helpers

        static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        static Image Img(Transform parent, string name, Sprite sprite, Color color)
        {
            var rt = NewRect(name, parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        TextMeshProUGUI Text(Transform parent, string name, float size, Color color, TextAlignmentOptions align)
        {
            var rt = NewRect(name, parent);
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            if (font != null) t.font = font;
            t.fontSize = size;
            t.color = color;
            t.alignment = align;
            t.raycastTarget = false;
            t.textWrappingMode = TextWrappingModes.Normal;
            t.overflowMode = TextOverflowModes.Overflow;
            return t;
        }

        static void Stretch(RectTransform rt, float inset = 0)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.one * inset; rt.offsetMax = -Vector2.one * inset;
        }

        static void Place(RectTransform rt, Vector2 anchor, Vector2 position, Vector2 size, Vector2? pivot = null)
        {
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = pivot ?? new Vector2(.5f, .5f);
            rt.sizeDelta = size;
            rt.anchoredPosition = position;
        }
    }

    public sealed class BreakoutDragSurface : MonoBehaviour, IPointerDownHandler, IPointerUpHandler,
        IInitializePotentialDragHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        public BreakoutApp app;
        public Vector2Int edges;   // resize edges of the shell handle this replaced (zero: move)
        public void OnPointerDown(PointerEventData e) { if (e.button == PointerEventData.InputButton.Left && app != null) app.Press(e, edges); }
        public void OnPointerUp(PointerEventData e) { if (e.button == PointerEventData.InputButton.Left && app != null) app.Release(e); }
        public void OnInitializePotentialDrag(PointerEventData e) => e.useDragThreshold = false;
        public void OnBeginDrag(PointerEventData e) { }
        public void OnDrag(PointerEventData e) { if (e.button == PointerEventData.InputButton.Left && app != null) app.DragTo(e); }
        public void OnEndDrag(PointerEventData e) { }
    }

    public sealed class BreakoutIconDrag : MonoBehaviour, IPointerDownHandler, IPointerUpHandler,
        IInitializePotentialDragHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        BreakoutApp app;
        DeskItem item;
        public void Bind(BreakoutApp owner, DeskItem target) { app = owner; item = target; }
        public void OnPointerDown(PointerEventData e) { if (e.button == PointerEventData.InputButton.Left && app != null) app.PressItem(item, e); }
        public void OnPointerUp(PointerEventData e) { if (e.button == PointerEventData.InputButton.Left && app != null) app.ReleaseItem(e); }
        public void OnInitializePotentialDrag(PointerEventData e) => e.useDragThreshold = false;
        public void OnBeginDrag(PointerEventData e) { }
        public void OnDrag(PointerEventData e) { if (e.button == PointerEventData.InputButton.Left && app != null) app.DragItem(e); }
        public void OnEndDrag(PointerEventData e) { }
    }

    public sealed class BreakoutShortcut : MonoBehaviour, IPointerClickHandler
    {
        public BreakoutApp app;
        public void OnPointerClick(PointerEventData e)
        {
            if (e.button == PointerEventData.InputButton.Left && e.clickCount == 2 && app != null) app.Show();
        }
    }
}
