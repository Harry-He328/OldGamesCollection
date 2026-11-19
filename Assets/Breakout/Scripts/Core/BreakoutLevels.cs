namespace OldGames.Breakout
{
    public enum DeskKind
    {
        Folder,   // breakable desktop folder
        Locked,   // system folder: indestructible
        Disc,     // CD drive: round bumper
        File      // a document icon
    }

    public sealed class DeskSpec
    {
        public readonly DeskKind kind;
        // Desktop icons: fraction of the desktop workspace, from the top-left.
        // inWindow icons: fraction of the playfield, from its top-left.
        public readonly float fx, fy;
        public readonly string label;
        public bool goal;        // hitting it clears the level
        public bool draggable;   // can be picked up and moved with the mouse
        public bool inWindow;    // starts inside the window and moves with it until dragged out
        public DeskSpec(DeskKind kind, float fx, float fy, string label) { this.kind = kind; this.fx = fx; this.fy = fy; this.label = label; }
    }

    public sealed class LevelSpec
    {
        public string name;
        public string[] map;
        public int[] move = System.Array.Empty<int>();
        public DeskSpec[] desk = System.Array.Empty<DeskSpec>();
    }

    // Window map, 10 columns per row:
    //   .  empty        1/2/3 normal brick (hit points)   #  steel (indestructible)
    //   E  energy       B  bomb (destroys its neighbours)  G  ghost (solid only in bullet time)
    //   T  target: hitting it clears the level
    // move: rows that slide left and right inside the window.
    // desk: icons fixed to the desktop (they do not follow the window), unless inWindow.
    //
    // A level is cleared as soon as one goal (a T brick or a starred icon) is hit. Other bricks
    // and folders are optional: they give points. Every bounce refills 20% of the energy ring.
    public static class BreakoutLevels
    {
        // Desktop folders can be dragged with the mouse, like real ones. Nothing says so.
        static DeskSpec Folder(float x, float y, string label) => new DeskSpec(DeskKind.Folder, x, y, label) { draggable = true };
        static DeskSpec Locked(float x, float y, string label) => new DeskSpec(DeskKind.Locked, x, y, label);
        static DeskSpec Disc(float x, float y, string label) => new DeskSpec(DeskKind.Disc, x, y, label);
        static DeskSpec GoalFolder(float x, float y, string label) => new DeskSpec(DeskKind.Folder, x, y, label) { goal = true, draggable = true };
        static DeskSpec GoalFile(float x, float y, string label) =>
            new DeskSpec(DeskKind.File, x, y, label) { goal = true, draggable = true, inWindow = true };

        public static readonly LevelSpec[] All =
        {
            // 1. Only the window: learn to steer the ball by dragging the window.
            new LevelSpec
            {
                name = "The Window",
                map = new[] { "....TT....", "..######..", "..........", "..........", "1111111111", "11E1111E11" },
            },
            // 2. The target is outside the window, on the desktop. Dragging the window over it is the
            //    expected way; the folder itself can also be dragged into the window, if the player thinks of it.
            new LevelSpec
            {
                name = "Outside the Window",
                map = new[] { "..........", "1111111111", "1E11BB11E1", "1111111111" },
                desk = new[] { GoalFolder(.8f, .1f, "My Documents"), Folder(.42f, .78f, "New Folder"),
                               Disc(.62f, .26f, "CD Drive (D:)"), Locked(.8f, .24f, "System32") }
            },
            // 3. The target is sealed in a vault. Digging in takes ages; dragging the file out does not.
            new LevelSpec
            {
                name = "Deep Storage",
                map = new[] { "##########", "#33333333#", "#333..333#", "#333..333#", "#33333333#", "#33333333#", "###3333###" },
                desk = new[] { GoalFile(.5f, .213f, "Treasure.txt") }
            },
        };
    }
}
