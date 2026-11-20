"""Finds high-pitched sustained passages (e.g. tyre squeal) in a 16-bit mono WAV. Per 100 ms window: level (RMS dB)
and brightness = RMS of the sample-to-sample difference / RMS of the signal (a differentiator's gain rises with
frequency: about 0.13 at 1 kHz, 0.39 at 3 kHz at 48 kHz; engine-dominated sound sits well below 0.1).
Prints runs of at least --min seconds that are both bright (> --bright) and loud (> floor + --above dB)."""
import wave, struct, math, argparse
ap = argparse.ArgumentParser(); ap.add_argument('src'); ap.add_argument('--bright', type=float, default=0.15)
ap.add_argument('--above', type=float, default=15); ap.add_argument('--min', type=float, default=0.5)
a = ap.parse_args()
w = wave.open(a.src, 'rb'); rate = w.getframerate()
s = struct.unpack('<%dh' % w.getnframes(), w.readframes(w.getnframes()))
hop = rate // 10; rows = []
for i in range(0, len(s) - hop, hop):
    seg = s[i:i + hop]
    e = sum(v * v for v in seg) / hop
    d = sum((seg[k] - seg[k - 1]) ** 2 for k in range(1, hop)) / hop
    rows.append((20 * math.log10(max(math.sqrt(e) / 32768, 1e-7)), math.sqrt(d / e) if e > 0 else 0))
floor = sorted(r[0] for r in rows)[len(rows) // 10]
print(f'{a.src.split("/")[-1][:60]}: {len(s)/rate:.0f}s floor {floor:.0f} dB; median brightness {sorted(r[1] for r in rows)[len(rows)//2]:.3f}')
run = None
for i, (lvl, br) in enumerate(rows + [(-200, 0)]):
    ok = br > a.bright and lvl > floor + a.above
    if ok and run is None: run = i
    if not ok and run is not None:
        if (i - run) * 0.1 >= a.min:
            seg = rows[run:i]
            print(f'   {run*0.1:7.1f}-{i*0.1:7.1f}s ({(i-run)*0.1:4.1f}s)  level {max(r[0] for r in seg):5.1f} dB  brightness {sum(r[1] for r in seg)/len(seg):.2f}')
        run = None
