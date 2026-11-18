using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DesktopMinesweeper
{
    public partial class MinesweeperController : MonoBehaviour
    {
        [SerializeField, Min(1)] private int width = 9;
        [SerializeField, Min(1)] private int height = 9;
        [SerializeField, Min(0)] private int mineCount = 10;
        [SerializeField] private GridLayoutGroup grid;
        [SerializeField] private CellView cellPrefab;
        [SerializeField] private TMP_Text statusLabel;
        [SerializeField] private TMP_Text mineLabel;
        [SerializeField] private TMP_Text timerLabel;
        [SerializeField] private Image restartFace;
        [SerializeField] private Sprite readyFace;
        [SerializeField] private Sprite wonFace;
        [SerializeField] private Sprite lostFace;
        [Header("Campaign")]
        [SerializeField] private CampaignLayout campaign;

        private enum Stage { Sweep, Platform, Boss, HiddenBoard }
        // Insert future normal levels here. Boss is always appended as the final level.
        private static readonly Stage[] CampaignLevels = { Stage.Sweep, Stage.HiddenBoard };
        private Stage stage;
        private readonly List<CellView> views = new List<CellView>();
        private float elapsed;
        private DesktopWindow window;
        private Image boardBackground;
        private Color defaultBoardColor;
        private BoardEffects effects;
        private int displayedSeconds = -1;
        private CampaignHud hud;
        private DesktopPet pet;
        private bool petTransition;
        private BoardLayout SweepBoard => campaign != null ? campaign.SweepBoard : null;
        public MinesweeperGame Game { get; private set; }
        public RectTransform DesktopRoot => window != null ? (RectTransform)window.transform.parent : (RectTransform)transform.parent;
        public bool WindowOpen => window != null && gameObject.activeInHierarchy;

        protected virtual MinesweeperRules CreateRules() => new MinesweeperRules();

        private void Awake()
        {
            if (campaign == null) campaign = Resources.Load<CampaignLayout>("Minesweeper/Campaign");
            window = GetComponent<DesktopWindow>();
            if (grid != null)
            {
                boardBackground = grid.GetComponent<Image>();
                if (boardBackground != null) defaultBoardColor = boardBackground.color;
                if (restartFace != null)
                {
                    effects = new BoardEffects((RectTransform)transform, (RectTransform)grid.transform,
                        restartFace.rectTransform);
                    if (SweepBoard != null)
                        hud = new CampaignHud((RectTransform)transform, restartFace, RestartStage,
                            OnFacePressed, AdvanceStage);
                    // Must run after CampaignHud: it locates the button through the face hierarchy.
                    pet = DesktopPet.Attach(this, restartFace);
                }
            }
        }

        private void Start()
        {
            NewGame();
            // The desktop starts clean; the shortcut (and the opening beat) opens the game.
            if (window != null) window.CloseWindow();
        }

        private void RestartStage()
        {
            if (petTransition) return;
            if (stage == Stage.Boss) StartBoss();
            else if (stage == Stage.HiddenBoard) StartHiddenBoard();
            else NewGame();
        }

        public void OnFacePressed()
        {
            if (petTransition) return;
            if (stage == Stage.Boss) return;
            if (stage == Stage.Platform) RetryJump();
            else if (CanAdvanceSweep || stage == Stage.HiddenBoard && Game.State == GameState.Won) AdvanceStage();
            else if (stage == Stage.HiddenBoard) StartHiddenBoard();
            else NewGame();
        }

        public void AdvanceStage()
        {
            if (petTransition) return;
            if (CanAdvanceSweep || stage == Stage.HiddenBoard && Game.State == GameState.Won)
            {
                int next = System.Array.IndexOf(CampaignLevels, stage) + 1;
                if (next >= CampaignLevels.Length) EnterBoss();
                else if (CampaignLevels[next] == Stage.HiddenBoard) StartHiddenBoard();
            }
            else if (stage == Stage.Platform && platform.Fell) RetryJump();
            else if (stage == Stage.Boss && battle.BossHealth == 0) NewGame();
        }

        // The boss starts with a short beat: input pauses while the pet flies back into its
        // window button, then the round begins.
        private void EnterBoss()
        {
            if (pet == null)
            {
                StartBoss();
                return;
            }
            petTransition = true;
            pet.AttachToButton(DesktopPet.TransitionSeconds, () =>
            {
                petTransition = false;
                StartBoss();
            });
        }

        private bool CanAdvanceSweep => SweepBoard != null && stage == Stage.Sweep &&
            Game != null && (Game.State == GameState.Playing || Game.State == GameState.Won);

        private void LoadFixedBoard(BoardLayout layout, MinesweeperRules rules)
        {
            BoardData board = layout.CreateBoard(rules);
            LoadBoard(board, rules, true);
            if (window != null)
            {
                float preferred = layout.PreferredCellSize;
                float minimum = Mathf.Min(22, preferred);
                window.SetContentSize(new Vector2(Mathf.Max(460, board.Width * preferred + 32),
                        board.Height * preferred + 126),
                    new Vector2(Mathf.Max(420, board.Width * minimum + 32),
                        board.Height * minimum + 126));
            }
        }

        private void LoadBoard(BoardData board, MinesweeperRules rules, bool fixedMines)
        {
            if (grid == null || cellPrefab == null || !cellPrefab.IsConfigured)
            {
                Debug.LogError("Assign the board Grid, Cell Prefab and its text Label before playing.", this);
                return;
            }
            if (Game != null) Game.Changed -= Refresh;
            effects?.Clear();
            hitFlash = 0;
            if (boardBackground != null) boardBackground.color = defaultBoardColor;
            foreach (var view in views)
            {
                view.gameObject.SetActive(false);
                view.transform.SetParent(null, false);
                Destroy(view.gameObject);
            }
            views.Clear();
            Game = new MinesweeperGame(board, rules, new System.Random(), fixedMines);
            elapsed = 0;
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = board.Width;
            grid.startAxis = GridLayoutGroup.Axis.Horizontal;
            grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
            foreach (var cell in Game.Board.Cells)
            {
                var view = Instantiate(cellPrefab, grid.transform);
                view.name = $"Cell_{cell.X}_{cell.Y}";
                view.Bind(Game, cell, Act);
                views.Add(view);
            }
            Game.Changed += Refresh;
            Refresh();
            UpdateTimer();
        }

        private void Update()
        {
            if (OldGames.Collection.DesktopPinballApp.OwnsKeyboard) return;
            if (Game == null) return;
            switch (stage)
            {
                case Stage.Sweep:
                    UpdateSweep();
                    break;
                case Stage.HiddenBoard:
                    hiddenBoard?.Tick();
                    UpdateSweep();
                    break;
                case Stage.Platform:
                    UpdatePlatform();
                    break;
                case Stage.Boss:
                    UpdateBoss();
                    break;
            }
        }

        private CellView ViewAt(Vector2Int p) => views[p.y * Game.Board.Width + p.x];

        private void Act(CellData cell, CellAction action)
        {
            if (petTransition) return;
            if (stage == Stage.Platform) return;
            if (stage == Stage.Boss)
            {
                if (!bossRoundActive) return;
                if (Time.timeAsDouble >= roundEndsAt)
                {
                    FinishRound();
                    return;
                }
            }
            Game.Act(cell.X, cell.Y, action);
            if (stage == Stage.Boss && (battle.MinesUsed == Game.Board.MineCount || battle.BossHealth == 0))
                FinishRound();
        }

        private void UpdateTimer()
        {
            if (timerLabel == null) return;
            int seconds = stage == Stage.Boss ? (bossRoundActive ?
                Mathf.CeilToInt((float)System.Math.Max(0, roundEndsAt - Time.timeAsDouble)) : 0)
                : Mathf.FloorToInt(elapsed);
            seconds = Mathf.Min(999, seconds);
            if (seconds == displayedSeconds) return;
            displayedSeconds = seconds;
            timerLabel.text = seconds.ToString("000");
        }

        private void Refresh()
        {
            int flags = 0;
            foreach (var cell in Game.Board.Cells)
                if (cell.IsFlagged) flags++;
            foreach (var view in views) view.Render();
            if (mineLabel != null)
                mineLabel.text = (stage == Stage.Boss ? battle.PlayerHealth : Game.Board.MineCount - flags).ToString("000");
            if (restartFace != null)
                restartFace.sprite = Game.State == GameState.Won ? wonFace
                    : Game.State == GameState.Lost ? lostFace : readyFace;
            RefreshHud();
            if (statusLabel == null) return;
            switch (Game.State)
            {
                case GameState.Ready: statusLabel.text = "Click a cell to start"; break;
                case GameState.Playing: statusLabel.text = "Minesweeper"; break;
                case GameState.Won: statusLabel.text = "You win!"; break;
                case GameState.Lost: statusLabel.text = "Game over"; break;
            }
        }

        private void RefreshHud()
        {
            bool isBoss = stage == Stage.Boss;
            if (restartFace != null && !isBoss)
            {
                restartFace.color = Color.white;
                restartFace.rectTransform.localScale = Vector3.one;
                if (stage == Stage.Platform) restartFace.sprite = platform.ReachedGoal ? wonFace :
                    platform.Fell ? lostFace : readyFace;
            }
            if (hud == null) return;

            string menu = "New game";
            string info = "";
            string next = "";
            Color messageColor = new Color32(32, 40, 52, 255);
            if (stage == Stage.Platform)
            {
                menu = "Back to level 1";
                if (platform.Fell) next = "Retry";
            }
            else if (stage == Stage.Sweep && CanAdvanceSweep)
            {
                next = "Next level";
            }
            else if (stage == Stage.HiddenBoard)
            {
                menu = "Restart level";
                info = Game.State == GameState.Won ? "Level complete" :
                    Game.State == GameState.Lost ? "Game over" : "Move windows to explore";
                if (Game.State == GameState.Won) next = "Final Level";
            }
            else if (isBoss)
            {
                menu = "Restart level";
                if (mineLabel != null) mineLabel.text = battle.PlayerHealth.ToString("000");
                restartFace.sprite = battle.BossHealth == 0 ? lostFace : readyFace;
                hud.SetBossHealth(battle.BossHealth, campaign.BossHealth);
                if (battle.BossHealth == 0) { info = "Level complete"; next = "New game"; }
                else if (battle.PlayerHealth == 0) info = "Game over";
                else if (!bossRoundActive) info = battle.Interrupted ? "Attack blocked" : "Player hit";
                else info = battle.Interrupted ? $"Round {battle.RoundNumber}  |  Attack blocked" :
                    $"Round {battle.RoundNumber}  |  Find mines  {battle.DamageThisRound}/{battle.Round.DamageToInterrupt}";

                if (battle.BossHealth == 0 || battle.Interrupted) messageColor = new Color32(13, 88, 46, 255);
                else if (!bossRoundActive) messageColor = new Color32(138, 36, 30, 255);
            }

            int level = isBoss ? CampaignLevels.Length + 1 : System.Array.IndexOf(CampaignLevels, stage) + 1;
            hud.Show(level, CampaignLevels.Length + 1, menu, info, next, isBoss, messageColor);
        }

        private void OnDestroy()
        {
            EndHiddenBoard();
            if (Game != null) Game.Changed -= Refresh;
        }
    }
}
