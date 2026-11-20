"""Finds sound events in a 16-bit mono WAV: 20 ms RMS envelope, noise floor = 10th percentile, events = runs above
floor + --above dB (gaps shorter than --gap s merged). Prints each event's start, end, length and peak level.
With --coarse, also prints the envelope in --step second windows."""
import wave, struct, math, argparse
ap = argparse.ArgumentParser(); ap.add_argument('src'); ap.add_argument('--above', type=float, default=15)
ap.add_argument('--gap', type=float, default=0.15); ap.add_argument('--coarse', action='store_true'); ap.add_argument('--step', type=float, default=0.5)
a = ap.parse_args()
w = wave.open(a.src, 'rb'); rate = w.getframerate()
s = struct.unpack('<%dh' % w.getnframes(), w.readframes(w.getnframes()))
hop = rate // 50
env = [math.sqrt(sum(v * v for v in s[i:i + hop]) / hop) / 32768 for i in range(0, len(s) - hop, hop)]
db = [20 * math.log10(max(e, 1e-7)) for e in env]
floor = sorted(db)[len(db) // 10]
print(f'{a.src.split("/")[-1][:60]}: {len(s)/rate:.1f}s, floor {floor:.0f} dB, max {max(db):.0f} dB')
if a.coarse:
    k = int(a.step * 50)
    print('  ', ' '.join(f'{max(db[i:i+k]):.0f}' for i in range(0, len(db), k)))
events, start, last = [], None, None
for i, d in enumerate(db):
    if d > floor + a.above:
        if start is None or (i - last) * 0.02 > a.gap:
            if start is not None: events.append((start, last))
            start = i
        last = i
if start is not None: events.append((start, last))
for b, e in events:
    print(f'   event {b*0.02:7.2f}-{(e+1)*0.02:7.2f}s  len {(e-b+1)*0.02:5.2f}s  peak {max(db[b:e+1]):5.1f} dB')
