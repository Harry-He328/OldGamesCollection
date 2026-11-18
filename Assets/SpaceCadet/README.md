# Space Cadet 3D pinball — models, materials and basic play

The standalone pinball scenes have been removed. The table now runs only inside the desktop collection: open `Assets/Scenes/SampleScene.unity`, then double-click the 3D Pinball desktop shortcut.

`Assets/SpaceCadet/Resources/DesktopPinballTable.prefab` includes the ball and its game controller.

- Right mouse drag: orbit around the real 3D table; wheel: zoom.
- R: reset view. 1: playing view. 2: overhead. 3: three-quarter view. V: automatic turntable.
- Hold and release Space: launch. A/D, Left/Right, or Left/Right Shift: flippers.
- Up: nudge (rapid repeated nudges can tilt). P: pause/resume. F2: restart.

## Current scope

Model/material pass:
- Continuous tapered flippers with bevelled rubber edges, ivory tops and metal bearings; simple compound motion has been replaced by a single kinematic pivot and a convex contact mesh.
- Round stand-up targets, bevelled drop targets, mounting hardware, mechanical spinner brackets/axles, machined well rims, bumper skirts, rounded cabinet trims, leg hardware and shooter handle.
- Imported ramps, railings, posts and playfield geometry remain actual 3D meshes.
- Source VPX material colors, metal flags and surface parameters are read from `Data/materials.json`, then adapted to URP Lit. Material variations for enamel, ivory, rubber and metal are explicit.
- Static colored inserts only. No emission, bloom, particles, animated lighting or post-processing effects in V2. Target/spinner/plunger movement is ordinary mechanical movement.

Basic logic:
- Charging/releasing the launcher; left/right contact-driven flippers; ball gravity along the playfield; bumper and rubber impacts.
- Three-ball game, out-of-bounds recovery, timed early-ball save, end-of-ball bonus, game over/restart, pause and persistent local high score.
- Target banks, multiplier (up to 5), lane rollovers, ramp entry/exit scoring and spinner contacts.
- Black-hole/hyperspace capture/ejection and wormhole routing; helper kickers outside the playable table are excluded.
- Nudge heat and tilt disable flippers/scoring for the remainder of the ball.

These are playable basic rules and provisional physics, **not** a faithful port of the original mission engine, exact score table or original physical response. Original missions, rank progression, exact awards, sound and multiplayer remain future work. Community table geometry/artwork differs from the original Windows version. No gameplay or automated tests were run, as requested; Unity import/compilation and scene resource generation are necessary authoring steps only.

## Editing

The scene builder and its editor menu were removed; the runtime table prefab is now the source of truth. Materials live in `Art/MaterialsV2`.

## Asset provenance

Source archive: https://github.com/drewburr/3D_Pinball_Game/blob/master/Files/Space_Cadet-022118a.zip
Downloaded via the same repository's raw URL, 2026-11-18.
VPX file: `spacecadet 022118a.vpx`.
Embedded TableInfo identifies the table author as **freneticamnesic**, website http://www.vpinball.com.
The referring repository explicitly notes that this table is not 1:1 with the Windows XP original.
Original Space Cadet game/artwork belongs to its respective rights holders. This archive provides no verified redistribution/commercial-use permission. Imported assets are for the user's requested local reconstruction; confirm permission before distributing these assets or a game containing them.

The local converter reads VPX binary records and emits meshes; no Visual Pinball engine implementation or game scripts were copied into the Unity project. Open-source format definitions were consulted for interoperability:
https://github.com/freezy/VisualPinball.Engine
https://github.com/vpinball/vpinball
The custom LZW decoding utility follows the standard GIF-style LZW format (8-bit symbols, clear/end codes, 12-bit dictionary).

Conversion intermediates and source archive are under `work/`, outside Unity's imported Assets.
