"""Reproducible sampled cue library for the whole game (station, ship battle, interface).

Every source is a CC0 pack listed in tools/audio/sources.json. Raw downloads live in the ignored cache
(artifacts/audio-sources); only the processed clips below are committed. Each take is a small recipe:
layers of trimmed source clips with gain, pitch, filters and delay, then fades, peak normalisation and,
for loops, a seamless crossfade. Output: game/audio/sfx/*.ogg, game/audio/cues.json (runtime manifest with
per-take provenance) and an ignored listening montage under artifacts/audio-review/.

Usage (Python 3.11+ and ffmpeg with libvorbis on PATH):
    python tools/audio/build_game_audio.py --download   # fetch the listed packs into the cache (optional)
    python tools/audio/build_game_audio.py              # verify hashes, extract, build, write the manifest
"""
from __future__ import annotations

import argparse
import array
import hashlib
import json
import math
import shutil
import subprocess
import sys
import urllib.request
import zipfile
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
SOURCES = json.loads((ROOT / "tools/audio/sources.json").read_text(encoding="utf-8"))
CACHE = ROOT / SOURCES["cache"]
RAW, EXTRACTED = CACHE / "raw", CACHE / "extracted"
OUTPUT = ROOT / "game/audio/sfx"
MANIFEST = ROOT / "game/audio/cues.json"
REVIEW = ROOT / "artifacts/audio-review"
RATE = 44100

SCIFI = "kenney_sci-fi-sounds/Audio/"
IMPACT = "kenney_impact-sounds/Audio/"
UI = "kenney_interface-sounds/Audio/"
DIGITAL = "kenney_digital-audio/Audio/"
LOOPS = "rubberduck_sfx_loops/"
MISC = "rubberduck_100-CC0-SFX/"
FIRE = "antumdeluge_fire/fire-1.wav"


def L(src, gain=0.0, pitch=1.0, start=0.0, dur=None, delay=0.0, hp=None, lp=None):
    """One layer: a source clip (pack-relative), trimmed, pitched, filtered, delayed (seconds) and gained (dB)."""
    return {"src": src, "gain": gain, "pitch": pitch, "start": start, "dur": dur, "delay": delay, "hp": hp, "lp": lp}


def T(*layers, fade_in=0.002, fade_out=0.06, length=None):
    return {"layers": list(layers), "fade_in": fade_in, "fade_out": fade_out, "length": length}


def C(bus, volume_db, takes, jitter=0.04, voices=4, interval=30, loop=False, stereo=False, peak=-1.0, crossfade=0.0):
    return {"bus": bus, "volume_db": volume_db, "takes": takes, "pitch_jitter": jitter, "max_voices": voices,
            "min_interval_ms": interval, "loop": loop, "stereo": stereo, "peak": peak, "crossfade": crossfade}


def n(i):
    return f"{i:03d}"


# Cue recipes. Levels are relative mix levels; every take is peak-normalised first.
CUES = {
    # Ship battle: weapons and impacts.
    "ship.laser_fire": C("Sfx", -11, [T(L(SCIFI + f"laserRetro_{n(i)}.ogg", pitch=.92), L(SCIFI + f"laserLarge_{n(i)}.ogg", -14, 1.3, dur=.3),
                                        fade_out=.08) for i in range(5)], jitter=.05, voices=6, interval=25),
    "ship.missile_launch": C("Sfx", -8, [T(L(SCIFI + f"thrusterFire_{n(i)}.ogg", 0, 1.15, .1, .9, lp=6000),
                                           L(SCIFI + "lowFrequency_explosion_001.ogg", -9, 1.4, 0, .35), fade_in=.01, fade_out=.35) for i in range(3)]),
    "ship.shield_hit": C("Sfx", -13, [T(L(SCIFI + f"forceField_{n(i)}.ogg", 0, 1.1, 0, .55), L(IMPACT + f"impactGlass_light_{n(i)}.ogg", -12),
                                       fade_out=.2) for i in range(5)], voices=5),
    "ship.hull_hit": C("Sfx", -5, [T(L(SCIFI + f"explosionCrunch_{n(i)}.ogg", 0, 1.0, 0, .8), L(IMPACT + f"impactMetal_heavy_{n(i + 1)}.ogg", -3),
                                     L(SCIFI + "lowFrequency_explosion_001.ogg", -6, 1.1, 0, .5), fade_out=.3) for i in (0, 1, 2, 3)], voices=5),
    "ship.explosion_heavy": C("Sfx", -3, [T(L(SCIFI + f"explosionCrunch_{n(i)}.ogg"), L(SCIFI + "lowFrequency_explosion_000.ogg", -3, 1.0, 0, 1.2),
                                            L(IMPACT + "impactPlate_heavy_002.ogg", -6), fade_out=.5) for i in (3, 4)], voices=3),
    "ship.explosion_final": C("Sfx", -1, [T(L(SCIFI + "lowFrequency_explosion_000.ogg"), L(SCIFI + "explosionCrunch_004.ogg", -2),
                                            L(SCIFI + "explosionCrunch_001.ogg", -4, .85, delay=.25), L(SCIFI + "explosionCrunch_003.ogg", -6, .75, delay=.55),
                                            fade_out=.8)], voices=1),
    "ship.miss": C("Sfx", -15, [T(L(SCIFI + f"thrusterFire_{n(i)}.ogg", 0, 1.6, .3, .75, hp=700, lp=7000), fade_in=.12, fade_out=.25) for i in (1, 3)], voices=3),
    "ship.shield_down": C("Sfx", -6, [T(L(SCIFI + "forceField_002.ogg", 0, .72), L(IMPACT + "impactGlass_heavy_003.ogg", -4), L(DIGITAL + "phaserDown1.ogg", -10, .8),
                                        fade_out=.3)], voices=1),
    "ship.shield_up": C("Sfx", -17, [T(L(SCIFI + f"forceField_{n(i)}.ogg", 0, 1.35, 0, .45), fade_in=.12, fade_out=.2) for i in (1, 4)], voices=2, interval=120),
    # Hazards, crew and repairs.
    "ship.fire_ignite": C("Sfx", -8, [T(L(FIRE, 0, 1.0, s, .9), L(IMPACT + "impactSoft_heavy_001.ogg", -2, .7), fade_out=.35) for s in (0, 1.2)], voices=2),
    "ship.fire_loop": C("Ambience", -5, [T(L(FIRE, lp=7000), fade_in=0, fade_out=0)], loop=True, crossfade=.4, peak=-3),
    "ship.breach": C("Sfx", -6, [T(L(IMPACT + "impactMetal_heavy_000.ogg"), L(SCIFI + "lowFrequency_explosion_001.ogg", -5, .9, 0, .4),
                                   L(LOOPS + "noise_02.ogg", -6, 1.0, 0, 1.2, hp=1800), fade_out=.5)], voices=1),
    "ship.leak_loop": C("Ambience", -15, [T(L(LOOPS + "noise_02.ogg", 0, 1.0, hp=1400, lp=9000), fade_in=0, fade_out=0)], loop=True, crossfade=.5, peak=-3),
    "ship.alarm": C("Sfx", -14, [T(L(LOOPS + "alarm_01.ogg", 0, 1.0, 0, 1.0), fade_out=.15)], voices=1, interval=1500),
    "ship.oxygen_alarm": C("Ambience", -19, [T(L(LOOPS + "alarm_03.ogg"), fade_in=0, fade_out=0)], loop=True, crossfade=.05, peak=-3),
    "ship.crew_hurt": C("Sfx", -13, [T(L(IMPACT + f"impactPunch_medium_{n(i)}.ogg"), L(IMPACT + f"impactSoft_heavy_{n(i)}.ogg", -6)) for i in range(3)], voices=3),
    "ship.crew_down": C("Sfx", -9, [T(L(IMPACT + "impactPunch_heavy_002.ogg"), L(DIGITAL + "lowDown.ogg", -8, .8), fade_out=.3)], voices=1),
    "ship.repair_done": C("Sfx", -13, [T(L(MISC + "tools_01.ogg", -2), L(UI + "confirmation_002.ogg", -6)), T(L(MISC + "tools_03.ogg", -2),
                                                                                                      L(UI + "confirmation_002.ogg", -6, 1.1))], voices=2),
    "ship.hazard_cleared": C("Sfx", -15, [T(L(UI + "confirmation_001.ogg"), L(SCIFI + "forceField_000.ogg", -12, 1.5, 0, .3))], voices=1),
    "ship.vent": C("Sfx", -8, [T(L(SCIFI + "doorOpen_001.ogg"), L(LOOPS + "noise_01.ogg", -3, 1.0, 0, 1.4, hp=1200), fade_out=.6)], voices=1),
    "ship.door": C("Sfx", -15, [T(L(SCIFI + f"doorOpen_{n(i)}.ogg")) for i in range(3)] + [T(L(SCIFI + f"doorClose_{n(i)}.ogg")) for i in range(3)], voices=3),
    "ship.power_up": C("Ui", -16, [T(L(UI + f"switch_00{i}.ogg")) for i in (1, 2)], voices=2),
    "ship.power_down": C("Ui", -16, [T(L(UI + f"switch_00{i}.ogg", 0, .8)) for i in (5, 6)], voices=2),
    "ship.victory": C("Ui", -9, [T(L(UI + "confirmation_004.ogg"), L(DIGITAL + "powerUp5.ogg", -8, .9, delay=.12), fade_out=.3)], voices=1),
    "ship.defeat": C("Ui", -8, [T(L(DIGITAL + "lowDown.ogg"), L(SCIFI + "lowFrequency_explosion_000.ogg", -6, .8, 0, 1.2), fade_out=.6)], voices=1),
    "ship.ambience": C("Ambience", -17, [T(L(SCIFI + "spaceEngineLow_002.ogg", lp=2400), L(SCIFI + "computerNoise_001.ogg", -20, .9, lp=3000),
                                           fade_in=0, fade_out=0)], loop=True, crossfade=.8, peak=-3),
    # Interface, shared by both scenes.
    "ui.click": C("Ui", -15, [T(L(UI + "click_002.ogg"), fade_out=.005), T(L(UI + "click_003.ogg"), fade_out=.005)], jitter=.03, voices=3, interval=20),
    "ui.select": C("Ui", -15, [T(L(UI + f"select_00{i}.ogg")) for i in (1, 2, 3)], jitter=.02, voices=2, interval=30),
    "ui.deny": C("Ui", -13, [T(L(UI + "error_004.ogg")), T(L(UI + "error_006.ogg"))], voices=1, interval=120),
    "ui.aim": C("Ui", -15, [T(L(UI + "tick_002.ogg"), L(UI + "switch_003.ogg", -8, 1.3, 0, .15))], voices=1),
    "ui.target": C("Ui", -13, [T(L(UI + "confirmation_001.ogg", 0, 1.1))], voices=1, interval=60),
    "ui.order": C("Ui", -15, [T(L(UI + "select_004.ogg")), T(L(UI + "select_005.ogg", dur=.4))], voices=2),
    "ui.pause": C("Ui", -14, [T(L(UI + "minimize_004.ogg"))], voices=1, interval=100),
    "ui.resume": C("Ui", -14, [T(L(UI + "maximize_004.ogg"))], voices=1, interval=100),
    # Station combat (3D positional playback owned by the station host).
    "station.carbine": C("Sfx", -11, [T(L(SCIFI + f"laserSmall_{n(i)}.ogg", 0, .85), L(IMPACT + f"impactMetal_light_{n(i)}.ogg", -10, 1.3, 0, .12),
                                        L(SCIFI + "lowFrequency_explosion_001.ogg", -16, 1.6, 0, .18), fade_out=.08) for i in range(4)]),
    "station.shotgun": C("Sfx", -8, [T(L(SCIFI + f"laserLarge_{n(i)}.ogg", 0, .7, 0, .45), L(SCIFI + f"explosionCrunch_{n(i)}.ogg", -5, 1.25, 0, .35),
                                       L(IMPACT + f"impactPunch_heavy_{n(i)}.ogg", -6), fade_out=.15) for i in range(4)]),
    "station.sentry": C("Sfx", -11, [T(L(SCIFI + f"laserRetro_{n(i)}.ogg", 0, .8), L(IMPACT + f"impactMetal_medium_{n(i)}.ogg", -12, 1.4, 0, .1))
                                     for i in range(4)]),
    "station.burst": C("Sfx", -10, [T(L(SCIFI + f"laserLarge_{n(i)}.ogg", 0, 1.05, 0, .5), fade_out=.12) for i in range(4)]),
    "station.interrupt": C("Sfx", -11, [T(L(SCIFI + f"forceField_{n(i)}.ogg", 0, 1.7, 0, .3), L(DIGITAL + "zap1.ogg", -8, 1.4, 0, .25), fade_out=.1)
                                        for i in range(4)]),
    "station.carbine_hit": C("Sfx", -13, [T(L(IMPACT + f"impactMetal_light_{n(i)}.ogg"), L(IMPACT + f"impactGeneric_light_{n(i)}.ogg", -4)) for i in range(4)]),
    "station.shotgun_hit": C("Sfx", -11, [T(L(IMPACT + f"impactPlate_heavy_{n(i)}.ogg"), L(IMPACT + f"impactPunch_medium_{n(i)}.ogg", -4)) for i in range(4)]),
    "station.melee_hit": C("Sfx", -11, [T(L(IMPACT + f"impactPunch_heavy_{n(i)}.ogg")) for i in range(4)]),
    "station.sentry_hit": C("Sfx", -13, [T(L(IMPACT + f"impactMetal_medium_{n(i)}.ogg")) for i in range(4)]),
    "station.block": C("Sfx", -11, [T(L(IMPACT + f"impactBell_heavy_{n(i)}.ogg", 0, 1.6, 0, .35), L(SCIFI + f"forceField_{n(i)}.ogg", -8, 1.5, 0, .25),
                                      fade_out=.15) for i in range(4)]),
    "station.barrier": C("Sfx", -12, [T(L(SCIFI + f"forceField_{n(i)}.ogg", 0, .9), fade_in=.03, fade_out=.25) for i in range(4)]),
    "station.taunt": C("Sfx", -12, [T(L(IMPACT + f"impactBell_heavy_{n(i)}.ogg", 0, .62, 0, .8), L(DIGITAL + "lowThreeTone.ogg", -10, .8, 0, .6),
                                      fade_out=.3) for i in range(2)]),
    "station.heal": C("Sfx", -14, [T(L(DIGITAL + f"powerUp{i}.ogg", 0, .85, lp=5000), L(SCIFI + "forceField_003.ogg", -12, 1.6, 0, .3), fade_in=.02, fade_out=.2)
                                   for i in (2, 5)]),
    "station.departure": C("Sfx", -9, [T(L(SCIFI + "thrusterFire_000.ogg", 0, .8, lp=5000), L(SCIFI + "spaceEngineLarge_000.ogg", -4, .9, delay=.8),
                                         L(SCIFI + "lowFrequency_explosion_000.ogg", -8, .7, delay=.2), fade_in=.6, fade_out=1.2, length=4.8)], voices=1),
    "station.ambience": C("Ambience", -14, [T(L(LOOPS + "ambient_01.ogg", 0, 1.0, lp=6000), L(SCIFI + "spaceEngineLow_000.ogg", -9, .8, lp=900),
                                              fade_in=0, fade_out=0)], loop=True, crossfade=.8, peak=-4),
}


def sha256(path):
    digest = hashlib.sha256()
    with open(path, "rb") as handle:
        for chunk in iter(lambda: handle.read(1 << 20), b""):
            digest.update(chunk)
    return digest.hexdigest()


def prepare(download):
    RAW.mkdir(parents=True, exist_ok=True)
    for pack in SOURCES["packs"]:
        path = RAW / pack["file"]
        if not path.exists():
            if not download:
                sys.exit(f"Missing {path}. Run with --download (fetches {pack['url']}) or place the file there.")
            print("download", pack["url"])
            urllib.request.urlretrieve(pack["url"], path)
        if path.stat().st_size != pack["bytes"] or sha256(path) != pack["sha256"]:
            sys.exit(f"{path.name} does not match the recorded size/hash; refusing to build from it.")
        target = EXTRACTED / pack["id"]
        if path.suffix == ".zip":
            if not target.exists():
                with zipfile.ZipFile(path) as archive:
                    archive.extractall(target)
        else:
            target.mkdir(parents=True, exist_ok=True)
            shutil.copyfile(path, target / path.name.split("_", 1)[1])


def decode(layer, channels):
    source = EXTRACTED / layer["src"]
    filters = []
    if layer["pitch"] != 1.0:
        filters += [f"asetrate={round(RATE * layer['pitch'])}", f"aresample={RATE}"]
    if layer["hp"]:
        filters.append(f"highpass=f={layer['hp']}")
    if layer["lp"]:
        filters.append(f"lowpass=f={layer['lp']}")
    command = ["ffmpeg", "-v", "error", "-ss", str(layer["start"])]
    if layer["dur"]:
        command += ["-t", str(layer["dur"])]
    command += ["-i", str(source), "-ac", str(channels), "-ar", str(RATE), "-af", ",".join(filters) or "anull", "-f", "f32le", "-"]
    data = array.array("f")
    data.frombytes(subprocess.run(command, check=True, capture_output=True).stdout)
    return data


def render(take, channels, crossfade):
    layers = []
    for layer in take["layers"]:
        samples = decode(layer, channels)
        gain = 10 ** (layer["gain"] / 20)
        offset = round(layer["delay"] * RATE) * channels
        layers.append((offset, gain, samples))
    length = max(offset + len(samples) for offset, _, samples in layers)
    if take["length"]:
        length = min(length, round(take["length"] * RATE) * channels)
    mix = array.array("f", bytes(4 * length))
    for offset, gain, samples in layers:
        for index in range(min(len(samples), length - offset)):
            mix[offset + index] += samples[index] * gain
    frames = length // channels
    fade_in, fade_out = round(take["fade_in"] * RATE), round(take["fade_out"] * RATE)
    for frame in range(frames):
        weight = 1.0
        if fade_in and frame < fade_in:
            weight = frame / fade_in
        if fade_out and frame >= frames - fade_out:
            weight = min(weight, (frames - frame) / fade_out)
        if weight < 1.0:
            for channel in range(channels):
                mix[frame * channels + channel] *= weight
    if crossfade:
        # Seamless loop: the head is crossfaded with the tail, which is then dropped.
        x = round(crossfade * RATE)
        body = frames - x
        for frame in range(x):
            t = frame / x
            for channel in range(channels):
                head = frame * channels + channel
                mix[head] = mix[head] * math.sin(t * math.pi / 2) + mix[(body + frame) * channels + channel] * math.cos(t * math.pi / 2)
        mix = mix[:body * channels]
    return mix


def normalise(mix, peak_db):
    peak = max((abs(value) for value in mix), default=0.0)
    if peak <= 1e-6:
        raise ValueError("silent take")
    gain = 10 ** (peak_db / 20) / peak
    for index in range(len(mix)):
        mix[index] *= gain
    return 20 * math.log10(peak)


def encode(mix, channels, path):
    subprocess.run(["ffmpeg", "-v", "error", "-y", "-f", "f32le", "-ar", str(RATE), "-ac", str(channels), "-i", "-",
                    "-c:a", "libvorbis", "-q:a", "6", str(path)], input=mix.tobytes(), check=True)


def rms_db(mix):
    return 10 * math.log10(max(1e-12, sum(value * value for value in mix) / max(1, len(mix))))


def build():
    OUTPUT.mkdir(parents=True, exist_ok=True)
    for generated in OUTPUT.glob("*.ogg"):
        generated.unlink()
    REVIEW.mkdir(parents=True, exist_ok=True)
    manifest = {"schema_version": 1, "generator": "tools/audio/build_game_audio.py", "sources": "tools/audio/sources.json",
                "license": "All takes derive only from CC0 packs; see provenance per take.", "cues": {}}
    montage, timing = array.array("f"), []
    for name, cue in CUES.items():
        channels = 2 if cue["stereo"] else 1
        files, provenance = [], []
        for index, take in enumerate(cue["takes"], start=1):
            mix = render(take, channels, cue["crossfade"] if cue["loop"] else 0)
            source_peak = normalise(mix, cue["peak"])
            path = OUTPUT / f"{name.replace('.', '_')}_{index}.ogg"
            encode(mix, channels, path)
            files.append("res://" + path.relative_to(ROOT / "game").as_posix())
            provenance.append({"file": path.name, "seconds": round(len(mix) / channels / RATE, 3), "rms_db": round(rms_db(mix), 1),
                               "mixed_peak_db": round(source_peak, 1),
                               "layers": [{key: value for key, value in layer.items() if value not in (None, 0, 0.0, 1.0) or key == "src"}
                                          for layer in take["layers"]]})
            mono = mix if channels == 1 else array.array("f", (sum(mix[i:i + channels]) / channels for i in range(0, len(mix), channels)))
            timing.append({"cue": name, "take": index, "starts_seconds": round(len(montage) / RATE, 3), "in_game_volume_db": cue["volume_db"]})
            gain = 10 ** (cue["volume_db"] / 20)
            montage.extend(value * gain for value in (mono if not cue["loop"] else mono[:RATE * 3]))
            montage.extend(array.array("f", bytes(4 * round(.25 * RATE))))
        manifest["cues"][name] = {"bus": cue["bus"], "volume_db": cue["volume_db"], "pitch_jitter": cue["pitch_jitter"],
                                  "max_voices": cue["max_voices"], "min_interval_ms": cue["min_interval_ms"], "loop": cue["loop"],
                                  "files": files, "provenance": provenance}
        print(f"{name:24s} {len(files)} take(s)")
    MANIFEST.write_text(json.dumps(manifest, indent=1) + "\n", encoding="utf-8", newline="\n")
    # Ignored listening evidence at in-game relative levels (loops truncated to 3 s).
    subprocess.run(["ffmpeg", "-v", "error", "-y", "-f", "f32le", "-ar", str(RATE), "-ac", "1", "-i", "-", str(REVIEW / "audition.wav")],
                   input=montage.tobytes(), check=True)
    (REVIEW / "timing.json").write_text(json.dumps(timing, indent=1) + "\n", encoding="utf-8", newline="\n")
    print("AUDIO_LIBRARY_BUILT", json.dumps({"cues": len(CUES), "takes": sum(len(c["takes"]) for c in CUES.values())}))


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--download", action="store_true", help="fetch missing packs listed in tools/audio/sources.json")
    args = parser.parse_args()
    if shutil.which("ffmpeg") is None:
        sys.exit("ffmpeg (with libvorbis) is required on PATH.")
    prepare(args.download)
    build()


if __name__ == "__main__":
    main()
