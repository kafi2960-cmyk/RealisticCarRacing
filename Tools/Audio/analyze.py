"""Measures WAV files (PCM 16/24-bit): peak dBFS, RMS dBFS, and the loop seam (jump from last to first sample, and
level difference between the last and first 20 ms, relative to RMS). Optional --envelope prints 0.5 s RMS windows."""
import wave, sys, math, struct

def read(path):
    w = wave.open(path, 'rb')
    ch, width, rate, n = w.getnchannels(), w.getsampwidth(), w.getframerate(), w.getnframes()
    raw = w.readframes(n)
    full = float(1 << (8 * width - 1))
    if width == 3:
        vals = [int.from_bytes(raw[i:i+3], 'little', signed=True) for i in range(0, len(raw), 3)]
    elif width == 2:
        vals = list(struct.unpack('<%dh' % (len(raw) // 2), raw))
    else:
        raise SystemExit(f'{path}: unsupported width {width}')
    mono = [sum(vals[i:i+ch]) / ch / full for i in range(0, len(vals), ch)]
    return mono, rate

def db(x): return 20 * math.log10(max(x, 1e-9))

for path in [a for a in sys.argv[1:] if not a.startswith('--')]:
    s, rate = read(path)
    peak = max(abs(v) for v in s); rms = math.sqrt(sum(v*v for v in s) / len(s))
    k = int(rate * 0.02)
    head = math.sqrt(sum(v*v for v in s[:k]) / k); tail = math.sqrt(sum(v*v for v in s[-k:]) / k)
    jump = abs(s[-1] - s[0]) / max(rms, 1e-9)
    print(f'{path.split("/")[-1][:70]:70s} {len(s)/rate:6.2f}s peak {db(peak):6.1f} dBFS  rms {db(rms):6.1f}  seam jump {jump:4.2f}xRMS  head/tail {db(head)-db(tail):+5.1f} dB')
    if '--envelope' in sys.argv:
        win = rate // 2
        print('   0.5s RMS dB:', ' '.join(f'{db(math.sqrt(sum(v*v for v in s[i:i+win])/win)):.0f}' for i in range(0, len(s) - win, win)))
