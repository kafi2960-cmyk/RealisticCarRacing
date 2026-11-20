# Realistic Car Racing

Cross-platform 3D car racing simulator built with Unity and the Universal Render Pipeline.

| | |
|---|---|
| Engine | Unity **6000.6.10f1** (Unity 6.6, *Supported* stream). Target: move to the current Unity 6 LTS (6000.3.x) — see roadmap. |
| Rendering | URP with `PC` and `Mobile` quality tiers (`Assets/Settings`) |
| Input | Unity Input System (new input handling only) — keyboard, gamepad, touch |
| Platforms | Windows, macOS, Android, iOS |
| Language / IDE | C# · Visual Studio Code |

## Getting started

1. Open the project in **Unity Hub → Add → `~/Projects/RealisticCarRacing`** with editor 6000.6.10f1.
2. Open `Assets/Scenes/MainMenu.unity` and press **Play**: RACE → pick an event → pick a car → START RACE.
   (You can also play `Circuit_GrandPrix` directly — it uses the last selected race and car — or the
   `VehicleTestTrack` physics sandbox.) In a race, **P** toggles a development autopilot for your car.
3. Set VS Code as the script editor: **Unity → Settings → External Tools → External Script Editor → Visual Studio Code**,
   then **Regenerate project files**. Install the recommended VS Code extensions when prompted (`.vscode/extensions.json`).

## Driving controls

All three input methods go through the Unity Input System into the same `VehicleInputState` and the same
`VehicleController` physics. Touch controls are on-screen controls that drive a virtual gamepad.

| Action | Keyboard / mouse | Gamepad | Touch |
|---|---|---|---|
| Accelerate | W / ↑ | Right trigger (analog) | GAS |
| Brake, then reverse when stopped | S / ↓ | Left trigger (analog) | BRAKE/REV |
| Steer | A D / ← → (progressive) | Left stick (analog) | Steering knob (drag) |
| Handbrake | Space | A / Cross | HAND |
| **Change camera** (Chase → Hood → Cockpit) | **C** | Right-stick click (R3) | **CAM** |
| Chase camera distance | − / = or mouse wheel | D-pad ↓ / ↑ | — |
| Look around (chase) | — | Right stick | — |
| Shift down / up (manual) | Q / E | LB / RB | − / + |
| Automatic ⇄ manual | T | Y / Triangle | A / M |
| Reset car | R | View / Select | RESET |
| Autopilot on/off (circuit) | P | — | — |

### Cameras

`VehicleCameraRig` (on the main camera) has three views, switched by `PlayerCameraInput`:

- **Chase** — swings smoothly behind the car's heading, distance adjustable 4–14 m (height rises with distance),
  speed-based field of view, right-stick look-around, and it pulls in front of walls/buildings instead of clipping.
- **Hood** — on the bonnet; **Cockpit** — driver's eye with dashboard, pillars and a steering wheel that turns
  with the front wheels. Both use the car's `VehicleCameraMounts` and lightly smoothed, partly levelled rotation.

Settings (distances, sharpness, field of view, levelling) are in the rig's Inspector.

Touch controls appear automatically on phones/tablets. To try them in the editor, use the Game view's **Simulator**
mode, or set *TouchControlsCanvas → Touch Controls Visibility → Mode* to **Always Show** and click the buttons.

## Single-player game

| Screen / system | What it does |
|---|---|
| Main menu | RACE · GARAGE · SETTINGS · QUIT over a 3D showroom with the selected car on a turntable |
| Race select | Sprint (2 laps, 3 Easy AI) · Grand Prix (3 laps, 5 Medium) · Championship Final (5 laps, 9 Hard) · Time Trial (solo); shows your best lap and finish |
| Car select | Velocity GT (RWD) · Apex RS (AWD, most power) · Strada Sport (FWD, lightest), with power / top speed / grip / agility bars |
| Garage | 8 paints per car and three setups (Balanced, High Grip, Top Speed); saved per car |
| Race start | Grid of up to 12 cars (you start at the back), 3-2-1-GO countdown with cars held on the brakes |
| AI opponents | Follow a computed racing line (outside–apex–outside), overtake slower cars on the side with room, hold back when boxed in, keep a car's width alongside, and are put back on track if stuck, beached or facing the wrong way |
| Checkpoints & laps | 16 checkpoints per lap that must be passed in order; reversing over the line never counts twice |
| HUD | Position, lap, race and lap timers, best lap, live standings with timing gaps, speedometer with RPM ring and gear, countdown, FINAL LAP / BEST LAP / WRONG WAY messages, pause button |
| Pause | Esc / Start / on-screen II: Resume · Restart · Settings · Main menu (time stops) |
| Finish | Completion screen (position, race time, best lap, new records) → Results table (positions, gaps or laps behind, best laps) with Restart · Next race · Main menu; cool-down lap drives itself |
| Save data | Selections, garage setups and records (best lap, best time, best finish, starts, wins) in `PlayerPrefs` |

Menus work with mouse/touch, keyboard (arrows/WASD, Enter, Esc) and gamepad (D-pad/stick, A, B, Start).

Content lives in `Assets/Settings/Cars`, `Assets/Settings/Races` and `Assets/Resources/GameCatalog.asset`; edit those
assets to tune cars, change laps/opponents/difficulty or add events. AI pace per difficulty is in `AIProfile.For`.

## Grand Prix circuit

`Circuit_GrandPrix` is generated entirely in code — no third-party assets — from
`Assets/Settings/Tracks/GrandPrixCircuit.asset` (a `TrackLayout`):

- 2.2 km anticlockwise lap, 17 corners (tightest radius 30 m), 12 m asphalt with painted edge lines,
  red/white kerbs on every corner, gravel traps on the outside of the slowest ones, chequered start line and grid.
- Painted concrete barriers on both sides along the whole lap (low-friction physics material so cars slide off them).
- Pit building, race-control tower, start gantry, five main grandstands plus corner grandstands, a city skyline,
  ~6,600 GPU-instanced trees and a ring of hills.
- Procedural tileable textures with normal maps (`Assets/Textures/Circuit`), URP Lit materials, procedural sky,
  real-time sun with soft shadows, baked sky ambient + reflection probe, fog, and post-processing
  (ACES tonemapping, bloom, colour grading, vignette; SSAO on the PC renderer).

To change the track, edit the control points in the layout asset and run **RealisticCarRacing → Create Circuit Scene**.
Keep corners wider than the barrier offset (15 m); `TrackLayoutTests` checks this.

### Graphics presets

Three presets — **LOW**, **MEDIUM**, **HIGH** — selectable in **SETTINGS** (main menu and pause menu) together with
resolution scale, frame-rate limit and a performance overlay. The look (sky, fog, colour grading, hills, track, cars)
and the physics are the same on every preset. Details, optimisations and measured results: **[PERFORMANCE.md](PERFORMANCE.md)**.

## Project layout

```
Assets/
  Scenes/                 MainMenu (startup), Circuit_GrandPrix, VehicleTestTrack
  Scripts/
    Cars/                 VehicleController, VehicleSettings, CarDefinition, CarBuilder, CarStats, VehicleTuning,
                          TestVehicleFactory, VehicleCameraMounts, SteeringWheelVisual
    Physics/              VehicleEngine, VehicleTransmission, VehicleWheel
    Input/                VehicleInputState + IVehicleInputSource (contract), VehicleInputActions, PlayerVehicleInput,
                          CameraInputActions, PlayerCameraInput
    Track/                TrackLayout, TrackSpline, RaceTrack, InstancedScatter
    AI/                   RaceAIDriver + AIProfile, TrackFollowerDriver (pure pursuit), AutopilotDriver
    Race/                 RaceManager, RaceProgress (checkpoints/laps), Racer, RaceStandings, RacingLine, RaceDefinition
    Managers/             GameSession, GameCatalog, PlayerProfile (+ storage), SceneFlow, EnvironmentQualityScaler,
                          GraphicsSettingsService, DeviceTierDetector, PerformanceOverlay, PerformanceBenchmark
    Camera/               VehicleCameraRig (chase / hood / cockpit)
    UI/                   UiFactory/UiTheme, MainMenuController, ShowroomController, RaceHud, RaceMenus,
                          GraphicsSettingsPanel, LoadingScreen,
                          TouchControlsFactory, TouchControlsVisibility, VehicleDebugHud
    Multiplayer/          (upcoming phase)
    Editor/               ProjectSetup, BuildMenu, VehicleTestSceneBuilder, SceneBuildUtility,
                          Circuit/ (CircuitSceneBuilder, TrackMeshBuilder, SceneryMeshBuilder, ProceduralTextures)
  Settings/               URP assets; Vehicles/TestCarSettings (car tuning); Tracks/GrandPrixCircuit (layout)
  Prefabs/                TestVehicle.prefab
  Materials/ Textures/ Models/Generated/ Audio/ UI/
  Tests/EditMode, Tests/PlayMode
```

### Vehicle architecture

```
IVehicleInputSource ──► VehicleInputState ──► VehicleController (Rigidbody + 4 WheelColliders)
  PlayerVehicleInput        (throttle, brake,       ├─ VehicleEngine        torque curve, clutch slip, rev limiter, engine braking
  (AI / network later)       steer, handbrake,      ├─ VehicleTransmission  auto/manual, reverse, shift time
                             shift requests)        ├─ VehicleWheel ×4      collider + visual sync, suspension travel
                                                    └─ steering (speed-sensitive, Ackermann), brakes, anti-roll bars, drag/downforce
```

- The car moves only through WheelCollider torques and Rigidbody forces — never by setting the transform.
- Input and physics are decoupled: anything implementing `IVehicleInputSource` can drive a car.
- All tuning lives in a `VehicleSettings` asset (*Create → RealisticCarRacing → Vehicle Settings*).
  For low-end mobiles, lower the *Simulation cost* sub-step counts.

## Editor menu: **RealisticCarRacing**

- **Build All Game Content** — regenerates everything below in order.
- **Create Game Content (Cars, Races)** — car/race assets and the GameCatalog (existing assets are kept).
- **Create Main Menu Scene** — the showroom menu scene.
- **Create Circuit Scene** — regenerates the Grand Prix circuit from its layout asset.
- **Create Vehicle Test Scene** — rebuilds the test track, test car prefab and touch UI (keeps your tuning asset).
- **Setup → Apply Project Settings** — app id, IL2CPP/ARM64 for mobile, landscape orientation, 60 Hz physics.
- **Build → macOS / Windows / Android / iOS** — builds into `Builds/` (needs the platform module installed in Unity Hub).

## Tests

**Window → General → Test Runner**, or from a terminal:

```sh
UNITY=/Applications/Unity/Hub/Editor/6000.6.10f1/Unity.app/Contents/MacOS/Unity
$UNITY -batchmode -projectPath . -runTests -testPlatform EditMode -testResults Logs/editmode.xml
$UNITY -batchmode -projectPath . -runTests -testPlatform PlayMode -testResults Logs/playmode.xml
```

## Assets and licensing

No third-party assets are included yet: the test car is built from Unity primitives and the whole circuit
(meshes and textures) is generated by the editor scripts. Record the source and licence of
every imported model, texture, sound or AI-generated asset in `ASSET_LICENSES.md` before committing it.
