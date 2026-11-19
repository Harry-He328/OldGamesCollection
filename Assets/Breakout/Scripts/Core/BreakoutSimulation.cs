using System;
using System.Collections.Generic;
using UnityEngine;

namespace OldGames.Breakout
{
    public enum BrickKind { Normal, Steel, Energy, Bomb, Ghost, Target }

    public enum Cue { Wall, Paddle, Hit, Break, Steel, Energy, Bomb, Bump, Folder, Lose, Launch, Level, DragStart, DragEnd, Empty }

    public sealed class Brick
    {
        public BrickKind kind;
        public float x, y, w, h, baseX;
        public int hp, max;
        public int row;
        public float flash, lastHit = -1;
        public bool moving;
        public float moveLeft, moveRight, moveSpeed, movePhase;
        public bool Alive => hp > 0;
        public bool Breakable => kind != BrickKind.Steel;
    }

    public sealed class DeskItem
    {
        public DeskSpec spec;
        public DeskKind Kind => spec.kind;
        public Rect rect;          // hit area in desktop coordinates (y down), set by the presentation every frame
        public bool bound;         // moves with the window; local is its centre in content space
        public Vector2 local;
        public bool placed;        // moved by the player; pos is its desktop centre
        public Vector2 pos;
        public bool held;          // being dragged with the mouse right now
        public int hp, max;
        public float flash, lastHit = -1;
        public bool Alive => hp > 0;
        public Vector2 Center => rect.center;
        public float Radius => Mathf.Min(rect.width, rect.height) * .5f;
    }

    /// <summary>
    /// Breakout rules with no Unity UI dependencies. All positions use desktop coordinates with
    /// the origin at the desktop's top-left corner and y pointing down. Bricks and the paddle live
    /// in the window's content space; the ball and desktop icons live in desktop space, so moving
    /// the window moves bricks and paddle but never the ball.
    /// </summary>
    public sealed class BreakoutSimulation
    {
        public const float Slow = .25f;           // bullet-time scale
        public const float Drain = 1f;            // energy per real second while dragging: one drag lasts at most 1 s
        public const float Regen = .3f;           // energy per real second while not dragging: about 3.3 s from empty
        public const float HitGain = .2f;         // energy for every bounce (wall, paddle, brick, icon), on top of Regen
        public const float EnergyBrickGain = .3f; // extra, on top of HitGain, for breaking a green energy brick
        public const float MinStart = .15f;
        public const int Columns = 10;

        public enum Phase { Play, Over, Won }

        public Phase State { get; private set; } = Phase.Play;
        public int Score { get; private set; }
        public int Lives { get; private set; } = 3;
        public int Level { get; private set; } = 1;
        public LevelSpec Spec { get; private set; } = BreakoutLevels.All[0];
        public float Energy { get; private set; } = 1;
        public float TimeScale { get; private set; } = 1;
        public float GameTime { get; private set; }
        public float Banner { get; private set; }
        public float LevelTime { get; private set; }   // real seconds of play on this level
        public float Clearing { get; private set; }    // counts down after a goal is hit, then the next level starts
        public bool Moved { get; set; }                // the player dragged an icon on this level
        public int LevelCount => BreakoutLevels.All.Length;
        public float Shake { get; private set; }
        public float EmptyFlash { get; private set; }
        public bool Dragging { get; private set; }
        public bool Stuck { get; private set; } = true;
        public bool GhostSolid => TimeScale < .6f;
        public float SlowAmount => Mathf.Clamp01((1 - TimeScale) / (1 - Slow));

        public float Width { get; private set; }
        public float Height { get; private set; }
        public float BrickWidth { get; private set; }
        public float BrickHeight { get; private set; }
        public float Gap { get; private set; } = 4;
        public float PaddleX => Width * .5f;
        public float PaddleY => Height - 34;
        public float PaddleWidth => Width * .24f;
        public const float PaddleHeight = 12;

        public Vector2 Ball;               // desktop space
        public Vector2 Velocity;
        public const float BallRadius = 7;
        public float Speed { get; private set; }

        public readonly List<Brick> Bricks = new List<Brick>();
        public readonly List<DeskItem> Desk = new List<DeskItem>();

        public event Action<Cue> Sound;
        public event Action<Vector2, Color, int, float> Burst;   // desktop position, colour, count, spread
        public event Action<Vector2, string, Color> Popup;
        public event Action<DeskItem> DeskItemDestroyed;
        public event Action LevelBuilt;

        Vector2 local;        // ball in content space during a step
        Vector2 origin;       // content origin for the current sub-step
        float lastWall = -1, lastPaddle = -1, lastGain = -1;

        public void Layout(float width, float height)
        {
            if (Width > 0 && (Mathf.Abs(width - Width) > .5f || Mathf.Abs(height - Height) > .5f))
            {
                float kx = width / Width, ky = height / Height;
                foreach (var b in Bricks)
                {
                    b.x *= kx; b.baseX *= kx; b.w *= kx; b.y *= ky; b.h *= ky;
                    b.moveLeft *= kx; b.moveRight *= kx;
                }
                BrickWidth *= kx; BrickHeight *= ky; Gap *= kx;
            }
            Width = width; Height = height;
            Speed = LevelSpeed();
        }

        float LevelSpeed() => Mathf.Min(540, 330 + 22 * (Level - 1)) * (Height / 620f);

        public void NewGame()
        {
            State = Phase.Play; Score = 0; Lives = 3; Level = 1; Energy = 1; Banner = 2.5f;
            Dragging = false;
            BuildLevel(1);
            ResetBall();
        }

        public void GotoLevel(int n)
        {
            State = Phase.Play; Level = Mathf.Clamp(n, 1, LevelCount); Energy = 1; Banner = 2.5f; Dragging = false;
            BuildLevel(Level);
            ResetBall();
        }

        void ResetBall()
        {
            Stuck = true;
            Speed = LevelSpeed();
        }

        public void Launch()
        {
            if (!Stuck || State != Phase.Play) return;
            Stuck = false;
            float a = UnityEngine.Random.Range(-.4f, .4f);
            Velocity = new Vector2(Mathf.Sin(a), -Mathf.Cos(a)) * Speed;
            Sound?.Invoke(Cue.Launch);
        }

        public bool TryBeginDrag()
        {
            if (State != Phase.Play || Stuck) return false;
            if (Energy < MinStart) { EmptyFlash = 1; Sound?.Invoke(Cue.Empty); return false; }
            Dragging = true;
            Sound?.Invoke(Cue.DragStart);
            return true;
        }

        public void EndDrag()
        {
            if (!Dragging) return;
            Dragging = false;
            Sound?.Invoke(Cue.DragEnd);
        }

        void BuildLevel(int n)
        {
            var spec = BreakoutLevels.All[Mathf.Clamp(n, 1, LevelCount) - 1];
            Spec = spec;
            LevelTime = 0; Clearing = 0; Moved = false; goalHit = false;
            Bricks.Clear();
            const float margin = 14;
            Gap = 4;
            BrickWidth = (Width - 2 * margin - (Columns - 1) * Gap) / Columns;
            BrickHeight = Mathf.Clamp(Height * .03f, 14, 20);
            for (int r = 0; r < spec.map.Length; r++)
            {
                string row = spec.map[r];
                bool moving = Array.IndexOf(spec.move, r) >= 0;
                int minC = 99, maxC = -1;
                for (int c = 0; c < Columns && c < row.Length; c++)
                    if (row[c] != '.') { minC = Mathf.Min(minC, c); maxC = Mathf.Max(maxC, c); }
                for (int c = 0; c < Columns && c < row.Length; c++)
                {
                    char ch = row[c];
                    if (ch == '.') continue;
                    var b = new Brick { kind = BrickKind.Normal, hp = 1, row = r };
                    if (ch >= '1' && ch <= '3') b.hp = ch - '0';
                    else if (ch == '#') { b.kind = BrickKind.Steel; b.hp = int.MaxValue; }
                    else if (ch == 'E') b.kind = BrickKind.Energy;
                    else if (ch == 'B') b.kind = BrickKind.Bomb;
                    else if (ch == 'G') b.kind = BrickKind.Ghost;
                    else if (ch == 'T') b.kind = BrickKind.Target;
                    b.max = b.hp;
                    b.x = b.baseX = margin + c * (BrickWidth + Gap);
                    b.y = 48 + r * (BrickHeight + Gap);
                    b.w = BrickWidth; b.h = BrickHeight;
                    if (moving)
                    {
                        b.moving = true;
                        b.moveLeft = minC * (BrickWidth + Gap);
                        b.moveRight = (Columns - 1 - maxC) * (BrickWidth + Gap);
                        b.moveSpeed = .8f + (r % 3) * .35f;
                        b.movePhase = r * 1.7f;
                    }
                    Bricks.Add(b);
                }
            }
            Desk.Clear();
            foreach (var d in spec.desk)
            {
                bool breakable = d.kind == DeskKind.Folder || d.kind == DeskKind.File;
                var item = new DeskItem { spec = d, hp = breakable ? 1 : int.MaxValue, max = 1 };
                if (d.inWindow) { item.bound = true; item.local = new Vector2(d.fx * Width, d.fy * Height); }
                Desk.Add(item);
            }
            LevelBuilt?.Invoke();
        }

        bool goalHit;

        /// <summary>Advances one frame. oldOrigin/newOrigin are the content area's top-left corner
        /// before and after this frame's window movement. Returns true if energy ran out.</summary>
        public bool Step(float realDt, Vector2 oldOrigin, Vector2 newOrigin)
        {
            TimeScale += ((Dragging ? Slow : 1) - TimeScale) * Mathf.Min(1, realDt * 18);

            bool emptied = false;
            if (Dragging)
            {
                Energy -= Drain * realDt;
                if (Energy <= 0) { Energy = 0; EmptyFlash = 1; Dragging = false; emptied = true; Sound?.Invoke(Cue.Empty); }
            }
            else if (State == Phase.Play && !Stuck)
                Energy = Mathf.Min(1, Energy + Regen * realDt);
            if (State == Phase.Play && !Stuck && Clearing <= 0) LevelTime += realDt;

            float gdt = realDt * TimeScale;

            if (State == Phase.Play && !Stuck)
                foreach (var b in Bricks)
                    if (b.moving)
                        b.x = b.baseX - b.moveLeft + (b.moveLeft + b.moveRight) * (Mathf.Sin(GameTime * b.moveSpeed + b.movePhase) + 1) * .5f;

            if (Clearing > 0)
            {
                // Short pause after a goal so the hit can be seen before the next level.
                Clearing -= realDt;
                if (Clearing <= 0) NextLevel();
            }
            else if (Stuck || State != Phase.Play)
            {
                Ball = newOrigin + new Vector2(PaddleX, PaddleY - BallRadius - 1);
            }
            else
            {
                // Sub-step in content space, interpolating window movement and ball movement together
                // so a fast drag cannot tunnel through a wall, brick or the paddle.
                local = Ball - oldOrigin;
                Vector2 dw = newOrigin - oldOrigin;
                int n = Mathf.Clamp(Mathf.CeilToInt(Mathf.Max(dw.magnitude, Speed * gdt) / (BallRadius * .5f)), 1, 400);
                float sdt = gdt / n;
                bool lost = false;
                for (int i = 1; i <= n; i++)
                {
                    origin = oldOrigin + dw * (i / (float)n);
                    local += Velocity * sdt - dw / n;
                    GameTime += sdt;
                    Collide();
                    if (local.y - BallRadius > Height) { lost = true; break; }
                }
                Ball = local + newOrigin;
                if (goalHit) { Clearing = 1.2f; Dragging = false; Score += 100 * Level; Sound?.Invoke(Cue.Level); }
                else if (lost) LoseLife();
            }

            foreach (var b in Bricks) b.flash = Mathf.Max(0, b.flash - realDt * 6);
            foreach (var d in Desk) d.flash = Mathf.Max(0, d.flash - realDt * 5);
            Shake = Mathf.Max(0, Shake - realDt * 4);
            EmptyFlash = Mathf.Max(0, EmptyFlash - realDt * 2);
            Banner = Mathf.Max(0, Banner - realDt);
            return emptied;
        }

        /// <summary>Skips straight to the end: all levels cleared. A testing shortcut.</summary>
        public void Win()
        {
            Dragging = false;
            Clearing = 0;
            Level = LevelCount;
            State = Phase.Won;
            Stuck = true;
        }

        void NextLevel()
        {
            Dragging = false;
            if (Level >= LevelCount) { State = Phase.Won; Stuck = true; return; }
            Level++; Banner = 2.5f; Energy = 1;
            BuildLevel(Level);
            ResetBall();
        }

        void LoseLife()
        {
            Lives--;
            Shake = 1;
            Burst?.Invoke(Ball, new Color(1, .36f, .43f), 30, 6);
            Sound?.Invoke(Cue.Lose);
            Dragging = false;
            if (Lives <= 0) { State = Phase.Over; Stuck = true; }
            else ResetBall();
        }

        // ---------- collision ----------

        void FixVelocity()
        {
            float s = Velocity.magnitude;
            if (s < 1e-4f) Velocity = new Vector2(0, -1);
            Velocity = Velocity.normalized * Speed;
            float minVy = .28f * Speed;
            if (Mathf.Abs(Velocity.y) < minVy)
            {
                Velocity.y = (Velocity.y < 0 ? -1 : 1) * minVy;
                Velocity.x = (Velocity.x < 0 ? -1 : 1) * Mathf.Sqrt(Speed * Speed - minVy * minVy);
            }
        }

        void Reflect(Vector2 n)
        {
            float vn = Vector2.Dot(Velocity, n);
            if (vn < 0) Velocity -= 2 * vn * n;
            FixVelocity();
        }

        // Circle against rectangle in content space: push out and reflect. Returns the contact normal.
        bool HitRect(float rx, float ry, float rw, float rh, out Vector2 normal)
        {
            normal = default;
            const float R = BallRadius;
            if (local.x + R < rx || local.x - R > rx + rw || local.y + R < ry || local.y - R > ry + rh) return false;
            float cx = Mathf.Clamp(local.x, rx, rx + rw), cy = Mathf.Clamp(local.y, ry, ry + rh);
            float dx = local.x - cx, dy = local.y - cy, d2 = dx * dx + dy * dy;
            if (d2 >= R * R) return false;
            float pen;
            if (d2 > 1e-9f)
            {
                float d = Mathf.Sqrt(d2);
                normal = new Vector2(dx / d, dy / d); pen = R - d;
            }
            else
            {
                float l = local.x - rx, r = rx + rw - local.x, t = local.y - ry, b = ry + rh - local.y;
                float m = Mathf.Min(Mathf.Min(l, r), Mathf.Min(t, b));
                if (m == l) { normal = new Vector2(-1, 0); pen = l + R; }
                else if (m == r) { normal = new Vector2(1, 0); pen = r + R; }
                else if (m == t) { normal = new Vector2(0, -1); pen = t + R; }
                else { normal = new Vector2(0, 1); pen = b + R; }
            }
            local += normal * pen;
            Reflect(normal);
            return true;
        }

        // Every bounce refills the ring. A corner touching two surfaces in the same instant counts once.
        void Bounced()
        {
            if (GameTime - lastGain < .03f) return;
            lastGain = GameTime;
            Energy = Mathf.Min(1, Energy + HitGain);
        }

        void Collide()
        {
            const float R = BallRadius;
            bool wall = false;
            if (local.x < R) { local.x = R; if (Velocity.x < 0) Velocity.x = -Velocity.x; wall = true; }
            if (local.x > Width - R) { local.x = Width - R; if (Velocity.x > 0) Velocity.x = -Velocity.x; wall = true; }
            if (local.y < R) { local.y = R; if (Velocity.y < 0) Velocity.y = -Velocity.y; wall = true; }
            if (wall)
            {
                Bounced();
                FixVelocity();
                if (GameTime - lastWall > .06f) { lastWall = GameTime; Sound?.Invoke(Cue.Wall); }
            }

            // Paddle: fixed at the bottom centre of the window.
            if (HitRect(PaddleX - PaddleWidth * .5f, PaddleY, PaddleWidth, PaddleHeight, out Vector2 pn))
            {
                Bounced();
                if (pn.y < -.3f)
                {
                    float o = Mathf.Clamp((local.x - PaddleX) / (PaddleWidth * .5f), -1, 1);
                    float a = o * 1.05f;
                    Velocity = new Vector2(Mathf.Sin(a), -Mathf.Cos(a)) * Speed;
                }
                if (GameTime - lastPaddle > .08f) { lastPaddle = GameTime; Sound?.Invoke(Cue.Paddle); }
            }

            bool ghosts = GhostSolid;
            foreach (var b in Bricks)
            {
                if (!b.Alive) continue;
                if (b.kind == BrickKind.Ghost && !ghosts) continue;
                if (HitRect(b.x, b.y, b.w, b.h, out _))
                {
                    Bounced();
                    if (GameTime - b.lastHit > .05f) { b.lastHit = GameTime; Damage(b); }
                    break;
                }
            }

            // Desktop icons stay fixed in desktop space; convert to the current content space.
            foreach (var d in Desk)
            {
                if (!d.Alive) continue;
                if (d.Kind == DeskKind.Disc)
                {
                    Vector2 c = d.Center - origin;
                    float rr = d.Radius + R;
                    Vector2 delta = local - c;
                    if (delta.sqrMagnitude < rr * rr)
                    {
                        Vector2 n = delta.sqrMagnitude > 1e-6f ? delta.normalized : Vector2.up;
                        local = c + n * rr;
                        Reflect(n);
                        Bounced();
                        if (GameTime - d.lastHit > .05f)
                        {
                            d.lastHit = GameTime; d.flash = 1; Score += 5;
                            Burst?.Invoke(d.Center + n * d.Radius, new Color(.75f, .85f, 1f), 6, 4);
                            Sound?.Invoke(Cue.Bump);
                        }
                    }
                }
                else
                {
                    Rect r = ContentRect(d);
                    if (HitRect(r.x, r.y, r.width, r.height, out _))
                    {
                        Bounced();
                        if (GameTime - d.lastHit > .05f) { d.lastHit = GameTime; DamageDesk(d); }
                        break;
                    }
                }
            }
        }

        Rect ContentRect(DeskItem d)
        {
            Rect r = d.rect;
            if (d.bound) return new Rect(d.local.x - r.width * .5f, d.local.y - r.height * .5f, r.width, r.height);
            return new Rect(r.x - origin.x, r.y - origin.y, r.width, r.height);
        }

        public Vector2 BrickCenter(Brick b) => origin + new Vector2(b.x + b.w * .5f, b.y + b.h * .5f);

        public static Color BrickColor(Brick b)
        {
            switch (b.kind)
            {
                case BrickKind.Steel: return new Color(.62f, .65f, .7f);
                case BrickKind.Energy: return new Color(.25f, .75f, .3f);
                case BrickKind.Bomb: return new Color(.86f, .24f, .18f);
                case BrickKind.Ghost: return new Color(.72f, .5f, 1f);
                case BrickKind.Target: return new Color(1f, .78f, .18f);
            }
            return RowColors[b.row % RowColors.Length];
        }

        // Luna-inspired palette: orange, olive, silver, green, yellow, rose.
        static readonly Color[] RowColors =
        {
            new Color(.96f, .6f, .2f), new Color(.72f, .76f, .38f), new Color(.85f, .86f, .9f),
            new Color(.36f, .72f, .3f), new Color(1f, .82f, .3f), new Color(.93f, .45f, .5f),
            new Color(.55f, .8f, .9f), new Color(.98f, .7f, .45f), new Color(.6f, .66f, .4f),
        };

        void Damage(Brick b)
        {
            b.flash = 1;
            if (b.kind == BrickKind.Steel) { Sound?.Invoke(Cue.Steel); return; }
            b.hp--;
            Shake = Mathf.Min(1, Shake + .2f);
            Vector2 c = BrickCenter(b);
            Color col = BrickColor(b);
            if (b.hp > 0)
            {
                Score += 5;
                Burst?.Invoke(c, Color.Lerp(col, Color.white, .5f), 5, b.w);
                Sound?.Invoke(Cue.Hit);
                return;
            }
            Score += 10 * Level * b.max;
            Burst?.Invoke(c, col, 16, b.w);
            Sound?.Invoke(Cue.Break);
            if (b.kind == BrickKind.Energy)
            {
                Energy = Mathf.Min(1, Energy + EnergyBrickGain);
                Popup?.Invoke(c, "+ENERGY", new Color(.4f, 1f, .5f));
                Sound?.Invoke(Cue.Energy);
            }
            if (b.kind == BrickKind.Bomb) Explode(b);
            if (b.kind == BrickKind.Target) Goal(c);
        }

        void Goal(Vector2 at)
        {
            if (goalHit) return;
            goalHit = true;
            Shake = 1;
            Burst?.Invoke(at, new Color(1f, .85f, .3f), 50, 40);
            Popup?.Invoke(at, "TARGET!", new Color(1f, .9f, .4f));
        }

        void Explode(Brick bomb)
        {
            Shake = 1;
            Burst?.Invoke(BrickCenter(bomb), new Color(1, .7f, .28f), 40, bomb.w * 2);
            Sound?.Invoke(Cue.Bomb);
            float rx = (BrickWidth + Gap) * 1.15f, ry = (BrickHeight + Gap) * 1.15f;
            float cx = bomb.x + bomb.w * .5f, cy = bomb.y + bomb.h * .5f;
            foreach (var o in Bricks)
            {
                if (o == bomb || !o.Alive || !o.Breakable) continue;
                if (Mathf.Abs(o.x + o.w * .5f - cx) <= rx && Mathf.Abs(o.y + o.h * .5f - cy) <= ry)
                {
                    o.hp = 1;   // bombs destroy their neighbours outright and chain into other bombs
                    Damage(o);
                }
            }
        }

        void DamageDesk(DeskItem d)
        {
            d.flash = 1;
            if (d.Kind == DeskKind.Locked) { Sound?.Invoke(Cue.Steel); return; }
            d.hp--;
            Shake = Mathf.Min(1, Shake + .3f);
            var gold = new Color(1f, .8f, .3f);
            if (d.hp > 0)
            {
                Burst?.Invoke(d.Center, gold, 6, d.rect.width);
                Sound?.Invoke(Cue.Hit);
                return;
            }
            Score += 50 * Level;
            Burst?.Invoke(d.Center, gold, 24, d.rect.width);
            Popup?.Invoke(new Vector2(d.Center.x, d.rect.yMin), "+" + 50 * Level, gold);
            Sound?.Invoke(Cue.Folder);
            DeskItemDestroyed?.Invoke(d);
            if (d.spec.goal) Goal(d.Center);
        }
    }
}
