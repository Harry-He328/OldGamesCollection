using UnityEngine;

namespace DesktopMinesweeper
{
    [CreateAssetMenu(menuName = "Minesweeper/Campaign")]
    public sealed class CampaignLayout : ScriptableObject
    {
        [SerializeField] private BoardLayout sweepBoard;
        [SerializeField] private Vector2Int platformStart = new Vector2Int(0, 6);
        [SerializeField] private Vector2Int platformGoal = new Vector2Int(19, 1);
        [SerializeField] private BossRound[] bossRounds;
        [SerializeField, Min(1)] private int bossHealth = 16;
        [SerializeField, Min(1)] private int playerHealth = 3;
        [SerializeField, Min(1)] private int damagePerMine = 1;

        public BoardLayout SweepBoard => sweepBoard;
        public Vector2Int PlatformStart => platformStart;
        public Vector2Int PlatformGoal => platformGoal;
        public BossRound[] BossRounds => bossRounds;
        public int BossHealth => bossHealth;
        public int PlayerHealth => playerHealth;
        public int DamagePerMine => damagePerMine;
    }
}
