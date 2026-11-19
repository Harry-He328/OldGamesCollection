# Desktop games

Open Assets/Scenes/SampleScene.unity, then Play. Double-click the text-only 3D Pinball desktop shortcut to open its window. No icon asset has been added.

The existing desktop remains in place. Pinball renders to a RawImage inside a DesktopWindow, with its own camera and isolated 3D physics container. There is no scene replacement, scene-loading launcher or return-to-desktop button. Minesweeper board state is retained.

The title bar drags the window; right/bottom edges resize; title buttons minimize, maximize and close. A task button restores the window. Closing or minimizing pauses simulation; reopening retains the current ball/game. Click the pinball viewport to give it keyboard focus. Minesweeper keyboard logic yields while pinball owns focus.

SPACE: charge/release launcher; A/D or Shift: flippers; P: pause pinball only; F2: restart. Right mouse drag: orbit; wheel: zoom while pointing inside the viewport. Camera input and score display stay within the game window. The desktop clock and other applications are not globally paused.

The original table prefab is retained; the runtime desktop copy is Assets/SpaceCadet/Resources/DesktopPinballTable.prefab. Basic rules remain approximate. No gameplay/automated tests run, per request.

The pinball window is authored in the desktop scene by cloning the Minesweeper window shell and task button. It uses the same border images, TMP font, title-button components, menu, drag handles and resize handles. Only the game controller and board view are replaced. DesktopAppShortcut is shared by both games; the Pinball shortcut selects the pinball application. No custom runtime-drawn window frame is created. Icon slots remain empty pending suitable artwork.

## Breakout

Double-click the Breakout desktop shortcut. Its window, task button and shortcut are runtime clones of the Pinball shell (no scene changes). Holding the window drags it in 0.25x bullet time, which drains an energy ring. Three levels, each cleared by hitting one starred goal: inside the window, on the desktop, and a file you can drag out with the mouse. See Assets/Breakout/README.md.

## Desktop cursor runner

The desktop is also a small platformer, and the player is the mouse cursor. At Play a big cursor pops out of the Start button. While it is a body the real pointer is hidden and mouse clicks are swallowed, so the keyboard is the only way around. A/D or the arrow keys roll it: it never slides, it tumbles over its own corners. Space, W or Up jumps (hold for a higher jump). On any icon or window that is not a shortcut, Down or S drops through.

Standing on a shortcut and pressing Down or S opens it. The cursor sinks into the icon, the game opens, and the cursor rises out of the icon, shrinks to pointer size and becomes the real mouse pointer at that spot, so the game is played with the mouse. It turns back into a body, which flies from the pointer back to the shortcut it went in through (so the mouse never carries it across the level), when that game's window is closed or minimised, or when a move or jump key is pressed while the desktop has the keyboard. That is always the case for Minesweeper. For Pinball and Breakout, click the desktop first; Notepad needs its text field unfocused. While the pet is talking, the pointer is shown so its lines can be clicked through.

E leaves the body without opening anything: the body stays where it is as a faint ghost, still standing and still riding whatever it stands on, and the real pointer comes out of its tip to move or resize windows and drag icons. E again, or a move or jump key, sends the pointer back into the body. Shortcuts never open from a mouse double-click, in any mode: only the body pressing Down on them opens them. Every icon lights up while the body stands on it.

**To do: onboarding for E.** Nothing in the game teaches E yet, and the bridge puzzle to Minesweeper depends on it (the cleared Breakout window has to be moved and resized with the pointer). The player needs to be guided to it without obvious on-screen text, for example by the pet nudging or showing it at the moment the body is stuck below the Minesweeper icon.

Testing shortcut (Editor and development builds only): **Ctrl+Shift+M** opens Minesweeper at once and hands over the real pointer, as if the body had entered its shortcut.

Platforms are the taskbar, the top of every desktop icon's whole box, picture and label (unless a window covers it), the top edge of every open window and the pet. Windows and icons moved with the mouse stay where they are put, and the body rides whatever it stands on (also as a ghost), so moving things around reshapes the level, and a window can carry the body up.

The icons start in a layout that follows the game order. Breakout (game 1) is one jump from Start, at the foot of a short zigzag tower in the left columns (Recycle Bin, Notepad, Computer), away from the middle of the screen where windows open. Minesweeper (game 2) is alone in the far top-right corner, out of reach of any jump. Once Breakout is cleared, its window stays on the ALL CLEAR screen for good and becomes an ordinary window: it can be moved, resized and maximised freely, and New game no longer restarts it. The way to Minesweeper is to make that window a bridge: stretch it from the tower to the right side, with its top a little lower than it starts, climb the tower, cross it and jump. Pinball (game 3) floats above the middle of the screen, just over the top of the Minesweeper window. Code: `Assets/Collection/Scripts/DesktopCursorRunner.cs`.
