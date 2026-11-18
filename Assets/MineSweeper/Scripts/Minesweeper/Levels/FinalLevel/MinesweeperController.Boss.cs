using UnityEngine;
namespace DesktopMinesweeper
{
    public partial class MinesweeperController
    {
        private BossBattle battle;
        private bool bossRoundActive;
        private double roundEndsAt;
        private double nextRoundAt;
        private double pausedAt;
        private bool paused;
        private float hitFlash;

        private void StartBoss()
        {
            if (campaign.BossRounds == null || campaign.BossRounds.Length == 0)
            {
                Debug.LogError("Assign at least one boss round.", this);
                return;
            }
            EndHiddenBoard();
            stage = Stage.Boss;
            platform = null;
            battle = new BossBattle(campaign.BossRounds, campaign.BossHealth, campaign.PlayerHealth,
                campaign.DamagePerMine);
            BeginBossRound();
        }

        private void BeginBossRound()
        {
            battle.BeginRound();
            bossRoundActive = true;
            roundEndsAt = Time.timeAsDouble + battle.Round.Seconds;
            LoadFixedBoard(battle.Round.Board, new BossRules(LaunchMine));
            Vector2Int opening = battle.Round.Board.Opening;
            Game.Act(opening.x, opening.y, CellAction.Reveal);
            UpdateTimer();
        }

        private void LaunchMine(CellData cell)
        {
            battle.Hit();
            CellView view = ViewAt(new Vector2Int(cell.X, cell.Y));
            view.MarkMineUsed();
            effects.Launch(view);
        }

        private void FinishRound()
        {
            bossRoundActive = false;
            int health = battle.PlayerHealth;
            battle.ResolveAttack();
            if (battle.PlayerHealth < health && mineLabel != null)
                effects.Counterattack(mineLabel.rectTransform);
            nextRoundAt = Time.timeAsDouble + 1.25;
            RefreshHud();
            UpdateTimer();
        }

        private void UpdateBoss()
        {
            if (effects.AnimateFlights(Time.deltaTime)) hitFlash = .20f;
            hitFlash = Mathf.Max(0, hitFlash - Time.deltaTime);
            restartFace.color = Color.Lerp(new Color32(255, 125, 103, 255), Color.white, hitFlash * 5);
            restartFace.rectTransform.localScale = Vector3.one * (1 + hitFlash * .8f);

            if (bossRoundActive && Time.timeAsDouble >= roundEndsAt) FinishRound();
            else if (!bossRoundActive && battle.BossHealth > 0 && battle.PlayerHealth > 0 &&
                Time.timeAsDouble >= nextRoundAt) BeginBossRound();
            UpdateTimer();
        }

        private void OnDisable()
        {
            if (stage != Stage.Boss || battle == null) return;
            paused = true;
            pausedAt = Time.timeAsDouble;
        }

        private void OnEnable()
        {
            if (!paused) return;
            double gap = Time.timeAsDouble - pausedAt;
            roundEndsAt += gap;
            nextRoundAt += gap;
            paused = false;
        }

    }
}
