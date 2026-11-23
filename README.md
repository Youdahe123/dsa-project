# Hushwater

Open this folder in Unity 6, open `Assets/Scenes/HushwaterPrototype.unity` and press Play. Everything is built in code at runtime by `HushwaterBootstrap`: menus, the town for each night, the Sexton and the HUD.

Still placeholder: models and animation are primitives and a few Poly Haven props, and sound is generated in code (`ProceduralSounds`). There is no FMOD yet.

## The game

Main menu → the boat → a night in town → back to the boat, for five nights (design doc, "The Five Nights").

| Night | Open | Evidence needed | Pressure |
| --- | --- | --- | --- |
| 1 | Millrow | 3 photos | Learn to move quietly |
| 2 | Millrow, Chapel Hill | 2 photos, 2 register pages | Congregation and Silt Hands appear |
| 3 | All three districts | 3 register pages, 1 tape | Lower Millrow floods (swim it on held breath). Lantern Moths. |
| 4 | Chapel Hill, Dam Works | the bell-tower photo | His route includes every place you took evidence from |
| 5 | The Dam Works | transmit from the radio room (90 s) | He hunts nonstop; the chapel bell tolls every 60 s and drowns all sound |

- **Win a night:** reach the boat with at least one new piece of evidence (hold E). The night advances only once its target is met. Anything extra (18 register pages and 6 tapes in all) changes the ending.
- **Lose a night:** Vitality hits 0 or he lands a full grab. Wake at your last lit lantern post (R), or go back to the boat and lose what you found tonight.
- **The boat:** saves, develops photos, plays Emil's tapes, and sells upgrades for salvage (brass, wire, lens): felt boots, geared crank, quiet shutter, diver's lungs, extra tool slot, rope ladders.
- **Night 5:** shut and brace the radio room door, lure him off with bells, or open the spillway to wash him away (and flood the road). The ending depends on pages, tapes and the spillway: The Record, The Roll Call, Brought In, or Silence if dawn comes first.
- **The lake rises:** the shoreline (and the boat) moves north each night. `TownLayout` holds the numbers.

## Controls

Keyboard & mouse, or an Xbox-layout gamepad (axes are set up for Windows in `ProjectSettings/InputManager.asset`).

| Key | Pad | Action | Noise |
| --- | --- | --- | --- |
| `WASD` | left stick | Move | 6 m (4 m with felt boots, 10 m in deep mud) |
| Mouse | right stick | Look | |
| `Shift` | L3 | Sprint | 18 m |
| `Ctrl` / `C` | B | Crouch and creep (toggle) | 2 m |
| `Space` | RB | Hold your breath (30 s, 40 s with lungs). Running out makes you gasp | 30 m gasp |
| `E` | A | Pick up, hide (wardrobe, pew, jetty), hold to force doors and cabinets, search crates, climb ladders, row out | door/cabinet 25 m |
| `F` | Y | Hold to crank your lantern, or a lantern post (checkpoint) | 12 m (7 m geared) |
| `L` | R3 | Lantern down to nothing / back up (scatters moths) | |
| Right mouse | LT | Raise the survey camera (12 exposures a night) | |
| Left mouse | RT | Take a photo | 4 m, 20 m with flash |
| `R` | X | Flash on/off (dark scenes blur without it) | |
| `1`-`4` / scroll | D-pad | Choose a tool | |
| `Q` | LB | Throw a bottle or handbell, sniff salts, wrap boots, pour lamp oil | lure 22 m, salts 5 m |
| `Tab` | View | Journal & map | |
| `Esc` | Menu | Pause, settings, controls | |
| `F3` | | Noise debug view | |

## The Sexton

He is blind and hunts only by sound. Every noise is a `NoisePulse` with a radius. His hearing scales by difficulty (0.75× / 1.0× / 1.3×), and each wall between him and the sound halves its reach.

- **Patrol:** walks a route that links the evidence sites. A single sound makes him stop and turn toward it. Every photo Nell takes is added to his route.
- **Investigate:** 2 sounds within 5 s, or any lure. He walks there, then sweeps his hook through the mud. If the hook touches Nell's grooves, he follows the trail.
- **Hunt:** 3 sounds within 5 s. He runs, swipes at 2 m (20 / 35 / 50 damage) and grabs at 1 m.
- **Search:** he lost her nearby, so he feels inside every wardrobe within 10 m, her most-used one first. She is safe only while holding her breath.
- **Toll:** she got more than 20 m away during a hunt. He rings his bell for 30 s and hears nothing at all.
- **Barred doors:** if one stands between him and a sound, he walks to it and smashes it, so you can lure him into opening the door for you.
- A full grab knocks Nell down for 60 / 80 Vitality on Still Water / Standard. On Undertow it ends the night.

The HUD never shows where he is. You track him by captions with direction and distance ("[FAST KNOCKING] · behind you, close"), the same way you would by ear.

## Systems

- `HushwaterBootstrap`: builds whichever screen `Campaign.Next` says (menu, night, boat, ending) on each scene load, plus the night's ambience.
- `TownBuilder`: the three districts, buildings, lake, flooded culvert, spillway, radio room, evidence scenes, items, threats, lantern posts, ladders, the Sexton's route and the NavMesh bake.
- `Campaign` / `Story`: the five-night run and its save (PlayerPrefs, at the boat), salvage and upgrades, evidence placement and text, Emil's tapes, endings. `TownLayout` (in `Campaign.cs`) holds the shoreline per night.
- `NightDirector`: dawn timer, rowing out, losing a night, lantern-post retries, pause and journal, Night 5's transmission, bell and spillway.
- `Threats`: the Congregation, Silt Hands, Lantern Moths and Eels.
- `NellKit`: the survey camera and Nell's crank lantern. `TownObjects`: cabinets, ladders, the boat, radio door, transmitter, spillway, rotten floors, water volumes.
- `Screens`: main menu, boat, journal & map, settings, endings. `HudController` / `PauseMenu` / `UiKit`: the HUD and Esc menu.
- `GameInput`: keyboard, mouse and gamepad in one place.
- `GameSettings`: difficulty table from the design doc plus volume, brightness, look sensitivity and the accessibility toggles.
- `NoiseDebugView`: F3 draws each pulse's effective radius (rust if he heard it), his NavMesh path and his state.

Models and textures under `Assets/Resources/Hushwater` are CC0 assets from Poly Haven. Sounds are generated in code (`ProceduralSounds`) until the FMOD banks exist.
