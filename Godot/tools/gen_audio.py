#!/usr/bin/env python3
"""Synthesizes every sound offline with the Python stdlib — no samples, no
licences, fully reproducible. Writes 16-bit WAVs into Godot/assets/audio/.

  python3 Godot/tools/gen_audio.py
"""
import math
import os
import random
import struct
import wave

RATE = 22050
OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "assets", "audio")
os.makedirs(OUT, exist_ok=True)
random.seed(20260820)


def write(name, samples):
    path = os.path.join(OUT, name + ".wav")
    clamped = [max(-1.0, min(1.0, s)) for s in samples]
    with wave.open(path, "w") as f:
        f.setnchannels(1)
        f.setsampwidth(2)
        f.setframerate(RATE)
        f.writeframes(b"".join(struct.pack("<h", int(s * 32000)) for s in clamped))
    print("WROTE", path, f"{len(samples)/RATE:.2f}s")


def env(i, n, attack=0.01, release=0.4):
    t = i / n
    a = min(1.0, t / max(attack, 1e-6))
    r = max(0.0, 1.0 - max(0.0, t - (1.0 - release)) / max(release, 1e-6))
    return a * r


def noise():
    return random.uniform(-1, 1)


def hit():
    n = int(RATE * 0.16)
    return [ (noise() * 0.7 + math.sin(2 * math.pi * 180 * i / RATE) * 0.5)
             * env(i, n, 0.002, 0.85) for i in range(n) ]


def kick():
    n = int(RATE * 0.22)
    out = []
    for i in range(n):
        f = 120 * math.exp(-i / (RATE * 0.05)) + 40
        out.append((math.sin(2 * math.pi * f * i / RATE) * 0.9 + noise() * 0.15)
                   * env(i, n, 0.001, 0.7))
    return out


def crash():
    n = int(RATE * 0.9)
    out = []
    lp = 0.0
    for i in range(n):
        lp += (noise() - lp) * 0.25
        metal = math.sin(2 * math.pi * 640 * i / RATE) * math.exp(-i / (RATE * 0.12)) * 0.4
        out.append((lp * 0.9 + metal) * env(i, n, 0.001, 0.92))
    return out


def siren():
    # Two-tone cop yelp, loops cleanly on the bar.
    n = int(RATE * 1.8)
    out = []
    for i in range(n):
        t = i / RATE
        f = 880.0 if (t % 0.9) < 0.45 else 620.0
        tone = math.sin(2 * math.pi * f * t)
        odd = math.sin(2 * math.pi * f * 1.5 * t) * 0.22
        out.append((tone + odd) * 0.48)
    return out


def horn():
    n = int(RATE * 0.5)
    return [ (math.sin(2 * math.pi * 420 * i / RATE) * 0.5
              + math.sin(2 * math.pi * 330 * i / RATE) * 0.5)
             * env(i, n, 0.03, 0.3) for i in range(n) ]


def click():
    n = int(RATE * 0.06)
    return [ math.sin(2 * math.pi * 1500 * i / RATE) * env(i, n, 0.001, 0.9) * 0.5
             for i in range(n) ]


def pickup():
    n = int(RATE * 0.3)
    out = []
    for i in range(n):
        t = i / n
        f = 500 + 700 * t
        out.append(math.sin(2 * math.pi * f * i / RATE) * env(i, n, 0.01, 0.4) * 0.5)
    return out


def go():
    n = int(RATE * 0.5)
    return [ (math.sin(2 * math.pi * 880 * i / RATE)
              + 0.4 * math.sin(2 * math.pi * 1320 * i / RATE))
             * env(i, n, 0.005, 0.5) * 0.5 for i in range(n) ]


def engine():
    # Seamless loop: 250cc twin rumble + exhaust rasp.
    cycles = 48
    freq = 58.0
    n = int(RATE * cycles / freq)
    out = []
    lp = 0.0
    for i in range(n):
        t = i / RATE
        phase = 2 * math.pi * freq * t
        pulse = math.sin(phase) + 0.62 * math.sin(2 * phase + 0.55)
        pulse += 0.28 * math.sin(0.5 * phase) + 0.12 * math.sin(4 * phase + 1.1)
        lp += (noise() - lp) * 0.35
        rasp = lp * 0.18
        out.append((pulse * 0.32 + rasp) * 0.85)
    return out


def wind():
    n = int(RATE * 4.0)
    out = []
    lp = 0.0
    hp = 0.0
    for i in range(n):
        nse = noise()
        lp += (nse - lp) * 0.07
        hp += (nse - hp) * 0.45
        gust = 0.55 + 0.45 * math.sin(2 * math.pi * i / n)
        out.append((lp * 0.7 + (hp - lp) * 0.25) * 0.55 * gust)
    return out


def ambience_coast():
    n = int(RATE * 6.0)
    out = []
    lp = 0.0
    for i in range(n):
        lp += (noise() - lp) * 0.04
        swell = 0.45 + 0.55 * math.sin(2 * math.pi * i / n)
        hiss = noise() * 0.04 * swell
        out.append(lp * 0.55 * swell + hiss)
    return out


def ambience_desert():
    n = int(RATE * 5.0)
    out = []
    lp = 0.0
    for i in range(n):
        lp += (noise() - lp) * 0.03
        shimmer = math.sin(2 * math.pi * 0.35 * i / RATE) * 0.04
        cricket = 0.0
        if (i % int(RATE * 0.9)) < 40:
            cricket = math.sin(2 * math.pi * 4200 * i / RATE) * 0.03
        out.append(lp * 0.38 + shimmer + cricket)
    return out


def ambience_city():
    n = int(RATE * 5.0)
    out = []
    lp = 0.0
    rumble = 0.0
    for i in range(n):
        lp += (noise() - lp) * 0.05
        rumble += (noise() - rumble) * 0.012
        hornish = 0.0
        cycle = i % int(RATE * 2.4)
        if 200 < cycle < 520:
            f = 310 + (cycle % 17)
            hornish = math.sin(2 * math.pi * f * i / RATE) * 0.03 * env(cycle - 200, 320, 0.1, 0.4)
        out.append(rumble * 0.5 + lp * 0.18 + hornish)
    return out


def ambience_mountain():
    n = int(RATE * 5.5)
    out = []
    lp = 0.0
    for i in range(n):
        lp += (noise() - lp) * 0.025
        gust = 0.4 + 0.6 * math.sin(2 * math.pi * 2.0 * i / n)
        bird = 0.0
        if (i % int(RATE * 1.7)) < 90:
            bird = math.sin(2 * math.pi * (1800 + (i % 40) * 8) * i / RATE) * 0.025
        out.append(lp * 0.42 * gust + bird)
    return out


def music():
    """90s synthwave loop: driving bass, minor-key lead, seamless 8 bars @ 120bpm."""
    bpm = 120.0
    beat = 60.0 / bpm
    bars = 8
    n = int(RATE * beat * 4 * bars)
    # A minor pentatonic-ish progression.
    bass_line = [110.0, 110.0, 130.81, 98.0] * 2          # A A C G per bar
    lead_line = [220.0, 261.63, 329.63, 293.66, 220.0, 196.0, 261.63, 220.0]
    out = []
    for i in range(n):
        t = i / RATE
        bar = int(t / (beat * 4)) % bars
        beat_t = (t % beat) / beat
        # Bass: saw-ish, ducked on each beat like sidechain pump.
        bf = bass_line[bar % len(bass_line)]
        saw = 2.0 * ((t * bf) % 1.0) - 1.0
        pump = 0.4 + 0.6 * min(1.0, beat_t * 3.0)
        bass = saw * 0.22 * pump
        # Lead: square-ish melody, one note per half-bar.
        note = lead_line[int(t / (beat * 2)) % len(lead_line)]
        sq = 1.0 if (t * note) % 1.0 < 0.5 else -1.0
        vib = math.sin(2 * math.pi * 5.0 * t) * 0.004
        sq = 1.0 if (t * (note * (1 + vib))) % 1.0 < 0.5 else -1.0
        lead = sq * 0.075
        # Hats: noise ticks on offbeats.
        hat = noise() * 0.05 if (t % beat) > beat * 0.5 and (t % beat) < beat * 0.56 else 0.0
        # Kick on each beat.
        kt = t % beat
        kickd = math.sin(2 * math.pi * (90 * math.exp(-kt * 22) + 35) * kt) * math.exp(-kt * 14) * 0.5
        out.append(bass + lead + hat + kickd)
    return out


if __name__ == "__main__":
    write("hit", hit())
    write("kick", kick())
    write("crash", crash())
    write("siren", siren())
    write("horn", horn())
    write("click", click())
    write("pickup", pickup())
    write("go", go())
    write("engine", engine())
    write("music", music())
    write("wind", wind())
    write("ambience_coast", ambience_coast())
    write("ambience_desert", ambience_desert())
    write("ambience_city", ambience_city())
    write("ambience_mountain", ambience_mountain())
    print("ALL AUDIO DONE")
