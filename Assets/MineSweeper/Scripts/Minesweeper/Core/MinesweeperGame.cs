using System;

namespace DesktopMinesweeper
{
    public sealed class MinesweeperGame
    {
        private readonly MinesweeperRules rules;
        private readonly Random random;
        private readonly bool fixedMines;
        public BoardData Board { get; }
        public GameState State { get; private set; } = GameState.Ready;
        public event Action Changed;

        // Variant rules can change outside a cell click, for example through another application.
        public void ReevaluateRules()
        {
            if (State == GameState.Won || State == GameState.Lost) return;
            GameState result = rules.Evaluate(Board);
            if (State != GameState.Ready || result != GameState.Playing) State = result;
            Changed?.Invoke();
        }

        private int CountRevealed()
        {
            int count = 0;
            foreach (var cell in Board.Cells) if (cell.IsRevealed) count++;
            return count;
        }

        public MinesweeperGame(BoardData board, MinesweeperRules rules, Random random, bool fixedMines = false)
        {
            Board = board ?? throw new ArgumentNullException(nameof(board));
            this.rules = rules ?? throw new ArgumentNullException(nameof(rules));
            this.random = random ?? throw new ArgumentNullException(nameof(random));
            this.fixedMines = fixedMines;
        }

        public void Act(int x, int y, CellAction action)
        {
            if (State == GameState.Won || State == GameState.Lost) return;
            var cell = Board.GetCell(x, y);
            switch (action)
            {
                case CellAction.ToggleFlag:
                    rules.ToggleFlag(Board, cell);
                    break;
                case CellAction.Reveal:
                    if (cell.IsFlagged || cell.IsRevealed) return;
                    if (State == GameState.Ready)
                    {
                        if (!fixedMines) rules.PlaceMines(Board, cell, random);
                        State = GameState.Playing;
                    }
                    rules.Reveal(Board, cell);
                    break;
                case CellAction.Chord:
                    // A random board has no mines before the first reveal, but a fixed board can show
                    // authored clues from the start, and those chord like any other number.
                    if (State == GameState.Ready && !fixedMines) return;
                    int opened = CountRevealed();
                    rules.Chord(Board, cell);
                    if (State == GameState.Ready && CountRevealed() != opened) State = GameState.Playing;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(action));
            }
            if (State == GameState.Playing) State = rules.Evaluate(Board);
            Changed?.Invoke();
        }
    }
}
