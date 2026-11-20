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
| Main menu | RACE · GARAGE · SETTINGS · QUIT over the showroom, with the selected car turning on the turntable |
| Race select | Sprint (2 laps, 3 Easy AI) · Grand Prix (3 laps, 5 Medium) · Championship Final (5 laps, 9 Hard) · Time Trial (solo); shows your best lap and finish. Choosing a race opens the garage |
| Garage | Vehicle selection and preparation (see below), with **START RACE** |
| Race start | Grid of up to 12 cars (you start at the back), 3-2-1-GO countdown with cars held on the brakes |
| AI opponents | Follow a computed racing line (outside–apex–outside), overtake slower cars on the side with room, hold back when boxed in, keep a car's width alongside, and are put back on track if stuck, beached or facing the wrong way |
| Checkpoints & laps | 16 checkpoints per lap that must be passed in order; reversing over the line never counts twice |
| HUD | Position, lap, race and lap timers, best lap, live standings with timing gaps, speedometer with RPM ring and gear, countdown, FINAL LAP / BEST LAP / WRONG WAY messages, pause button |
| Pause | Esc / Start / on-screen II: Resume · Restart · Settings · Main menu (time stops) |
| Finish | Completion screen (position, race time, best lap, new records) → Results table (positions, gaps or laps behind, best laps) with Restart · Next race · Main menu; cool-down lap drives itself |
| Save data | Selections, garage setups and records (best lap, best time, best finish, starts, wins) in `PlayerPrefs` |

Menus work with mouse/touch, keyboard (arrows/WASD, Enter, Esc) and gamepad (D-pad/stick, A, B, Start).

### Garage

| Feature | How |
|---|---|
| Choose a car | `<` / `>` beside the name: Velocity GT (RWD) · Apex RS (AWD, most power) · Strada Sport (FWD, lightest) |
| Turn the car | Drag with the mouse or one finger (with a little inertia) · Q / E · gamepad right stick. Dragging up/down raises or lowers the camera |
| Zoom | Mouse wheel · pinch · + / − keys and buttons · gamepad triggers; limited so the camera never enters the car |
| Views | **3/4 · FRONT · SIDE · REAR** buttons (gamepad bumpers step through them); the turntable eases round |
| Paint | 8 named metallic clear-coat paints per car |
| Wheels | 4 designs: Split 10-Spoke (machined silver) · Forged Y-Spoke (gloss black) · Cross-Spoke Mesh (gunmetal) · Turbine 7-Spoke (satin bronze); cosmetic, same tyre |
| Setup | Balanced · High Grip · Top Speed, with power / top speed / grip / agility bars |
| Specifications | Power and torque (with rpm), 0–100 km/h, top speed, weight, power-to-weight, drivetrain, gearbox. Calculated from the car's physics and current setup; `CarSpecsTests` times real 0–100 runs to check the figure |
| Start Race | Races the selected event in the selected car, paint, wheels and setup (all saved per car) |

The showroom is a seamless cyclorama with a polished floor, overhead softboxes, a ring light and wall light strips.
Those are unlit emissive surfaces whose reflections come from a baked, box-projected reflection probe, so on a phone
the room costs a few draw calls and no extra real-time lights. One key light casts shadows; two accents don't; a
soft contact shadow keeps the car grounded on presets with short shadows. The camera keeps the whole car framed in
the area beside the menu panel at any screen shape, and the anti-aliasing follows the graphics preset.

**The selectable cars still use the box-bodied placeholder body** (see *Car visuals*): the garage is ready for
licensed car models, but none has been bought or imported yet.

Content lives in `Assets/Settings/Cars`, `Assets/Settings/Races` and `Assets/Resources/GameCatalog.asset`; edit those
assets to tune cars, change laps/opponents/difficulty or add events. AI pace per difficulty is in `AIProfile.For`.

## Audio

The sound system is built and tested, but **no recordings are in the project yet**: until clips are added under
`Assets/Audio`, the game is silent. Nothing is synthesised to stand in for missing recordings.

| Part | What it does |
|---|---|
| `CarEngineAudio` | Steady-RPM engine loops, equal-power cross-faded by the engine's actual RPM and pitched to it; on-throttle and coasting recordings blended by engine load; startup/shutdown; exhaust crackles on lift-off at high RPM; muffled from the cockpit |
| `GearShiftAudio` | One sound per actual gear change (up, down, drive/reverse engagement); firmer for manual shifts |
| `TireAudio` | Squeal, lock-up, drift and brake layers from each wheel's slip; rolling noise per surface (asphalt, kerb, grass, gravel via `TrackSurfaces`) |
| `EnvironmentAudio` | Wind with speed, horn (H / left-stick click / HORN touch button), impacts scaled by impact speed, barrier scrape |
| `UIAudioManager` | Menu hover/click/back, countdown beeps and GO, lap and finish cues, menu music |
| `VehicleAudioManager` | Creates each car's sources once at spawn, routes them to the mixer, pauses silent loops (frees voices) and AI cars beyond 140 m |
| Mixer | `Assets/Audio/Mixer/GameMixer.mixer`: Master → Engine, Transmission, Tires, Environment, UI, Music, each with an exposed volume |
| Settings | **AUDIO** in the main menu and pause menu: master and per-group volume in 10 % steps, mute; saved in the profile |

Adding recordings: put them under `Assets/Audio/Engine`, `Loops`, `SFX`, `UI` or `Music` (import settings are set
per folder by `AudioImportRules`) and name them as `AudioContentBuilder` expects (`engine_on_3000`,
`engine_off_1500`, `tyre_squeal`, `impact_light_01`, `ui_click`, ...), then run **RealisticCarRacing → Create
Audio Content**. Record each file's source and licence in `ASSET_LICENSES.md`.

## Grand Prix circuit

`Circuit_GrandPrix` is generated entirely in code — no third-party assets — from
`Assets/Settings/Tracks/GrandPrixCircuit.asset` (a `TrackLayout`):

- 2.2 km anticlockwise lap, 17 corners (tightest radius 30 m), 12 m asphalt with painted edge lines,
  red/white kerbs on every corner, gravel traps on the outside of the slowest ones, chequered start line and grid.
- Painted concrete barriers on both sides along the whole lap (low-friction physics material so cars slide off them).
- Chain-link debris fences on the barriers, pit building, race-control tower, start gantry, five main grandstands
  plus corner grandstands, a city skyline, ~6,600 GPU-instanced trees (three species, each with a detailed and a
  distant mesh) and a ring of hills.
- Procedural tileable textures with normal and smoothness maps (`Assets/Textures/Circuit`): asphalt with aggregate,
  repair patches, cracks and a polished racing line plus close-up detail maps; barriers with precast joints, chips,
  tyre scuffs and road grime.
- Cloud sky (`Assets/Shaders/Sky.shader`) that also provides the ambient light; real-time sun with soft shadows;
  baked reflection probes round the lap (box-projected); fog matched to the horizon; post-processing (ACES
  tonemapping, grading, bloom, vignette, camera motion blur on High; SSAO on High).

### Car visuals

Until the licensed car model is bought, the race cars are still the **box-bodied placeholder** — the body shape is
not realistic. Everything around it is the production system an imported model will use (`CarVisualKit`,
generated by **Create Game Content**): clear-coat metallic paint (URP Complex Lit), tinted glass that is hidden from
the cockpit, lathed tyres with tread and sidewall, ten-spoke rims, drilled brake discs and calipers that steer
but don't spin, head/tail lamps, **brake lights from the brake pedal**, reverse lights, headlight beams on the
player's car (Medium/High), seats, binnacle gauges with live rev and speed needles, and per-car LODs. Physics are
unchanged (`CarVisualsTests` checks identical driving with and without the kit).

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
                          TestVehicleFactory, VehicleCameraMounts, SteeringWheelVisual, CarVisualKit, CarLights,
                          BrakeCaliperVisual, DashboardGauges
    Physics/              VehicleEngine, VehicleTransmission, VehicleWheel
    Input/                VehicleInputState + IVehicleInputSource (contract), VehicleInputActions, PlayerVehicleInput,
                          CameraInputActions, PlayerCameraInput
    Track/                TrackLayout, TrackSpline, RaceTrack, InstancedScatter
    AI/                   RaceAIDriver + AIProfile, TrackFollowerDriver (pure pursuit), AutopilotDriver
    Race/                 RaceManager, RaceProgress (checkpoints/laps), Racer, RaceStandings, RacingLine, RaceDefinition
    Managers/             GameSession, GameCatalog, PlayerProfile (+ storage), SceneFlow, EnvironmentQualityScaler,
                          GraphicsSettingsService, DeviceTierDetector, PerformanceOverlay, PerformanceBenchmark
    Camera/               VehicleCameraRig (chase / hood / cockpit)
    UI/                   UiFactory/UiTheme, MainMenuController, ShowroomController, ShowroomOrbit, RaceHud, RaceMenus,
                          GraphicsSettingsPanel, LoadingScreen,
                          TouchControlsFactory, TouchControlsVisibility, VehicleDebugHud
    Multiplayer/          (upcoming phase)
    Editor/               ProjectSetup, BuildMenu, VehicleTestSceneBuilder, SceneBuildUtility, CarKitBuilder,
                          Circuit/ (CircuitSceneBuilder, TrackMeshBuilder, SceneryMeshBuilder, ProceduralTextures)
  Settings/               URP assets; Vehicles/TestCarSettings (car tuning); Tracks/GrandPrixCircuit (layout)
  Prefabs/                TestVehicle.prefab
  Shaders/                Sky (cloud sky + sun)
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

Before/after screenshots of fixed Game-camera views (circuit views with the cars held on the grid, and the garage
with its UI), on demand:

```sh
CAPTURE_DIR=$PWD/Logs/shots CAPTURE_TAG=after $UNITY -batchmode -projectPath . -runTests -testPlatform PlayMode -testFilter VisualCaptureTests
```

## Assets and licensing

No third-party assets are included yet: the placeholder car body is built from Unity primitives, and the car
detail kit, the sky, the showroom and the whole circuit (meshes, textures, shader) are generated by the project's own scripts. Record the source and licence of
every imported model, texture, sound or AI-generated asset in `ASSET_LICENSES.md` before committing it.
