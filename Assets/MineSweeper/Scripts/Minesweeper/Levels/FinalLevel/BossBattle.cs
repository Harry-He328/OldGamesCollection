using System;
using UnityEngine;

namespace DesktopMinesweeper
{
    [Serializable]
    public sealed class BossRound
    {
        [SerializeField] private BoardLayout board;
        [SerializeField, Min(1)] private int seconds = 20;
        [SerializeField, Min(1)] private int damageToInterrupt = 4;

        public BoardLayout Board => board;
        public int Seconds => seconds;
        public int DamageToInterrupt => damageToInterrupt;
    }

    internal sealed class BossBattle
    {
        private readonly BossRound[] rounds;
        private readonly int damagePerMine;
        public int BossHealth { get; private set; }
        public int PlayerHealth { get; private set; }
        public int RoundNumber { get; private set; }
        public int DamageThisRound { get; private set; }
        public int MinesUsed { get; private set; }
        public BossRound Round => rounds[(RoundNumber - 1) % rounds.Length];
        public bool Interrupted => DamageThisRound >= Round.DamageToInterrupt;

        public BossBattle(BossRound[] rounds, int bossHealth, int playerHealth, int damagePerMine)
        {
            this.rounds = rounds;
            this.damagePerMine = damagePerMine;
            BossHealth = bossHealth;
            PlayerHealth = playerHealth;
        }

        public void BeginRound()
        {
            RoundNumber++;
            DamageThisRound = 0;
            MinesUsed = 0;
        }

        public void Hit()
        {
            MinesUsed++;
            DamageThisRound += damagePerMine;
            BossHealth = Math.Max(0, BossHealth - damagePerMine);
        }

        public void ResolveAttack()
        {
            if (!Interrupted && BossHealth > 0) PlayerHealth--;
        }
    }

    internal sealed class BossRules : MinesweeperRules
    {
        private readonly Action<CellData> launchMine;

        public BossRules(Action<CellData> launchMine) => this.launchMine = launchMine;

        public override void OnMineRevealed(BoardData board, CellData cell) => launchMine(cell);
        public override GameState Evaluate(BoardData board) => GameState.Playing;

        protected override bool CountsForChord(CellData cell) =>
            base.CountsForChord(cell) || (cell.HasMine && cell.IsRevealed);
    }
}
