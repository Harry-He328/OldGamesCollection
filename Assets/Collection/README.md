# Desktop games

Open Assets/Scenes/SampleScene.unity, then Play. Double-click the text-only 3D Pinball desktop shortcut to open its window. No icon asset has been added.

The existing desktop remains in place. Pinball renders to a RawImage inside a DesktopWindow, with its own camera and isolated 3D physics container. There is no scene replacement, scene-loading launcher or return-to-desktop button. Minesweeper board state is retained.

The title bar drags the window; right/bottom edges resize; title buttons minimize, maximize and close. A task button restores the window. Closing or minimizing pauses simulation; reopening retains the current ball/game. Click the pinball viewport to give it keyboard focus. Minesweeper keyboard logic yields while pinball owns focus.

SPACE: charge/release launcher; A/D or Shift: flippers; P: pause pinball only; F2: restart. Right mouse drag: orbit; wheel: zoom while pointing inside the viewport. Camera input and score display stay within the game window. The desktop clock and other applications are not globally paused.

The original table prefab is retained; the runtime desktop copy is Assets/SpaceCadet/Resources/DesktopPinballTable.prefab. Basic rules remain approximate. No gameplay/automated tests run, per request.

The pinball window is authored in the desktop scene by cloning the Minesweeper window shell and task button. It uses the same border images, TMP font, title-button components, menu, drag handles and resize handles. Only the game controller and board view are replaced. DesktopAppShortcut is shared by both games; the Pinball shortcut selects the pinball application. No custom runtime-drawn window frame is created. Icon slots remain empty pending suitable artwork.
