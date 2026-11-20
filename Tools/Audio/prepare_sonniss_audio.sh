#!/bin/sh
# Recreates the game's Sonniss-derived recordings (not in the repository: the #GameAudioGDC licence forbids publishing
# the sound files). Needs macOS (afconvert) and Python 3.
#
# 1. Download from https://sonniss.com/gameaudiogdc (GDC 2020, free, royalty-free; read the licence there):
#      Sonniss.com - GDC 2020 - Game Audio Bundle Part3of14.zip
#      Sonniss.com - GDC 2020 - Game Audio Bundle Part6of14.zip
#      Sonniss.com - GDC 2020 - Game Audio Bundle Part12of14.zip
# 2. Run:  Tools/Audio/prepare_sonniss_audio.sh <folder with those zips>
# 3. In Unity: RealisticCarRacing > Create Audio Content.
#
# Every step is listed here: which recording, which seconds, the loudness target and whether it loops.
set -e
ZIPS="$1"; [ -d "$ZIPS" ] || { echo "usage: $0 <folder with Part3of14.zip, Part6of14.zip and Part12of14.zip>"; exit 1; }
HERE=$(cd "$(dirname "$0")" && pwd); PROJECT=$(cd "$HERE/../.." && pwd)
WORK=$(mktemp -d); trap 'rm -rf "$WORK"' EXIT
mkdir -p "$WORK/raw" "$WORK/m48"

# Extract only the recordings used (by file name).
python3 -I - "$ZIPS" "$WORK/raw" <<'PY'
import zipfile, glob, os, sys, re
src, out = sys.argv[1], sys.argv[2]
want = re.compile(r'(Audi A4 Allroad 2.0 TFSI Engine 4k|Lexus LFA 0[6]|Lexus LFA 12|Subaru Impreza GD WRX 08|Subaru Impreza GD WRX 11|'
                  r'Mazda Miata MX5 NBFL 1.6 Modded - (02|16)|Audi, R8, t3, Onbrd, Start|BMW_M4_t7_Ext_Start|BMW_E30_t6_Onbrd_Start|'
                  r'Car_Destruction_(Designed_t44|Chassis_Impact)|Corvette_C7_t10_Var_SFX_Horn|BMW1502 BMW 520i)')
for part in sum((glob.glob(os.path.join(src, f'*Part{n}of14.zip')) for n in (3, 6, 12)), []):
    with zipfile.ZipFile(part) as z:
        for i in z.infolist():
            if not i.is_dir() and want.search(i.filename) and i.filename.lower().endswith('.wav') and '..' not in i.filename:
                open(os.path.join(out, os.path.basename(i.filename)), 'wb').write(z.read(i))
PY
for f in "$WORK"/raw/*; do afconvert -f WAVE -d LEI16@48000 -c 1 "$f" "$WORK/m48/$(basename "$f")"; done

P="python3 -I $HERE/process.py"; M="$WORK/m48"; A="$PROJECT/Assets/Audio"
mkdir -p "$A/Engine/apex_rs" "$A/Engine/velocity_gt" "$A/Engine/strada_sport" "$A/Loops" "$A/SFX"
# Engine loops: level-matched to -16 dBFS RMS, end crossfaded into start (80 ms) so they loop without a click.
$P "$M/Subaru Impreza GD WRX 08 - ONB - Engine - 2k rpm loop mono.wav" "$A/Engine/apex_rs/engine_on_2000.wav" --loop --rms -16
$P "$M/Subaru Impreza GD WRX 11 - ONB - Exhaust - 5k rpm loop mono.wav" "$A/Engine/apex_rs/engine_on_5000.wav" --loop --rms -16
$P "$M/Lexus LFA 06 - ONB - Designed - 2k RPM loop mono.wav" "$A/Engine/velocity_gt/engine_on_2000.wav" --loop --rms -16
$P "$M/Lexus LFA 12 - INT - Designed - 8k RPM loop stereo XY.wav" "$A/Engine/velocity_gt/engine_on_8000.wav" --loop --rms -16
$P "$M/Mazda Miata MX5 NBFL 1.6 Modded - 02 - ONB - Designed - on idle gas off.wav" "$A/Engine/strada_sport/engine_on_900.wav" --start 10 --end 16 --loop --rms -16
$P "$M/Audi A4 Allroad 2.0 TFSI Engine 4k rpm loop mono.wav" "$A/Engine/strada_sport/engine_on_4000.wav" --loop --rms -16
$P "$M/Mazda Miata MX5 NBFL 1.6 Modded - 16 - INT - Cabin - 7k RPM loop stereo.wav" "$A/Engine/strada_sport/engine_on_7000.wav" --loop --rms -16
# Startups and shutdowns, cut from the start and end of whole-drive recordings (found with segments.py).
R8="$M/Audi, R8, t3, Onbrd, Start, Fast, Drive, Stop, Reverse, Stop, Drive, Engine, Right, DPA4061.wav"
M4="$M/BMW_M4_t7_Ext_Start_Idle_Blips_Steady_in_Netural_Off_XY_RSM191.wav"
E30="$M/BMW_E30_t6_Onbrd_Start_Drive_Steady_RPMs_Stop_Off_Interior_ORTF_MKH8040.wav"
$P "$R8" "$A/Engine/velocity_gt/engine_startup.wav" --start 3.2 --end 7.5 --rms -18 --fadeout 0.3
$P "$M4" "$A/Engine/apex_rs/engine_startup.wav" --start 0.1 --end 3.5 --rms -18 --fadeout 0.3
$P "$M4" "$A/Engine/apex_rs/engine_shutdown.wav" --start 125.0 --end 128.6 --rms -22 --fadeout 0.3
$P "$E30" "$A/Engine/strada_sport/engine_startup.wav" --start 3.2 --end 6.0 --rms -18 --fadeout 0.3
$P "$E30" "$A/Engine/strada_sport/engine_shutdown.wav" --start 149.0 --end 153.5 --rms -22 --fadeout 0.3
# Horn: the steady middle of one long honk, looped.
$P "$M/Corvette_C7_t10_Var_SFX_Horn_Various_XY_RSM191.wav" "$A/Loops/horn_loop.wav" --start 11.0 --end 14.5 --loop --rms -18
# Crashes: the first 2.5 s of each designed impact and chassis drop, faded out.
i=0; for st in 0.64 7.78 16.72 27.34 36.66 49.26; do
  $P "$M/Car_Destruction_Designed_t44_Impacts_Interor.wav" "$A/SFX/impact_heavy_2$i.wav" --start $st --end $(python3 -c "print($st+2.5)") --rms -20 --fadeout 0.6; i=$((i+1)); done
i=1; for st in 9.64 21.78; do
  $P "$M/Car_Destruction_Chassis_Impact_t36_Ext_Drop_on_Ground_Metal_Crash_Bounce_Various_MKH8060.wav" "$A/SFX/impact_heavy_3$i.wav" --start $st --end $(python3 -c "print($st+2.5)") --rms -20 --fadeout 0.6; i=$((i+1)); done
# Tyres: the steadiest squeal (about 1.3 kHz) and a braking skid (about 2 kHz) from a BMW 520i skid take, found with
# brightness.py (high-frequency, sustained) and checked for a steady pitch; looped with a 150 ms crossfade.
BMW="$M/BMW1502 BMW 520i Exhaust Rear Bumper Interior QUAD Drive Skid.wav"
$P "$BMW" "$A/Loops/tyre_squeal.wav" --start 337.35 --end 338.55 --loop --xfade 0.15 --rms -18
$P "$BMW" "$A/Loops/tyre_lockup.wav" --start 315.2 --end 316.3 --loop --xfade 0.15 --rms -18
echo "Done. Now run RealisticCarRacing > Create Audio Content in Unity."
