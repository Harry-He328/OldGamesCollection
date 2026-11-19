using System.Collections.Generic;
using DesktopMinesweeper;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace OldGames.Collection
{
    /// <summary>
    /// The desktop as a platformer. A big mouse cursor pops out of the Start button and is
    /// steered with the keyboard. It never slides: it tumbles over its own corners, like a
    /// rigid polygon rolling on the floor. Desktop icons, the tops of open windows, the pet
    /// and the taskbar are the platforms; icons and windows can still be dragged with the
    /// mouse, which reshapes the level. Standing on a shortcut and pressing Down opens it.
    ///
    /// The cursor is the mouse. While it is a body the real pointer is hidden and mouse input is
    /// swallowed; entering a shortcut turns it back into the real pointer, so the game is played
    /// with the mouse. Closing or minimising that window, or pressing a move key while the desktop
    /// has the keyboard, drops the pointer back onto the desktop as a body.
    ///
    /// Coordinates here are desktop-local, origin at the bottom-left corner, y up.
    /// </summary>
    [DefaultExecutionOrder(300)]
    public sealed class DesktopCursorRunner : MonoBehaviour
    {
        public static DesktopCursorRunner Instance { get; private set; }

        // Art: the classic arrow, 16x24 pixels (y down), drawn at Scale desktop units per pixel.
        const float Scale = 1.8f;
        static readonly Vector2[] ArrowShape = { new Vector2(1, 1), new Vector2(1, 18), new Vector2(5, 14.2f), new Vector2(8, 21.5f), new Vector2(11, 20.3f), new Vector2(8, 13.2f), new Vector2(13.5f, 13.2f) };

        const float TaskbarHeight = 38;
        const float Gravity = 2200;
        const float JumpSpeed = 880;      // apex about 175 units: floor to the lowest icon row, or up onto a window from the tower
        const float RollRate = 15;        // rad/s while a direction is held: about 250 units/s
        const float RollDrive = 160;      // rad/s² towards RollRate
        const float AirMax = 260, AirAccel = 1400, AirDrag = 500, AirSpin = 11;
        const float IconHalfWidth = 24;   // platform on top of an icon's picture
        const float ContactEps = .6f;

        // Starting layout on the desktop icon grid (column, row), ordered like the games:
        // Breakout (game 1) is one jump from the Start button; Minesweeper (game 2) is at the top
        // of a zigzag tower in the left columns, next to Notepad, clear of the middle where windows
        // open; Pinball (game 3) sits alone on the far right, reached across the top of an open
        // window (or by moving things around).
        static readonly (string name, int col, int row)[] Layout =
        {
            ("Breakout Shortcut", 1, 6),
            ("Recycle Bin", 0, 5),
            ("Notepad", 1, 4),
            ("Minesweeper Shortcut", 2, 3),
            ("Computer", 1, 2),
            ("3D Pinball Shortcut", 10, 1),
        };
        static readonly Vector2 FirstCell = new Vector2(54, -49), CellSpacing = new Vector2(108, 87);

        struct Platform
        {
            public Object key;
            public float left, right, top;
            public bool droppable;
            public GameObject launch;   // shortcut opened by Down
        }

        enum Mode { Ground, Air, Enter, Become, Mouse }

        RectTransform desktop, layer, body, shadow, startButton;
        Image bodyImage, shadowImage, blocker;
        Vector2 tipLocal;               // arrow tip, relative to the centroid at scale 1 and angle 0
        DesktopWindow enteredWindow;
        float becomeTime;
        Vector2 becomeFrom;
        float becomeAngle;
        Vector2[] hull;                 // convex hull around the centroid, y up
        Vector2 spritePivot;
        AudioSource audioSource;
        AudioClip tick, thud, pop;

        Mode mode = Mode.Air;
        Vector2 pos, vel;               // centroid
        float angle, spin;              // radians, counter-clockwise; spin is rad/s
        float scale = 1;
        Platform ground;
        bool hasGround;
        float lastGrounded = -1, jumpBuffer, dropUntil, clock;
        Object dropKey;
        bool jumpCut;
        float enterTime;
        GameObject enterTarget;
        Vector2 enterFrom;
        GameObject highlighted;

        readonly List<Platform> platforms = new List<Platform>();
        readonly List<Rect> windowRects = new List<Rect>();
        readonly List<DesktopIcon> icons = new List<DesktopIcon>();
        readonly List<DesktopWindow> windows = new List<DesktopWindow>();
        DesktopPet pet;
        float rescan;
        readonly Vector3[] corners = new Vector3[4];
        Vector2[] world, prevWorld;

        // ---------------------------------------------------------------- install

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            if (Instance != null) return;
            RectTransform desk = null;
            foreach (var icon in FindObjectsByType<DesktopIcon>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (icon.name == "Recycle Bin") { desk = icon.transform.parent as RectTransform; break; }
            if (desk == null) return;
            var go = new GameObject("Desktop Cursor Runner");
            Instance = go.AddComponent<DesktopCursorRunner>();
            Instance.desktop = desk;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            Cursor.visible = true;
        }

        void OnDisable() => Cursor.visible = true;

        void Start()
        {
            // Start runs after every AfterSceneLoad installer, so the Breakout shortcut exists by now.
            foreach (Transform child in desktop)
                if (child.name == "Taskbar")
                    foreach (Transform t in child) if (t.name == "Start") startButton = (RectTransform)t;
            ArrangeIcons();
            BuildVisuals();
            BuildSounds();
            Rescan();
            Spawn();
        }

        void ArrangeIcons()
        {
            float width = desktop.rect.width;
            int maxColumn = Mathf.Max(0, Mathf.FloorToInt((width - FirstCell.x - 48 - 6) / CellSpacing.x));
            foreach (var (name, col, row) in Layout)
            {
                Transform t = desktop.Find(name);
                if (t is RectTransform rt)
                    rt.anchoredPosition = FirstCell + new Vector2(Mathf.Min(col, maxColumn) * CellSpacing.x, -row * CellSpacing.y);
            }
        }

        void BuildVisuals()
        {
            var layerGo = new GameObject("Cursor Runner Layer", typeof(RectTransform), typeof(Canvas));
            layer = (RectTransform)layerGo.transform;
            layer.SetParent(desktop, false);
            layer.anchorMin = Vector2.zero; layer.anchorMax = Vector2.one;
            layer.offsetMin = layer.offsetMax = Vector2.zero;
            var canvas = layerGo.GetComponent<Canvas>();
            var root = desktop.GetComponentInParent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = (root != null ? root.rootCanvas.sortingOrder : 0) + 20;

            // Convex hull of the arrow (the notch never touches a flat floor), around its area centroid.
            var h = ConvexHull(ArrowShape);
            Vector2 c = Centroid(h);
            hull = new Vector2[h.Length];
            for (int i = 0; i < h.Length; i++) hull[i] = new Vector2(h[i].x - c.x, c.y - h[i].y) * Scale;
            world = new Vector2[hull.Length];
            prevWorld = new Vector2[hull.Length];
            spritePivot = new Vector2(c.x / 16f, 1 - c.y / 24f);
            tipLocal = new Vector2(ArrowShape[0].x - c.x, c.y - ArrowShape[0].y) * Scale;

            // While the cursor is a body, mouse clicks go nowhere.
            layerGo.AddComponent<GraphicRaycaster>();
            var blockGo = new GameObject("Mouse Blocker", typeof(RectTransform), typeof(Image));
            var blockRt = (RectTransform)blockGo.transform;
            blockRt.SetParent(layer, false);
            blockRt.anchorMin = Vector2.zero; blockRt.anchorMax = Vector2.one;
            blockRt.offsetMin = blockRt.offsetMax = Vector2.zero;
            blocker = blockGo.GetComponent<Image>();
            blocker.color = Color.clear;
            blocker.raycastTarget = true;

            Sprite sprite = MakeArrow();
            shadow = MakeImage("Cursor Shadow", sprite, new Color(0, 0, 0, .3f), out shadowImage);
            body = MakeImage("Cursor", sprite, Color.white, out bodyImage);
        }

        RectTransform MakeImage(string name, Sprite sprite, Color color, out Image image)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            var rt = (RectTransform)go.transform;
            rt.SetParent(layer, false);
            rt.anchorMin = rt.anchorMax = Vector2.zero;
            rt.sizeDelta = new Vector2(16, 24) * Scale;
            rt.pivot = spritePivot;
            image = go.GetComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = false;
            return rt;
        }

        void BuildSounds()
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0;
            tick = Click("Cursor tick", .018f, 5200, .25f);
            thud = Click("Cursor land", .04f, 2400, .45f);
            pop = Click("Cursor pop", .06f, 900, .4f);
        }

        // ---------------------------------------------------------------- frame

        void Update()
        {
            if (desktop == null || body == null) return;
            if ((rescan -= Time.unscaledDeltaTime) <= 0) Rescan();
            CollectPlatforms();

            bool keys = HasKeyboard();
            var kb = Keyboard.current;
            int dir = 0;
            bool jumpPressed = false, jumpHeld = false, down = false;
            if (keys && kb != null)
            {
                if (kb.leftArrowKey.isPressed || kb.aKey.isPressed) dir--;
                if (kb.rightArrowKey.isPressed || kb.dKey.isPressed) dir++;
                jumpPressed = kb.spaceKey.wasPressedThisFrame || kb.upArrowKey.wasPressedThisFrame || kb.wKey.wasPressedThisFrame;
                jumpHeld = kb.spaceKey.isPressed || kb.upArrowKey.isPressed || kb.wKey.isPressed;
                down = kb.downArrowKey.wasPressedThisFrame || kb.sKey.wasPressedThisFrame;
            }
            if (mode == Mode.Mouse)
            {
                bool moved = keys && kb != null && (kb.leftArrowKey.wasPressedThisFrame || kb.aKey.wasPressedThisFrame ||
                                                    kb.rightArrowKey.wasPressedThisFrame || kb.dKey.wasPressedThisFrame || jumpPressed);
                bool closed = enteredWindow != null && !enteredWindow.gameObject.activeInHierarchy;
                if (moved || closed) { DropFromMouse(); jumpPressed = false; }
                else { SyncMouse(); return; }
            }
            if (jumpPressed) jumpBuffer = .12f;

            // Fixed small steps, so the tumble feels the same at any frame rate.
            float total = Mathf.Min(Time.unscaledDeltaTime, .25f);
            int steps = Mathf.Max(1, Mathf.CeilToInt(total * 120));
            float dt = total / steps;
            for (int i = 0; i < steps; i++)
            {
                clock += dt;
                jumpBuffer -= dt;
                switch (mode)
                {
                    case Mode.Ground: StepGround(dt, dir, down && i == 0); break;
                    case Mode.Air: StepAir(dt, dir, jumpHeld); break;
                    case Mode.Enter: StepEnter(dt); break;
                    case Mode.Become: StepBecome(dt); break;
                }
                if (mode == Mode.Mouse) break;
                if ((mode == Mode.Ground || mode == Mode.Air) && jumpBuffer > 0 && (mode == Mode.Ground || clock - lastGrounded < .08f))
                    Jump(dir);
            }

            KeepInside();
            UpdateHighlight();
            SyncMouse();
            Render();
        }

        // ---------------------------------------------------------------- the real mouse

        void SyncMouse()
        {
            bool mouse = mode == Mode.Mouse;
            var director = DialogueDirector.Instance;
            bool talking = director != null && director.IsPlaying;   // the pet's lines are clicked through
            Cursor.visible = mouse || talking;
            blocker.gameObject.SetActive(!mouse && !talking);
            body.gameObject.SetActive(!mouse);
            shadow.gameObject.SetActive(!mouse);
        }

        // Size the body shrinks to before it turns into the real pointer (roughly the OS arrow).
        float PointerScale()
        {
            var canvas = desktop.GetComponentInParent<Canvas>();
            float factor = canvas != null ? canvas.rootCanvas.scaleFactor : 1;
            float pixels = 20 * (Screen.dpi > 150 ? 2 : 1);
            return Mathf.Clamp(pixels / Mathf.Max(.01f, factor) / (20 * Scale), .25f, 1);
        }

        void StepBecome(float dt)
        {
            becomeTime += dt;
            const float Duration = .28f;
            float t = Mathf.Clamp01(becomeTime / Duration);
            float e = 1 - (1 - t) * (1 - t);
            Vector2 to = becomeFrom + new Vector2(0, 46);
            pos = Vector2.Lerp(becomeFrom, to, e);
            scale = Mathf.Lerp(.15f, PointerScale(), e);
            angle = Mathf.LerpAngle(becomeAngle * Mathf.Rad2Deg, 0, e) * Mathf.Deg2Rad;
            if (t < 1) return;

            // Hand over to the real pointer, right where the arrow's tip is.
            angle = 0;
            Vector2 tip = pos + tipLocal * scale;
            var mouse = Mouse.current;
            if (mouse != null)
            {
                Vector3 world = desktop.TransformPoint(tip + desktop.rect.min);
                mouse.WarpCursorPosition(RectTransformUtility.WorldToScreenPoint(null, world));
            }
            mode = Mode.Mouse;
            hasGround = false;
            spin = 0;
            vel = Vector2.zero;
        }

        void DropFromMouse()
        {
            // The pointer falls off the mouse and becomes a body again, tip where the pointer was.
            Vector2 tip = pos + tipLocal * scale;
            var mouse = Mouse.current;
            if (mouse != null &&
                RectTransformUtility.ScreenPointToLocalPointInRectangle(desktop, mouse.position.ReadValue(), null, out Vector2 local))
                tip = local - desktop.rect.min;
            tip.x = Mathf.Clamp(tip.x, 4, desktop.rect.width - 20);
            tip.y = Mathf.Clamp(tip.y, TaskbarHeight + 40, desktop.rect.height - 2);
            scale = PointerScale();
            angle = 0;
            pos = tip - tipLocal * scale;
            vel = Vector2.zero;
            spin = Random.value < .5f ? 1.5f : -1.5f;
            mode = Mode.Air;
            hasGround = false;
            enteredWindow = null;
            jumpBuffer = 0;
            Play(pop, .8f, .5f);
        }

        bool HasKeyboard()
        {
            if (DesktopPinballApp.OwnsKeyboard || OldGames.Breakout.BreakoutApp.OwnsKeyboard) return false;
            var director = DialogueDirector.Instance;
            if (director != null && director.IsPlaying) return false;
            var es = EventSystem.current;
            if (es != null && es.currentSelectedGameObject != null &&
                es.currentSelectedGameObject.TryGetComponent(out TMP_InputField field) && field.isFocused) return false;
            return true;
        }

        // ---------------------------------------------------------------- ground: tumbling

        void StepGround(float dt, int dir, bool down)
        {
            if (!FindPlatform(ground.key, out Platform now)) { Fall(); return; }
            // Ride the platform when it is dragged.
            pos += new Vector2(now.left - ground.left, now.top - ground.top);
            ground = now;
            lastGrounded = clock;

            if (down && mode == Mode.Ground)
            {
                if (ground.launch != null) { BeginEnter(ground.launch); return; }
                if (ground.droppable) { dropKey = ground.key; dropUntil = clock + .3f; Fall(); return; }
            }

            UpdateWorld();
            float s = ground.top;
            Contacts(s, out float minX, out float maxX);
            float cx = pos.x;

            // Gravity torque about the pivot the body would turn on.
            float pivotX = spin < 0 ? maxX : spin > 0 ? minX : cx > maxX ? maxX : cx < minX ? minX : cx;
            Vector2 r = pos - new Vector2(pivotX, s);
            float alpha = -r.x * Gravity / (64 + r.sqrMagnitude);
            if (dir != 0)
            {
                spin = Mathf.MoveTowards(spin, -dir * RollRate, RollDrive * dt) + alpha * dt * .25f;
            }
            else
            {
                bool resting = cx > minX && cx < maxX;
                if (resting && Mathf.Abs(spin) < 2.5f) spin = 0;
                else spin += alpha * dt;
                spin *= 1 - 3f * dt;
            }

            // Roll, switching the pivot each time another corner meets the surface.
            float remaining = spin * dt;
            for (int guard = 0; guard < 4 && Mathf.Abs(remaining) > 1e-6f; guard++)
            {
                UpdateWorld();
                Contacts(s, out minX, out maxX);
                bool cw = remaining < 0;
                Vector2 p = new Vector2(cw ? maxX : minX, s);
                float limit = float.MaxValue;
                for (int i = 0; i < world.Length; i++)
                {
                    Vector2 d = world[i] - p;
                    if (d.sqrMagnitude < 1) continue;
                    float a = Mathf.Atan2(d.y, d.x);
                    if (a <= 1e-4f || a >= Mathf.PI - 1e-4f) continue;
                    limit = Mathf.Min(limit, cw ? a : Mathf.PI - a);
                }
                float step = Mathf.Min(Mathf.Abs(remaining), limit);
                Rotate(p, cw ? -step : step);
                remaining -= cw ? -step : step;
                if (step >= limit - 1e-6f)
                {
                    // A new corner lands: the tumble loses some speed and clicks.
                    float keep = dir != 0 ? .93f : .4f;
                    spin *= keep; remaining *= keep;
                    Play(tick, .8f + Random.value * .4f);
                }
            }
            UpdateWorld();
            SnapTo(s);

            // Rolled off the edge?
            Contacts(s, out minX, out maxX);
            if (maxX < ground.left - 1 || minX > ground.right + 1) Fall();
        }

        void Contacts(float s, out float minX, out float maxX)
        {
            float low = float.MaxValue;
            for (int i = 0; i < world.Length; i++) low = Mathf.Min(low, world[i].y);
            minX = float.MaxValue; maxX = float.MinValue;
            for (int i = 0; i < world.Length; i++)
                if (world[i].y <= low + ContactEps) { minX = Mathf.Min(minX, world[i].x); maxX = Mathf.Max(maxX, world[i].x); }
        }

        void Rotate(Vector2 pivot, float by)
        {
            angle += by;
            pos = pivot + RotateVec(pos - pivot, by);
        }

        void SnapTo(float surface)
        {
            float low = float.MaxValue;
            for (int i = 0; i < world.Length; i++) low = Mathf.Min(low, world[i].y);
            pos.y += surface - low;
            UpdateWorld();
        }

        void Fall()
        {
            // Leave the surface with the velocity of the tumble around its lowest corner.
            UpdateWorld();
            int k = Lowest();
            Vector2 r = pos - world[k];
            vel = new Vector2(-spin * r.y, spin * r.x);
            mode = Mode.Air;
            hasGround = false;
        }

        void Jump(int dir)
        {
            jumpBuffer = 0;
            Vector2 roll = Vector2.zero;
            if (mode == Mode.Ground)
            {
                UpdateWorld();
                Vector2 r = pos - world[Lowest()];
                roll = new Vector2(-spin * r.y, spin * r.x);
            }
            vel = new Vector2(Mathf.Clamp(roll.x + dir * 140, -AirMax, AirMax), JumpSpeed);
            if (Mathf.Abs(spin) < 4) spin = dir != 0 ? -dir * 6 : (Random.value < .5f ? -2.5f : 2.5f);
            mode = Mode.Air;
            hasGround = false;
            lastGrounded = -1;
            jumpCut = false;
            Play(pop, 1.6f, .5f);
        }

        // ---------------------------------------------------------------- air

        void StepAir(float dt, int dir, bool jumpHeld)
        {
            if (!jumpHeld && !jumpCut && vel.y > 0) { vel.y *= .5f; jumpCut = true; }
            vel.y -= Gravity * dt;
            vel.y = Mathf.Max(vel.y, -1400);
            vel.x = Mathf.MoveTowards(vel.x, dir * AirMax, (dir != 0 ? AirAccel : AirDrag) * dt);
            if (dir != 0) spin = Mathf.MoveTowards(spin, -dir * AirSpin, 30 * dt);

            UpdateWorld();
            System.Array.Copy(world, prevWorld, world.Length);
            float prevLow = float.MaxValue;
            for (int i = 0; i < prevWorld.Length; i++) prevLow = Mathf.Min(prevLow, prevWorld[i].y);

            pos += vel * dt;
            angle += spin * dt;
            UpdateWorld();
            if (vel.y > 0) return;

            // Land on the highest surface the body crossed this frame (platforms are one-way).
            int best = -1; float bestTop = float.MinValue, bestLow = 0;
            for (int p = 0; p < platforms.Count; p++)
            {
                Platform pl = platforms[p];
                if (pl.key == dropKey && clock < dropUntil) continue;
                if (prevLow < pl.top - 2) continue;
                float low = float.MaxValue;
                for (int i = 0; i < world.Length; i++)
                    if (world[i].x >= pl.left && world[i].x <= pl.right) low = Mathf.Min(low, world[i].y);
                if (low < pl.top && pl.top > bestTop) { best = p; bestTop = pl.top; bestLow = low; }
            }
            if (best < 0) return;
            pos.y += bestTop - bestLow;
            ground = platforms[best];
            hasGround = true;
            mode = Mode.Ground;
            spin = Mathf.Clamp(spin * .6f + vel.x * -.02f, -RollRate, RollRate);
            Play(thud, .9f + Random.value * .2f, Mathf.Clamp01(-vel.y / 900f) * .8f + .2f);
            vel = Vector2.zero;
            lastGrounded = clock;
            UpdateWorld();
        }

        // ---------------------------------------------------------------- shortcuts

        void BeginEnter(GameObject target)
        {
            mode = Mode.Enter;
            enterTarget = target;
            enterTime = 0;
            enterFrom = pos;
            spin = 0;
        }

        void StepEnter(float dt)
        {
            enterTime += dt;
            const float In = .22f;
            Vector2 into = new Vector2((ground.left + ground.right) * .5f, ground.top - 18);
            float t = Mathf.Clamp01(enterTime / In);
            pos = Vector2.Lerp(enterFrom, into, t * t);
            scale = Mathf.Lerp(1, .15f, t);
            angle = Mathf.LerpAngle(angle * Mathf.Rad2Deg, 0, t) * Mathf.Deg2Rad;
            if (t < 1) return;

            // Open it, then rise out of the icon as the real pointer.
            var before = new HashSet<DesktopWindow>();
            foreach (var w in windows) if (w != null && w.gameObject.activeInHierarchy) before.Add(w);
            Open(enterTarget);
            Rescan();
            enteredWindow = null;
            foreach (var w in windows)
                if (w != null && w.gameObject.activeInHierarchy && !before.Contains(w)) enteredWindow = w;
            if (enteredWindow == null)   // already open: it was brought to the front
                foreach (var w in windows)
                    if (w != null && w.gameObject.activeInHierarchy &&
                        (enteredWindow == null || w.transform.GetSiblingIndex() > enteredWindow.transform.GetSiblingIndex()))
                        enteredWindow = w;
            becomeFrom = new Vector2(into.x, ground.top + 4);
            becomeAngle = angle;
            becomeTime = 0;
            mode = Mode.Become;
            Play(pop, 1, .7f);
        }

        void Open(GameObject target)
        {
            if (target == null) return;
            var es = EventSystem.current;
            if (es == null) return;
            RectTransform rt = (RectTransform)target.transform;
            var e = new PointerEventData(es)
            {
                button = PointerEventData.InputButton.Left,
                clickCount = 2,
                position = RectTransformUtility.WorldToScreenPoint(null, rt.position),
                pointerPress = target,
            };
            es.SetSelectedGameObject(target);
            ExecuteEvents.Execute(target, e, ExecuteEvents.pointerClickHandler);
        }

        void UpdateHighlight()
        {
            GameObject want = mode == Mode.Ground && hasGround ? ground.launch : null;
            if (want == highlighted) return;
            var es = EventSystem.current;
            if (es != null)
            {
                if (highlighted != null && es.currentSelectedGameObject == highlighted) es.SetSelectedGameObject(null);
                if (want != null && es.currentSelectedGameObject == null) es.SetSelectedGameObject(want);
            }
            highlighted = want;
        }

        // ---------------------------------------------------------------- spawn

        void Spawn()
        {
            Vector2 from = startButton != null ? RectIn(startButton).center : new Vector2(54, 19);
            pos = from;
            angle = 0;
            scale = .3f;
            vel = new Vector2(170, 720);
            spin = -6;
            mode = Mode.Air;
            hasGround = false;
            Play(pop, 1, .6f);
        }

        void KeepInside()
        {
            if (mode != Mode.Ground && mode != Mode.Air) return;
            UpdateWorld();
            float minX = float.MaxValue, maxX = float.MinValue, low = float.MaxValue;
            for (int i = 0; i < world.Length; i++) { minX = Mathf.Min(minX, world[i].x); maxX = Mathf.Max(maxX, world[i].x); low = Mathf.Min(low, world[i].y); }
            float w = desktop.rect.width;
            if (minX < 0) { pos.x -= minX; if (vel.x < 0) vel.x = -vel.x * .3f; }
            else if (maxX > w) { pos.x -= maxX - w; if (vel.x > 0) vel.x = -vel.x * .3f; }
            // Never sink under the taskbar.
            if (low < TaskbarHeight - 1 && mode == Mode.Air) { pos.y += TaskbarHeight - low; vel.y = Mathf.Max(0, vel.y); }
        }

        // ---------------------------------------------------------------- platforms

        void Rescan()
        {
            rescan = 1;
            icons.Clear();
            foreach (Transform child in desktop)
                if (child.TryGetComponent(out DesktopIcon icon)) icons.Add(icon);
            windows.Clear();
            foreach (Transform child in desktop)
                if (child.TryGetComponent(out DesktopWindow window)) windows.Add(window);
            if (pet == null) pet = FindFirstObjectByType<DesktopPet>(FindObjectsInactive.Include);
        }

        void CollectPlatforms()
        {
            platforms.Clear();
            windowRects.Clear();
            float width = desktop.rect.width;
            platforms.Add(new Platform { key = desktop, left = -100, right = width + 100, top = TaskbarHeight });

            foreach (var window in windows)
            {
                if (window == null || !window.gameObject.activeInHierarchy) continue;
                Rect r = RectIn((RectTransform)window.transform);
                windowRects.Add(r);
                platforms.Add(new Platform { key = window, left = r.xMin + 2, right = r.xMax - 2, top = r.yMax, droppable = true });
            }

            foreach (var icon in icons)
            {
                if (icon == null || !icon.gameObject.activeInHierarchy) continue;
                RectTransform picture = icon.transform.Find("Icon") as RectTransform;
                Rect r = RectIn(picture != null ? picture : (RectTransform)icon.transform);
                Vector2 top = new Vector2(r.center.x, r.yMax);
                bool covered = false;
                foreach (var w in windowRects) if (w.Contains(top)) { covered = true; break; }
                if (covered) continue;   // an icon hidden under a window is not a platform
                platforms.Add(new Platform
                {
                    key = icon, left = r.center.x - IconHalfWidth, right = r.center.x + IconHalfWidth, top = r.yMax,
                    droppable = true, launch = icon.GetComponent<IPointerClickHandler>() != null ? icon.gameObject : null,
                });
            }

            if (pet != null && pet.OnDesktop && pet.isActiveAndEnabled)
            {
                Rect r = RectIn(pet.Rect);
                platforms.Add(new Platform { key = pet, left = r.center.x - r.width * .35f, right = r.center.x + r.width * .35f, top = r.yMax - 4, droppable = true });
            }
        }

        bool FindPlatform(Object key, out Platform found)
        {
            foreach (var p in platforms) if (p.key == key) { found = p; return true; }
            found = default;
            return false;
        }

        Rect RectIn(RectTransform rt)
        {
            rt.GetWorldCorners(corners);
            Vector2 a = desktop.InverseTransformPoint(corners[0]), b = desktop.InverseTransformPoint(corners[2]);
            Vector2 o = desktop.rect.min;
            return Rect.MinMaxRect(a.x - o.x, a.y - o.y, b.x - o.x, b.y - o.y);
        }

        // ---------------------------------------------------------------- render

        void Render()
        {
            body.anchoredPosition = pos;
            body.localEulerAngles = new Vector3(0, 0, angle * Mathf.Rad2Deg);
            body.localScale = Vector3.one * scale;
            shadow.anchoredPosition = pos + new Vector2(2.5f, -2.5f);
            shadow.localEulerAngles = body.localEulerAngles;
            shadow.localScale = body.localScale;
            if ((mode == Mode.Ground || mode == Mode.Air) && scale < 1) scale = Mathf.MoveTowards(scale, 1, Time.unscaledDeltaTime * 4);
            // The layer has its own sorting canvas, so it draws above windows without touching sibling order.
        }

        // ---------------------------------------------------------------- geometry

        void UpdateWorld()
        {
            for (int i = 0; i < hull.Length; i++) world[i] = pos + RotateVec(hull[i] * scale, angle);
        }

        int Lowest()
        {
            int k = 0;
            for (int i = 1; i < world.Length; i++) if (world[i].y < world[k].y) k = i;
            return k;
        }

        static Vector2 RotateVec(Vector2 v, float a)
        {
            float c = Mathf.Cos(a), s = Mathf.Sin(a);
            return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c);
        }

        static Vector2[] ConvexHull(Vector2[] points)
        {
            var p = new List<Vector2>(points);
            p.Sort((a, b) => a.x != b.x ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y));
            var h = new List<Vector2>();
            for (int pass = 0; pass < 2; pass++)
            {
                int start = h.Count;
                for (int i = 0; i < p.Count; i++)
                {
                    Vector2 q = pass == 0 ? p[i] : p[p.Count - 1 - i];
                    while (h.Count >= start + 2 && Cross(h[h.Count - 2], h[h.Count - 1], q) <= 0) h.RemoveAt(h.Count - 1);
                    h.Add(q);
                }
                h.RemoveAt(h.Count - 1);
            }
            return h.ToArray();
        }

        static float Cross(Vector2 o, Vector2 a, Vector2 b) => (a.x - o.x) * (b.y - o.y) - (a.y - o.y) * (b.x - o.x);

        static Vector2 Centroid(Vector2[] poly)
        {
            float area = 0; Vector2 c = Vector2.zero;
            for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
            {
                float f = poly[j].x * poly[i].y - poly[i].x * poly[j].y;
                area += f;
                c += (poly[j] + poly[i]) * f;
            }
            return c / (3 * area);
        }

        // ---------------------------------------------------------------- art and sound

        static Sprite MakeArrow()
        {
            const int Res = 4;   // texture pixels per art pixel
            int w = 16 * Res, h = 24 * Res;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = "Desktop cursor runner" };
            var px = new Color[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    Color acc = default;
                    for (int sy = 0; sy < 3; sy++)
                        for (int sx = 0; sx < 3; sx++)
                        {
                            Color c = ArrowPaint((x + (sx + .5f) / 3f) / Res, (y + (sy + .5f) / 3f) / Res);
                            acc += new Color(c.r * c.a, c.g * c.a, c.b * c.a, c.a);
                        }
                    acc /= 9f;
                    if (acc.a > 1e-4f) acc = new Color(acc.r / acc.a, acc.g / acc.a, acc.b / acc.a, acc.a);
                    px[(h - 1 - y) * w + x] = acc;
                }
            tex.SetPixels(px);
            tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(.5f, .5f), 100);
        }

        static Color ArrowPaint(float x, float y)
        {
            var poly = ArrowShape;
            bool inside = false;
            for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
                if ((poly[i].y > y) != (poly[j].y > y) && x < (poly[j].x - poly[i].x) * (y - poly[i].y) / (poly[j].y - poly[i].y) + poly[i].x)
                    inside = !inside;
            if (!inside) return Color.clear;
            var p = new Vector2(x, y);
            float d = float.MaxValue;
            for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
            {
                Vector2 a = poly[j], b = poly[i], ab = b - a;
                float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
                d = Mathf.Min(d, Vector2.Distance(p, a + ab * t));
            }
            return d < 1.05f ? Color.black : Color.white;
        }

        static AudioClip Click(string name, float length, float freq, float volume)
        {
            const int Rate = 44100;
            int n = Mathf.CeilToInt(length * Rate);
            var data = new float[n];
            var rng = new System.Random(name.GetHashCode());
            float lp = 0;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float env = Mathf.Exp(-t / (length * .25f));
                float noise = (float)rng.NextDouble() * 2 - 1;
                lp += (noise - lp) * .35f;
                data[i] = (Mathf.Sin(2 * Mathf.PI * freq * t) * .6f + lp * .5f) * env * volume;
            }
            var clip = AudioClip.Create(name, n, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        void Play(AudioClip clip, float pitch = 1, float volume = 1)
        {
            if (audioSource == null || clip == null) return;
            audioSource.pitch = pitch;
            audioSource.PlayOneShot(clip, volume);
        }
    }
}
