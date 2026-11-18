using UnityEngine;
using UnityEngine.InputSystem;

// Retained for reuse; the active campaign has no transition into this stage.
namespace DesktopMinesweeper
{
    public partial class MinesweeperController
    {
        private PlatformRun platform;
        private float nextLevelDelay;

        private void BeginPlatform()
        {
            stage = Stage.Platform;
            nextLevelDelay = 0;
            platform = new PlatformRun(Game.Board, campaign.PlatformStart, campaign.PlatformGoal);
            CellView source = ViewAt(campaign.PlatformStart);
            foreach (CellView view in views) view.ShowPlatformTile(platform.IsSolid(view.Data), view == source);
            if (boardBackground != null) boardBackground.color = new Color32(36, 51, 67, 255);
            effects.ShowPlatform(platform, source);
            RefreshHud();
        }

        private void RetryJump()
        {
            if (platform.ReachedGoal) return;
            platform.Retry();
            effects.PositionRunner(platform);
            RefreshHud();
        }

        private void UpdatePlatform()
        {
            if (platform.ReachedGoal)
            {
                if (nextLevelDelay > 0)
                {
                    nextLevelDelay -= Time.deltaTime;
                    if (nextLevelDelay <= 0) StartBoss();
                }
                return;
            }
            if (platform.Fell) return;
            Keyboard keys = Keyboard.current;
            int direction = 0;
            bool jump = false;
            if (keys != null)
            {
                if (keys.leftArrowKey.isPressed || keys.aKey.isPressed) direction--;
                if (keys.rightArrowKey.isPressed || keys.dKey.isPressed) direction++;
                jump = keys.spaceKey.wasPressedThisFrame || keys.upArrowKey.wasPressedThisFrame ||
                    keys.wKey.wasPressedThisFrame;
            }
            platform.Advance(Time.deltaTime, direction, jump);
            effects.PositionRunner(platform);
            if (platform.ReachedGoal) nextLevelDelay = .45f;
            if (platform.ReachedGoal || platform.Fell) RefreshHud();
        }

    }
}
