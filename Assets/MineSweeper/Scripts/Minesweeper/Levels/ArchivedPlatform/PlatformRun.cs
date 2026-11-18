using UnityEngine;

namespace DesktopMinesweeper
{
    // Bottom-left origin, in cells. Position is the center of the character's feet.
    internal sealed class PlatformRun
    {
        private const float HalfWidth = .46f;
        private const float Height = .94f;
        private const float Speed = 7.2f;
        private const float GroundAcceleration = 100f;
        private const float AirAcceleration = 82f;
        private const float Braking = 130f;
        private const float Gravity = 27f;
        private const float JumpSpeed = 10.12f;
        private const float StepTime = 1f / 120f;
        private const float Edge = .0001f;
        private readonly bool[,] solid;
        private readonly Vector2 start;
        private float velocity;
        private float horizontalSpeed;
        private float jumpBuffer;
        private float groundGrace;
        private float accumulatedTime;

        public int Width { get; }
        public int BoardHeight { get; }
        public Vector2 Position { get; private set; }
        public Vector2 Goal { get; }
        public bool ReachedGoal { get; private set; }
        public bool Fell { get; private set; }

        public PlatformRun(BoardData board, Vector2Int source, Vector2Int goal)
        {
            Width = board.Width;
            BoardHeight = board.Height;
            solid = new bool[Width, BoardHeight];
            foreach (CellData cell in board.Cells)
                solid[cell.X, BoardHeight - 1 - cell.Y] = !cell.IsRevealed;
            solid[source.x, BoardHeight - 1 - source.y] = false;
            start = new Vector2(source.x + .5f, BoardHeight - 1 - source.y);
            Goal = new Vector2(goal.x + .5f, BoardHeight - .5f - goal.y);
            Retry();
        }

        public bool IsSolid(CellData cell) => solid[cell.X, BoardHeight - 1 - cell.Y];

        public void Retry()
        {
            Position = start;
            horizontalSpeed = velocity = jumpBuffer = groundGrace = accumulatedTime = 0;
            Fell = ReachedGoal = false;
        }

        public void Advance(float seconds, int direction, bool jump)
        {
            if (Fell || ReachedGoal) return;
            if (jump) jumpBuffer = .14f;
            accumulatedTime += Mathf.Min(seconds, .1f);
            while (accumulatedTime >= StepTime && !Fell && !ReachedGoal)
            {
                Step(direction);
                accumulatedTime -= StepTime;
            }
        }

        private void Step(int direction)
        {
            Vector2 position = Position;
            bool grounded = velocity <= 0 && Overlaps(position.x, position.y - .01f);
            groundGrace = grounded ? .10f : groundGrace - StepTime;
            if (jumpBuffer > 0 && groundGrace > 0)
            {
                velocity = JumpSpeed;
                jumpBuffer = groundGrace = 0;
            }
            jumpBuffer -= StepTime;

            float rate = direction == 0 ? Braking : grounded ? GroundAcceleration : AirAcceleration;
            horizontalSpeed = Mathf.MoveTowards(horizontalSpeed, direction * Speed, rate * StepTime);
            float x = Mathf.Clamp(position.x + horizontalSpeed * StepTime, HalfWidth, Width - HalfWidth);
            if (!Overlaps(x, position.y)) position.x = x;
            else horizontalSpeed = 0;

            velocity = Mathf.Max(-20, velocity - Gravity * StepTime);
            float y = position.y + velocity * StepTime;
            if (Overlaps(position.x, y))
            {
                position.y = velocity < 0 ? Mathf.Floor(y + Edge) + 1
                    : Mathf.Floor(y + Height - Edge) - Height;
                velocity = 0;
            }
            else if (y > BoardHeight - Height)
            {
                position.y = BoardHeight - Height;
                velocity = 0;
            }
            else position.y = y;

            Position = position;
            Fell = position.y < -Height;
            ReachedGoal = !solid[Mathf.FloorToInt(Goal.x), Mathf.FloorToInt(Goal.y)] &&
                Mathf.Abs(position.x - Goal.x) < .52f &&
                Mathf.Abs(position.y + Height * .5f - Goal.y) < .60f;
        }

        private bool Overlaps(float x, float feet)
        {
            int left = Mathf.Max(0, Mathf.FloorToInt(x - HalfWidth + Edge));
            int right = Mathf.Min(Width - 1, Mathf.FloorToInt(x + HalfWidth - Edge));
            int bottom = Mathf.Max(0, Mathf.FloorToInt(feet + Edge));
            int top = Mathf.Min(BoardHeight - 1, Mathf.FloorToInt(feet + Height - Edge));
            for (int col = left; col <= right; col++)
                for (int row = bottom; row <= top; row++)
                    if (solid[col, row]) return true;
            return false;
        }
    }
}
