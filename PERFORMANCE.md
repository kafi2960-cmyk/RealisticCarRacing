# Performance — Realistic Car Racing

## Graphics presets

Choose in **Main menu → SETTINGS** or **Pause → SETTINGS**. On first launch the game picks a preset from the
hardware (`DeviceTierDetector`) — a heuristic starting point, not a compatibility promise.

| | LOW | MEDIUM | HIGH |
|---|---|---|---|
| Intended for | Older / lower-powered devices | Mid-range computers and phones | Powerful computers |
| Renderer | Forward | Forward | Forward+ |
| Resolution scale | 75 % | 90 % | 100 % |
| Anti-aliasing | FXAA (post-process, one pass) | MSAA 2× | MSAA 4× |
| Sun shadows | 45 m · 1 cascade · 1024 · hard | 90 m · 2 cascades (split at 20 m) · 2048 · soft | 180 m · 4 cascades (9 / 25 / 68 m) · 2048 · soft (high) |
| Extra lights | Per-vertex, 2 per object | Per-pixel, 4 | Per-pixel, 8, with shadows |
| Headlight beams (player's car, 2 unshadowed spots) | Off (lamps still glow) | On | On |
| Ambient occlusion (SSAO) | Off | Off | On (intensity 0.7, radius 0.45 m) |
| Reflection probes (all baked) | Nearest probe | Nearest probe | Blended + box-projected |
| Texture resolution | Half (mip limit 1) | Full | Full |
| Texture streaming budget | 128 MB | 256 MB | 512 MB |
| Anisotropic filtering | Off | Per texture | Forced |
| LOD bias | 0.6 | 1.0 | 1.6 |
| Object draw distance | 900 m | 1,700 m | 3,200 m |
| Trees | 35 %, 250 m, no shadows | 65 %, 550 m, shadows < 60 m | 100 %, 1,100 m, shadows < 150 m |
| Tree detail (full mesh, then a ~10× lighter one) | 60 m | 110 m | 180 m |
| Debris fences (alpha-tested) | Off | On | On |
| City skyline | Off | On | On |
| Bloom | Off | Quarter resolution | Half resolution, high-quality filtering |
| Camera motion blur | Off | Off | On (low quality) |
| Frame-rate cap (default) | 30 on phones, 60 elsewhere | 60 | 60 |

**Identical on every preset (the game's look and handling):** cloud sky, sun direction, fog, HDR + ACES tonemapping
and colour grading, vignette, hills on the horizon (Background layer, always drawn), track and barrier materials,
kerbs, buildings, cars and their materials (clear-coat paint, glass, lamps, brake lights), HUD — and **physics** (60 Hz, same tyre/suspension simulation), so a car handles the same on every device.

The menu also offers a **resolution scale** override (50–100 %), a **frame-rate limit** (30 / 60 / 120 on high-refresh
displays / MAX) and a **performance overlay** (FPS, frame time, CPU time, memory, preset).

## What was optimised

| Area | Change |
|---|---|
| Draw calls | Tree shadows only for chunks within the shadow distance (instanced draws are repeated per cascade); tree chunks 120 → 200 m; car interior (20+ pieces) merged into 3 renderers and only drawn in cockpit view; distant cars drop detail then cull (LODGroup); SRP Batcher + static batching kept |
| Culling | Road / barriers / kerbs / gravel split into ~200 m render chunks so off-screen track is skipped (collision still uses seamless whole-loop meshes); grandstands, pit, tower and gantry cull when tiny; per-layer cull distances |
| Textures | Per-preset mip limit, mip streaming with budgets, half-size ASTC on Android/iOS |
| Shadows / lighting / reflections | Per-preset shadow distance, cascades, resolution and softness; per-vertex extra lights on Low; SSAO and probe blending only on High; all probes baked |
| LOD | Car LODs, scenery culling LODs, LOD bias per preset, tree distance/density per preset |
| Object pooling | Showroom keeps one model per car and repaints it with a MaterialPropertyBlock (no rebuild per paint); HUD rows are built once and reused |
| Garbage | HUD only rebuilds text when values change (timers at 20 Hz), pre-built speed/gear strings, cached sort comparer → **44–98 bytes allocated per frame** in a race |
| Physics | MeshColliders cooked at build time; queries ignore triggers; collision callbacks reused; catch-up capped at 6 steps per frame (no spiral on slow devices); camera ignores the Vehicles layer. Simulation itself is identical on every preset |
| Audio | 24 real / 64 virtual voices; "Good latency" DSP buffer; import rules for future clips (SFX mono + decompress-on-load + ADPCM on phones, engine loops compressed in memory, music streamed). **The game has no audio assets yet.** |
| Memory | Render-only meshes keep no CPU copy (not readable), merged meshes upload-and-release, texture mip limit/streaming, test track excluded from player builds |
| Loading | Asynchronous scene loads behind a loading screen; collision meshes pre-cooked |

## Visual upgrade (rendering pipeline) — what it costs and how it was kept cheap

| Area | What was added | How it stays affordable |
|---|---|---|
| Sky and lighting | Cloud sky shader (gradient, sun disc + halo, two-octave clouds with sun-side shading); stronger, lower sun; fog colour = horizon | Sky shades only uncovered pixels with 3 texture reads; ambient light is baked from it (no real-time GI) |
| Reflections | Baked probes: whole circuit, main straight (box-projected), and 7 more every ~275 m round the lap | No per-frame cost. A **real-time car-following probe was built, measured and removed**: on High it raised SetPass calls 73 → 124 and caused 13.5 ms frame spikes |
| Car materials | Clear-coat metallic paint (Complex Lit), tinted "preserve specular" glass, textured rubber, metals, leather, plastics, emissive lamps and gauges | Lamps switch by swapping shared materials (SRP Batcher stays intact); each lamp pair merged into one renderer; rims, discs, calipers, lamps and glass don't cast shadows |
| Car detail and LOD | Lathed tyres, ten-spoke rims, drilled discs, steering calipers; cockpit with live gauges | Close up only: beyond ~16 m cars switch to body + glass + 14-segment tyres; tiny on screen they are culled; cockpit drawn only in cockpit view and its gauges only animate then |
| Asphalt / barriers | Albedo, normal and smoothness maps (aggregate glints, polished racing line, patches, cracks; joints, chips, scuffs) plus asphalt close-up detail maps | Same 1024/512 resolutions as before (no texture got bigger); detail maps are 256 px; phones get half size ASTC |
| Trees | Three species with fuller crowns | Detailed mesh only near the camera (distance per preset), ~10× lighter mesh beyond, still GPU-instanced per 200 m chunk |
| Fences | Chain-link debris fences on both barriers | Alpha-tested (costly on tile-based phone GPUs), so off on Low; no shadows; culled in ~200 m chunks |

## Measured results

**Only one device has been measured.** Development build, 1920×1080 windowed, vSync off and frame cap removed
(to measure headroom), 6-car race on the Grand Prix circuit with the autopilot driving, 30 s after a 6 s warm-up.

**Device: MacBook Air (Mac16,12) — Apple M4 (10-core CPU, 8-core GPU), 16 GB, macOS 26.6.2, Metal, on AC power.**

| Metric | Before (template "PC") | LOW | MEDIUM | HIGH |
|---|---|---|---|---|
| Average FPS | 235 | 952 | 667 | 227 |
| 1 % low FPS | 172 | 574 | 399 | 194 |
| Median frame time | 4.17 ms | 1.01 ms | 1.49 ms | 4.39 ms |
| 99th percentile frame time | 5.81 ms | 1.74 ms | 2.50 ms | 5.15 ms |
| CPU main thread (median) | 3.02 ms | 0.77 ms | 1.48 ms | 2.76 ms |
| Draw calls | **1,350** | 158 | 257 | 618 |
| SetPass calls | 58 | 29 | 35 | 53 |
| Triangles | 1,255 k | 83 k | 261 k | 1,274 k |
| Total memory | 432 MB | 207 MB | 230 MB | 409 MB |
| Texture memory (incl. render targets) | 233 MB | 13 MB | 36 MB | 197 MB |
| Managed heap | 8.5 MB | 8.6 MB | 8.6 MB | 8.6 MB |
| GC allocation per frame | — | 44 B | 63 B | 98 B |
| Race scene load, first run after a build | 1.47 s | 0.82 s | — | — |

Notes:
- **HIGH vs. "before":** HIGH draws ~54 % fewer calls but adds 4× MSAA (the template preset had none), so its frame
  time is similar. A first run of the same benchmark measured 918 / 573 / 192 FPS — the fanless M4 varies by ~15 %
  between runs.
- **GPU times** reported by `FrameTimingManager` on Apple Silicon overlap consecutive frames and read higher than the
  frame time; use them only to compare presets with each other.
- **Load times:** once the OS has the files cached, later loads take 0.03–0.06 s on this Mac, so only first-run
  numbers are comparable. 
- Render counters come from a development build; release builds are slightly faster.

### After the visual upgrade

Same device, build type, resolution, race and method as above; the benchmark was run twice (the fanless M4 varies by
10–20 % between runs, so both runs are shown).

| Metric | LOW (run 1 / 2) | MEDIUM | HIGH |
|---|---|---|---|
| Average FPS | 808 / 645 (before: 952) | 498 / 483 (before: 667) | 144 / 152 (before: 227) |
| 1 % low FPS | 467 / 399 | 311 / 318 | 128 / 132 (before: 194) |
| Median frame time | 1.19 / 1.50 ms | 1.99 / 2.08 ms | 6.91 / 6.56 ms |
| 99th percentile frame time | 2.14 / 2.51 ms | 3.21 / 3.14 ms | 7.81 / 7.55 ms |
| CPU main thread (median) | 0.98 / 1.49 ms | 1.99 / 2.06 ms | 4.05 / 3.48 ms |
| Draw calls | 195 (before: 158) | 278 / 320 (before: 257) | 821 / 795 (before: 618) |
| SetPass calls | 39 | 44 | 73 |
| Triangles | 122 k | 293–313 k | 1,151 k (before: 1,274 k) |
| Total memory | 262 MB | 267–274 MB | 467 MB |
| Texture memory | 24 MB | 36 MB | 215 MB |
| GC allocation per frame | 52–65 B | 58–60 B | 146–153 B |

The upgrade costs roughly 15–35 % of the frame rate on this Mac. The slowest preset (HIGH) still renders a 1080p
frame in under 8 ms even at the 99th percentile, about twice as fast as a 60 FPS target needs. Phone costs are
**not measured**. The heaviest additions are on Medium/High only (fences, headlight beams, motion blur,
high-quality bloom, SSAO, probe blending), but Low still pays for the sky shader, the extra car parts and FXAA.

### Garage (showroom)

Same device and build type; the benchmark now measures the garage before each race: main menu loaded, garage
open, car turning on the turntable, 15 s after a 4 s warm-up. Two runs.

| Metric | LOW | MEDIUM | HIGH |
|---|---|---|---|
| Average FPS | 780 / 799 | 582 / 598 | 172 / 174 |
| 1 % low FPS | 529 / 511 | 355 / 368 | 152 / 156 |
| 99th percentile frame time | 1.89 / 1.96 ms | 2.82 / 2.72 ms | 6.59 / 6.42 ms |
| CPU main thread (median) | 1.06–1.14 ms | 0.55–0.82 ms | 0.57 ms |
| Draw calls | 59 | 59 | 96 |
| SetPass calls | 33 | 32 | 45 |
| Triangles | 23 k | 24 k | 46 k |
| Total memory | 249 MB | 253 MB | 428–444 MB |
| GC allocation per frame | 25 B | 33–34 B | 0 B |
| Main menu load (first run) | 1.52 s | 1.24 s | 1.87 s |

How it stays cheap: the showroom's light panels, ring light and wall strips are unlit emissive meshes (no lights),
their reflections come from one baked, box-projected 256 px reflection probe, the room is a handful of static
meshes, only the key light casts shadows, and only the selected car is active (others stay pooled, hidden). HIGH
costs most because of 4× MSAA, SSAO and full resolution — not geometry. Phone frame rates are **not measured**.

The race numbers from the same two runs (LOW 584 / 620, MEDIUM 427 / 441, HIGH 139 / 137 FPS; draw calls 194, 278–322,
821–829) are within this Mac's run-to-run variation of the previous measurements, with identical SetPass counts:
AI cars now get varied wheel designs, but each design is one shared material.

## Compatibility — what is and isn't known

| Device category | Status |
|---|---|
| Apple Silicon Mac (M4, 16 GB) | **Measured** (above): all presets far above 60 FPS at 1080p |
| Other Macs, Windows PCs | Not tested. Windows builds need the Windows module in Unity Hub |
| Android phones / tablets | **Not tested.** Android build support is not installed and no device was connected |
| iPhone / iPad | **Not tested.** iOS build support is not installed and no device was connected |

No compatibility claims are made for untested devices. The preset values for phones are reasoned targets (lower
resolution, short shadows, fewer trees, 30 FPS cap on Low) that must be verified on real hardware.

## Re-running the benchmark

1. Build a development player: **RealisticCarRacing → Build → …** with *Development Build*, or from a terminal
   `Unity -batchmode -quit -projectPath . -executeMethod RealisticCarRacing.EditorTools.BuildMenu.BuildFromCommandLine -buildPlatform macOS -development`
2. Run it with:
   `"Builds/macOS/RealisticCarRacing.app/Contents/MacOS/Realistic Car Racing" -benchmark -benchmarkPresets Low,Medium,High -benchmarkSeconds 30 -benchmarkOutput results.json -screen-fullscreen 0 -screen-width 1920 -screen-height 1080`
3. The JSON lists every metric above per preset, plus device, GPU and resolution.

For a phone, build the player with the platform module installed and pass the same arguments
(Android: `adb shell am start … -e unity "-benchmark …"`), or turn on **Settings → Performance overlay** and read FPS live.
