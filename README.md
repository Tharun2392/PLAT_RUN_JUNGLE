# Jungle Quest

Jungle Quest is a 2D side-scrolling platform game built in Unity. Run through a jungle course, collect sun coins, use temporary powerups, avoid hazards and patrols, and reach the temple exit.

## Requirements

- Unity Editor **6000.5.9f1** (see `ProjectSettings/ProjectVersion.txt`).
- Game scripts are C# `MonoBehaviour` components compiled into Unity's default `Assembly-CSharp` assemblies; the project has no custom `.asmdef` files.
- The active input backend is the **Input System package**. Physics 2D and audio use Unity engine modules, and the menu/HUD use **uGUI**.
- The project is set to **Linear** color space, with default desktop resolution 1920 × 1080 and Web resolution 960 × 600.
- Universal Render Pipeline 17.6.0 is active through the quality-level overrides in `ProjectSettings/QualitySettings.asset`. `Assets/Settings/UniversalRP.asset` uses `Assets/Settings/Renderer2D.asset`; the global override in `GraphicsSettings.asset` is unset, but quality overrides take precedence.
- Package versions are recorded in `Packages/manifest.json`.

### Installed packages

| Package | Version | Project use |
| --- | --- | --- |
| Input System (`com.unity.inputsystem`) | 1.20.0 | Keyboard/gamepad controls and uGUI event input |
| Unity UI (`com.unity.ugui`) | 2.5.0 | Menu, HUD, buttons, and character cards |
| Universal RP (`com.unity.render-pipelines.universal`) | 17.6.0 | Active render pipeline through quality-level overrides |
| 2D Animation | 15.1.0 | Installed 2D authoring package |
| Aseprite Importer | 5.0.3 | Installed sprite-art import package |
| PSD Importer | 14.0.3 | Installed layered-art import package |
| 2D Sprite | 1.0.0 | Installed sprite package |
| Sprite Shape | 15.0.3 | Installed 2D package |
| 2D Tooling | 3.0.1 | Installed 2D package |
| Tilemap / Tilemap Extras | 1.0.0 / 8.0.3 | Installed 2D packages |
| Timeline | 1.8.12 | Installed package |
| Visual Scripting | 1.9.12 | Installed package; gameplay is implemented in C# |
| Unity Test Framework | 1.7.0 | Installed package |

The manifest also includes Visual Studio 2.0.26 and Rider 3.0.38 integrations, Collaboration Proxy 2.13.6, Multiplayer Center 1.0.1, and standard Unity modules. These are installed project packages, not required runtime services for the game.

## Technical architecture

### Runtime structure

- `JungleQuestGame` is the main scene controller and singleton (`JungleQuestGame.I`). It binds the serialized camera, player, and baked UI; tracks score, lives, checkpoints, and powerup timers; and handles input, collision outcomes, respawning, pause, and finish states.
- The course, pickups, hazards, menu, and HUD are authored procedurally by the editor bake workflow and saved into the Unity scene. Gameplay scripts then update or animate those saved objects at runtime.
- Most visual objects use `SpriteRenderer`; menu/HUD objects use `Canvas`, `Image`, `Text`, `Button`, and `GraphicRaycaster` from uGUI. There is no TextMeshPro dependency in the current UI.
- Runtime artwork is loaded from `Resources` with `Resources.Load<Sprite>()` / `Resources.LoadAll<Sprite>()`.

### Movement and collision

- The player uses a dynamic `Rigidbody2D`, a `BoxCollider2D`, gravity scale 3.2, continuous collision detection, and Rigidbody interpolation. Rotation is frozen.
- Horizontal movement is driven in `FixedUpdate`: 8 units/second normally and 11.5 while sprint is held. The Lightning multiplier is 1.45. Jump velocity starts at 14.3; releasing jump early clamps upward velocity to 9.8 for variable-height jumps.
- Grounding uses a downward `Physics2D.Raycast` with a `ContactFilter2D`. Jump buffering is 0.16 seconds and coyote time is 0.12 seconds.
- Coins and powerups use trigger colliders. Hazards, enemy contacts, checkpoints, and the temple exit use trigger callbacks. Beetles and owls use kinematic Rigidbody2D patrol movement.
- The camera is orthographic with size 6.1. `LateUpdate` follows the player with `Vector3.SmoothDamp`; damage adds a brief camera shake.
- `Application.targetFrameRate` is set to 60 in the game controller.

### Rendering

- Universal RP is assigned by the quality profiles; the serialized current quality index is 0 (`Very Low`), and the quality-level entries point to the URP asset. The URP asset selects a 2D Renderer, uses render scale 1 and MSAA 1, and has HDR enabled. The Renderer2D asset has no custom Renderer Features.
- World art is rendered with `SpriteRenderer` and sorting orders; backdrop layers use `JungleParallax` to move at a fraction of camera speed. Environment animation is component-based: sine-wave sway for foliage, bob/glow for coins and powerups, firefly drift, and animated birds.
- Linear color space is enabled in `ProjectSettings/ProjectSettings.asset`.

### Input and UI

- Gameplay reads `Keyboard.current` and `Gamepad.current` from `UnityEngine.InputSystem`; it does not use the legacy `Input.GetKey` API.
- The scene's UI `EventSystem` uses `InputSystemUIInputModule.AssignDefaultActions()` for pointer/navigation input.
- The UI Canvas uses `ScaleWithScreenSize`, 1920 × 1080 reference resolution, and width/height matching at 0.5. HUD text auto-fits within its panel; menu and HUD are stored in the generated scene.
- Character selection is saved with `PlayerPrefs` under `JungleAdventure.SelectedCharacter`. Score, lives, coins, checkpoint, and sound-toggle state are runtime state and reset when the scene reloads.

### Scene and asset generation

- `JungleQuestSetup` is an Editor-only utility that configures sprite import settings, slices character atlases, creates the camera/player scaffold, bakes the course and UI, persists procedural UI/world sprites, and saves the scene.
- Each realistic 768 × 768 character atlas is sampled in 256 × 256 cells to create nine frame sprites. Frame mapping is: 0–3 run, 4 idle, 5 jump, 6 fall, 7 landing, and 8 death.
- Procedural UI shapes are generated as `Texture2D`/`Sprite` objects and persisted as PNGs under `Assets/JungleQuest/Generated/` when the scene is baked.
- `CurrentSceneContentVersion` in `JungleQuestGame.cs` is the source version for generated scene content. The setup hook rebuilds an older generated scene when the main scene opens in the Editor.

### Supporting gameplay components

| Component | Responsibility |
| --- | --- |
| `JungleCoin` | Coin trigger, bob/scale animation, glow pulse, and collection callback |
| `JunglePowerup` | Floating pickup animation and one-use trigger |
| `JungleHazard` / `JungleEnemy` | Damage callbacks for environmental hazards and enemy contact |
| `JungleCheckpoint` / `JungleFinish` | Trigger-based checkpoint activation and level completion |
| `JunglePatrol` / `JungleOwlPatrol` | Beetle ground patrols and owl patrol/dive movement |
| `JungleBeetleVisual` / `JungleOwlVisual` | Procedural insect/wing animation |
| `JungleParallax` / `JungleSway` | Camera-relative backdrop motion and foliage sway |
| `JungleFirefly` / `JungleBird` | Ambient light drift and background bird motion |

The current scene and world builder do not instantiate the legacy rolling-log or falling-branch components; those older scripts remain in `Assets/Scripts/` but are inactive in the current level.

## Open and play

1. Open this folder as a Unity project and let Unity finish importing assets and compiling scripts.
2. Open `Assets/JungleQuest/Scenes/JungleQuest.unity`.
3. Press **Play** in the Unity Editor.

The Jungle Quest scene is the first enabled scene in Build Settings. The default standalone resolution is 1920 × 1080; the menu and HUD use a screen-scaled Canvas.

To create a player build, open **File → Build Profiles**, select the target platform, confirm that `JungleQuest/Scenes/JungleQuest.unity` is enabled first, then choose **Build**.

### Controls

| Action | Keyboard | Gamepad |
| --- | --- | --- |
| Move | A / D or Left / Right Arrow | Left stick or D-pad |
| Sprint | Left or Right Shift | — |
| Jump | Space, W, or Up Arrow | A / South button |
| Pause / resume | Escape | Start |
| Browse characters | Left / Right Arrow while the character page is open | D-pad left / right |

Use the on-screen Sound button to mute or unmute music and sound effects.

## Gameplay

- The course contains **39 sun coins**. Each coin adds 100 points.
- The explorer starts with **3 lives**. A hazard hit removes a life and respawns the explorer at the last checkpoint; the game-over page appears when lives reach zero.
- Checkpoints are placed along the course. Reach the temple exit near the end to finish.
- Current hazards include tree stumps, thorn grass, patrolling beetles, and patrolling owls.
- Four one-use powerups are placed along the course: two Lightning speed boosts and two Magnets. Each effect lasts 10 seconds.
  - **Lightning:** increases movement speed by 45% while active. Sprinting can be combined with the boost.
  - **Magnet:** pulls nearby coins toward the explorer within a 5.5-unit radius.

## Audio

`Assets/Scripts/JungleAudio.cs` synthesizes the background music and sound effects when the game starts; there is no imported BGM track or audio plugin.

The BGM is a cached, looping stereo `AudioClip` generated from PCM samples at 22,050 Hz. It is an approximately 19.84-second, eight-bar loop with a C add9 → Am7 → Fmaj7 → G → C → G → Dm7 → G sus progression. Sine-wave chord pads, a melodic pluck line, offbeat marimba notes, bass pulses, synthesized kick and tom sounds, and short shaker-noise accents form the mix. A short equal-power crossfade smooths the loop point. Coin, jump, damage, checkpoint, finish, powerup, selection, button, and game-over sounds are synthesized separately. Music and effects use separate non-spatial `AudioSource` components; the music source loops at 0.65 volume and both sources follow the Sound toggle.

## Characters and visuals

- Choose Maya, Koa, Tavi, or Nia from the character-selection page. The choice is saved in `PlayerPrefs` between sessions.
- Realistic 768 × 768 character atlases are stored under `Assets/JungleQuest/Resources/CharacterArt/`. The editor setup script slices each 3 × 3 atlas into nine animation frames in `CharacterFrames/`.
- `JunglePlayerVisual` selects run, idle, jump, fall, landing, and death frames from the chosen character.
- Jungle backdrops and ground textures are in `Assets/JungleQuest/Resources/`. Tree-stump obstacle art, Lightning and Magnet icons, coin art, and thorn art are in `Resources/Artwork/`.
- The menu and HUD are authored procedurally with Unity uGUI by the editor bake script and saved in the main scene. At runtime, the HUD updates score, coins, lives, checkpoint, and powerup timers.

## Project layout

| Path | Purpose |
| --- | --- |
| `Assets/JungleQuest/Scenes/JungleQuest.unity` | Main playable scene |
| `Assets/Scripts/JungleQuestGame.cs` | World generation, gameplay, input, menus, HUD, and pickups |
| `Assets/Scripts/JungleAudio.cs` | Runtime-generated BGM and sound effects |
| `Assets/Scripts/JunglePlayerVisual.cs` | Explorer animation and character frames |
| `Assets/Scripts/JunglePowerup.cs` | Pickup animation and collection trigger |
| `Assets/Editor/JungleQuestSetup.cs` | Scene generation, asset import setup, and character-frame slicing |
| `Assets/JungleQuest/Resources/` | Runtime-loaded artwork and sprites |

## Generated scene workflow

The saved scene is generated from `JungleQuestGame` defaults by `JungleQuestSetup`. The current generated content version is **11**. When generated world or UI content changes, increment `CurrentSceneContentVersion` in `JungleQuestGame.cs`; the editor setup detects an older saved scene and rebuilds it when that scene opens.

To rebuild manually, open the main scene and choose **Jungle Quest → Create or Rebuild Game Scene**. This recreates the generated scene and moves it to the top of Build Settings. The **Bake All Default Objects Into Scene** command is for baking defaults into the currently open main scene.

