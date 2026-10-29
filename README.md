# Hushwater player prototype

Open this folder in Unity 6 and press Play. The first scene does not need to be set up: `HushwaterBootstrap` builds the street, houses, wardrobe, props, player camera, the Sexton, and the HUD at runtime.

Controls: `WASD` move, `Shift` sprint, `C` or `Left Ctrl` crouch, `Space` hold breath, `E` pick up / hide in the wardrobe, `Q` throw a bottle, `Esc` unlock the mouse, `R` restart after the game ends.

Goal: find the three photographs (evidence) hidden around the street and bring them back to the south gate where you started.

The player emits `NoisePulse` events for crouching, walking, sprinting, and an exhausted-breath gasp. The Sexton can subscribe to `NoisePulse.Emitted` without needing to know anything about the player controller. Two sounds within 5 seconds send him to investigate, and three start a hunt. Up close he can hear you breathing unless you hold your breath. He swipes with his hook at 2 m and grabs at 1 m, and a grab plays the kill animation and ends the run.

Models and textures under `Assets/Resources/Hushwater` are CC0 assets from Poly Haven. Sounds are generated in code (`ProceduralSounds`) until the FMOD banks exist.
