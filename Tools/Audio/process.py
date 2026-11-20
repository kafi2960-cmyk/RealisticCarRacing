"""Prepares a recording for the game. Input: 16-bit mono WAV (convert first with afconvert). Steps: optional cut
(--start/--end seconds), RMS normalisation to --rms dBFS with the peak kept at or below -1 dBFS (gain is only reduced
for that, never limited), and for loops (--loop) an equal-power crossfade of the last --xfade seconds into the start,
so the end flows into the beginning without a click. One-shots get 5 ms fades instead."""
import wave, struct, math, argparse

ap = argparse.ArgumentParser()
ap.add_argument('src'); ap.add_argument('dst')
ap.add_argument('--start', type=float, default=0.0); ap.add_argument('--end', type=float, default=0.0)
ap.add_argument('--rms', type=float, default=-20.0); ap.add_argument('--loop', action='store_true')
ap.add_argument('--xfade', type=float, default=0.08)
ap.add_argument('--fadeout', type=float, default=0.005)
a = ap.parse_args()

w = wave.open(a.src, 'rb'); assert w.getsampwidth() == 2 and w.getnchannels() == 1, 'need 16-bit mono'
rate = w.getframerate(); s = [v / 32768.0 for v in struct.unpack('<%dh' % w.getnframes(), w.readframes(w.getnframes()))]
s = s[int(a.start * rate): int(a.end * rate) if a.end > 0 else len(s)]

if a.loop:
    n = int(a.xfade * rate)
    body, tail = s[:-n], s[-n:]
    for i in range(n):  # tail fades out over the head fading in: the loop point is inside a crossfade
        t = (i + 0.5) / n
        body[i] = body[i] * math.sin(t * math.pi / 2) + tail[i] * math.cos(t * math.pi / 2)
    s = body
else:
    f, g = int(0.005 * rate), int(a.fadeout * rate)
    for i in range(min(f, len(s))):
        s[i] *= i / f
    for i in range(min(g, len(s))):
        s[-1 - i] *= i / g

rms = math.sqrt(sum(v * v for v in s) / len(s)); peak = max(abs(v) for v in s)
gain = min(10 ** (a.rms / 20) / rms, 10 ** (-1 / 20) / peak)
out = wave.open(a.dst, 'wb'); out.setnchannels(1); out.setsampwidth(2); out.setframerate(rate)
out.writeframes(struct.pack('<%dh' % len(s), *[max(-32767, min(32767, int(round(v * gain * 32767)))) for v in s]))
print(f'{a.dst.split("/")[-1]:32s} {len(s)/rate:5.2f}s gain {20*math.log10(gain):+5.1f} dB -> rms {20*math.log10(rms*gain):5.1f} peak {20*math.log10(peak*gain):5.1f} dBFS')
