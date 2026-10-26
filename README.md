# Hushwater player prototype

Open this folder in Unity 6 and press Play. The first scene does not need to be set up: `HushwaterBootstrap` creates a grey test room, door, wardrobe, player camera, and HUD at runtime.

Controls: `WASD` move, `Shift` sprint, `C` or `Left Ctrl` crouch, `Space` hold breath, `E` enter/leave the wardrobe, `Esc` unlock the mouse.

The player emits `NoisePulse` events for crouching, walking, sprinting, and an exhausted-breath gasp. The Sexton can subscribe to `NoisePulse.Emitted` without needing to know anything about the player controller.
