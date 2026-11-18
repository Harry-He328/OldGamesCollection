using UnityEngine;
namespace DesktopMinesweeper
{
    public partial class MinesweeperController
    {
        private void UpdateSweep()
        {
            if (Game.State != GameState.Playing) return;
            elapsed += Time.deltaTime;
            UpdateTimer();
        }

        public void NewGame()
        {
            EndHiddenBoard();
            stage = Stage.Sweep;
            platform = null;
            battle = null;
            if (SweepBoard != null) LoadFixedBoard(SweepBoard, CreateRules());
            else LoadBoard(new BoardData(width, height, mineCount), CreateRules(), false);
            // Coming back from the boss, the pet returns to the desktop.
            pet?.ShowOnDesktop(DesktopPet.TransitionSeconds);
        }

    }
}
