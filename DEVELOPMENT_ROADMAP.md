# Development Roadmap — Realistic Car Racing

Status key: ✅ done · 🔄 in progress · ⬜ planned

## Phase 0 — Environment and project foundation
- ✅ URP project created (PC + Mobile quality tiers), Input System enabled
- ✅ Folder structure, assembly definitions, `.gitignore`, `.gitattributes`, `.editorconfig`, VS Code config
- ✅ Cross-platform player settings (app id, IL2CPP + ARM64 on mobile, landscape, 60 Hz physics)
- ✅ Build menu for macOS / Windows / Android / iOS; macOS build verified
- ⬜ Install Unity 6 **LTS** (6000.3.x) with Android, iOS and Windows (Mono) build support, and move the project to it
- ⬜ Install Git LFS, enable the LFS rules in `.gitattributes`, first commit, push to GitHub
- ⬜ Install the VS Code Unity extension; point Xcode with `xcode-select`
- ⬜ Replace the placeholder application id `com.realisticcarracing.game`

## Phase 1 — Player vehicle physics
- ✅ Rigidbody + 4 WheelColliders, suspension, tyre friction curves, anti-roll bars
- ✅ Engine torque curve, clutch slip, rev limiter, engine braking
- ✅ Automatic and manual gearbox, reverse, shift time
- ✅ Brakes with bias, handbrake with rear grip loss, hold-at-rest brake
- ✅ Speed-sensitive Ackermann steering; wheel spin/steer visuals
- ✅ Keyboard, gamepad and touch input via the Input System, decoupled from physics
- ✅ Test track scene, primitive test car, chase camera, debug HUD
- ✅ Edit-mode and play-mode tests
- ⬜ Traction control / ABS (optional assists), tyre surface types (asphalt, grass, gravel)
- ⬜ Tuning pass on real hardware (desktop + low-end Android)

## Phase 2 — Race environment and real assets
- ✅ Procedural Grand Prix circuit (2.2 km): asphalt + painted lines, kerbs, gravel, barriers, start/grid, pits,
  grandstands, tower, city, hills, instanced trees, sky/fog/reflections/post-processing
- ✅ Mobile and PC quality tiers (URP assets + `EnvironmentQualityScaler`), mobile texture overrides
- ✅ Circuit tests: continuous road/barrier collision, autopilot laps on three lines, barrier-impact recovery
- ⬜ Surface grip per material (grass, gravel, kerbs) via WheelHit collider lookup
- ⬜ Elevation changes and banking (layout points already carry Y; road/barrier generation needs slope support)
- ⬜ Choose licensed car models (Asset Store / commercial licence): separate wheel meshes, centred pivots, LODs
- ⬜ Car prefab pipeline: model → wheel pivots → colliders → `VehicleSettings` per car
- ⬜ Optionally replace procedural buildings/trees with licensed URP assets (keep the generated track as the base)
- ⬜ glTFast import path for GLB props; AI-generated (Hugging Face) props and textures where suitable
- ⬜ `ASSET_LICENSES.md` kept up to date

## Phase 3 — Race game loop
- ✅ Main menu with 3D showroom, race select, car select (3 cars), garage (paint + setup presets), saved profile
- ✅ Race manager: grid, countdown, checkpoints, laps, timing, positions with gaps, finish, classification
- ✅ HUD (position, lap, timers, standings, speedometer/RPM/gear, messages), pause, completion and results screens
- ✅ Restart, next race, quit to menu; all menus usable by mouse, touch, keyboard and gamepad
- ✅ End-to-end tests: every menu button, complete race start→results, menus/pause per input method
- ✅ Cameras: chase (smooth, adjustable distance, look-around, wall avoidance), hood, cockpit; switching on C / R3 / CAM
- ✅ End-to-end input tests per method (keyboard, gamepad, touch) through the Input System
- ⬜ Replay and TV-style trackside cameras; look-back
- ⬜ Control rebinding menu (actions are already exposed by `PlayerVehicleInput.Actions` / `PlayerCameraInput.Actions`)
- ⬜ Race HUD (speedometer, tachometer, gear, lap times, position, minimap)
- ⬜ Menus: main, car select, track select, settings (graphics, controls, rebinding)
- ⬜ Engine/tyre/collision audio

## Phase 4 — AI opponents
- ✅ `RaceAIDriver`: racing line, overtaking, side-by-side avoidance, stuck/off-track/wrong-way recovery, 3 difficulties
- ⬜ Optimised racing line (minimum-curvature solve) and per-corner braking points
- ⬜ Defensive driving, slipstream, mistakes; optional rubber-banding

## Phase 5 — Online multiplayer
- ⬜ Choose networking stack (Netcode for GameObjects + Unity Relay/Lobby, or alternative)
- ⬜ Networked input source and client-side prediction / reconciliation for cars
- ⬜ Lobby, matchmaking, race sync, disconnect handling

## Phase 6 — Graphics and performance
- ✅ LOW / MEDIUM / HIGH presets (URP assets + scene scaling), graphics settings menu, first-launch device recommendation
- ✅ Draw calls −54…88 %, memory −5…52 %, async loading, near-zero per-frame garbage (see PERFORMANCE.md)
- ✅ Benchmark mode (`-benchmark`) and in-game performance overlay; measured on Apple M4 MacBook Air
- ⬜ Measure on Windows PCs, mid/low-end Android and iPhone (needs build modules and devices)
- ⬜ Weather / time of day; adaptive resolution from measured frame time; engine and tyre audio

## Phase 7 — Release
- ⬜ CI builds for all four platforms (Windows IL2CPP needs a Windows runner)
- ⬜ Store assets, signing (Apple Developer Program, Google Play), privacy policy
- ⬜ Beta testing, crash reporting, analytics
