# Breakout (desktop window)

Open `Assets/Scenes/SampleScene.unity`, press Play, then double-click the **Breakout** shortcut on the desktop.

## How it plays

- The window is the game. Bricks and the paddle are bound to the window; the paddle sits fixed at the bottom centre.
- The ball is free: it lives in desktop space and does not follow the window. It bounces off the window's left, top and right edges. If it falls out of the bottom edge you lose a life (3 lives).
- Hold the mouse on the window (playfield, title bar or frame) to drag it. While dragging the whole game runs at **0.25x** (bullet time) and the energy ring in the score bar drains. One drag lasts at most about 1 s. When not dragging, the ring refills on its own in about 3 s, and every bounce of the ball (wall, paddle, brick, steel, desktop icon) also gives back 20%. When the ring is empty the window stays where it is.
- The window may be dragged partly off screen (up to 60% left, right and bottom, 30% at the top), so every spot on the desktop can be reached. The taskbar stays on top of it, and open task buttons are packed left to right after Start.
- Each level has a goal marked with a gold star. Hit it once and the level is cleared. Other bricks and folders are optional: they give points.
- Click or press Space to launch. F2 or **New game** restarts.
- Testing shortcut (Editor and development builds only): **Ctrl+Shift+B** clears all three levels at once, from anywhere, and finishes the game.
- There are no text hints. Before launch the ball breathes and an arrow bobs above it. On level 1, once the ball is in play, a ghost cursor grabs the playfield and slides a ghost outline of the window sideways, until the player drags the window for the first time. Draggable icons highlight on hover, like XP icons.

Levels:

1. **The Window**: two star bricks behind a steel shield. Drag the window to steer the ball around it.
2. **Outside the Window**: the goal is the starred *My Documents* folder near the top of the desktop, with the indestructible System32 folder right below it. Pull the window up and angle the ball past System32, or, if the player thinks of it, drag the folder itself into the window. Nothing says folders can be dragged.
3. **Deep Storage**: *Treasure.txt* is sealed in a vault of triple-strength bricks. Digging in works but is slow. The file itself can be dragged with the mouse, at no energy cost: drop it on the playfield and it moves with the window, or drop it on the desktop and it stays there. If the player has not touched it after 12 s, the file gives a small shake every 5 s.

Clearing level 3 shows **ALL CLEAR**, and the game is finished for good: the screen stays frozen, New game, clicks and Space no longer restart it, and the window gives up the keyboard. From then on it is an ordinary window that moves, resizes (down to 170x120) and maximises freely, with no energy or bullet time. On the desktop it is the bridge to Minesweeper (see Assets/Collection/README.md).

Brick types inside the window: normal (1–3 hits), gold star target, grey steel (never breaks), green energy (+30% ring on top of the bounce), red bomb (destroys its neighbours, chains), purple dashed ghost (solid only in bullet time). Some rows can slide left and right.

Desktop icons: yellow folder, locked system folder (indestructible), CD drive (round bumper), text file. A destroyed folder or file flies into the Recycle Bin.

## Code

- `Scripts/Core/BreakoutLevels.cs`: level maps, goals and desktop icon layouts.
- `Scripts/Core/BreakoutSimulation.cs`: rules and collision, no UI dependencies. Everything is in desktop coordinates (top-left origin, y down). The ball is sub-stepped against the window movement, so fast drags cannot tunnel.
- `Scripts/Presentation/BreakoutApp.cs`: desktop integration and drawing.
- `Scripts/Presentation/BreakoutArt.cs`: every sprite (glossy bricks, folders, CD, ring, icon) is painted in code at runtime. No image assets are imported or downloaded.

The window, taskbar button and shortcut are runtime clones of the authored 3D Pinball shell, so they share its XP frame, font and title buttons. The scene file is not modified. The clone's drag and resize handles are replaced with Breakout drag surfaces, and its size is locked. Breakout moves the window itself (`MoveWindow`) so it can leave the screen; the shared `DesktopWindow` clamp is unchanged. The ball, desktop folders and particles are drawn on a layer kept directly above the Breakout window.

Minesweeper's keyboard handling yields while Breakout has focus, as it already does for Pinball (`BreakoutApp.OwnsKeyboard`).

In-game text is English because the project's TMP font (LiberationSans) has no CJK glyphs.

Tuning constants are at the top of `BreakoutSimulation` (`Slow`, `Drain`, `Regen`, `HitGain`, `EnergyBrickGain`).
