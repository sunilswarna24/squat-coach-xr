#!/usr/bin/env python3
"""
Pre-render every coach line to a mono 16-bit WAV using the ElevenLabs TTS API,
then write them into the Unity ``Resources/VoiceCues/<key>/NN.wav`` layout that
VoiceCoach.cs loads at runtime.

Why this exists
---------------
The Meta Quest 3 ships without any Android TTS engine, so the app bundles
pre-synthesized clips. This script is the ElevenLabs replacement for
``generate_voice_cues.sh`` (which used macOS ``say``). The Quest runtime is
completely unchanged — ``VoiceCoach`` still loads WAVs from
``Assets/SquatCoach/Resources/VoiceCues/<cue_key>/NN.wav``.

Usage
-----
    export ELEVENLABS_API_KEY=sk_...
    # Optional: pick a specific voice (see defaults below).
    export ELEVENLABS_VOICE_ID=pNInz6obpgDQGcFmaJgB
    python3 quest/tools/generate_voice_cues_elevenlabs.py

    # Skip clips that already exist on disk (saves API quota when iterating):
    SKIP_EXISTING=1 python3 quest/tools/generate_voice_cues_elevenlabs.py

    # Only regenerate one cue key:
    python3 quest/tools/generate_voice_cues_elevenlabs.py heel_lift welcome

No third-party Python dependencies are used — just ``urllib`` and ``wave``
from the standard library. We request raw 16-bit PCM at 22.05 kHz from
ElevenLabs and wrap it with a WAV header ourselves, which keeps the output
format byte-identical to what the old ``afconvert -f WAVE -d LEI16@22050``
step produced.
"""

from __future__ import annotations

import json
import os
import sys
import time
import wave
from pathlib import Path
from typing import Iterable
from urllib import error, request


# ---------------------------------------------------------------------------
# Configuration
# ---------------------------------------------------------------------------

# Adam: deep, neutral male narration voice. Reads coaching cues with authority
# without feeling harsh. Swap via ELEVENLABS_VOICE_ID if you want a different
# character (Rachel = 21m00Tcm4TlvDq8ikWAM, Arnold = VR6AewLTigWG4xSOukaG, ...).
DEFAULT_VOICE_ID = "pNInz6obpgDQGcFmaJgB"

# "eleven_turbo_v2_5" is the low-latency multilingual model and gives the best
# price/quality ratio for short English clips. "eleven_multilingual_v2" is the
# higher-fidelity alternative at roughly double the quota cost.
DEFAULT_MODEL_ID = "eleven_turbo_v2_5"

# ElevenLabs PCM output sample rate. We mirror what the shell script wrote
# (22050 Hz mono 16-bit) so no other code has to change.
SAMPLE_RATE = 22050
OUTPUT_FORMAT = f"pcm_{SAMPLE_RATE}"

# Default voice style. Slight stability bias keeps the delivery consistent
# across 30+ clips so it doesn't feel like 30 different people talking.
VOICE_SETTINGS = {
    "stability": 0.55,
    "similarity_boost": 0.80,
    "style": 0.25,
    "use_speaker_boost": True,
}

# Keep this in sync with the `gen` blocks at the bottom of
# generate_voice_cues.sh. Each entry is (cue_key, [line, line, ...]).
CUES: list[tuple[str, list[str]]] = [
    # -- issue cues --------------------------------------------------------
    ("depth_shallow", [
        "Go deeper, aim for parallel.",
        "Sink a bit lower.",
        "Hit your depth target on the next rep.",
    ]),
    ("lean_forward", [
        "Chest up, don't fold forward.",
        "Keep your torso taller.",
        "Stop leaning. Brace your core.",
    ]),
    ("knees_forward", [
        "Push your hips back.",
        "Knees are drifting past your toes.",
        "Sit back into the squat.",
    ]),
    ("heel_lift", [
        "Keep your heels planted.",
        "Drive through your heels.",
        "Heels down.",
    ]),
    ("rushed", [
        "Control the tempo.",
        "Slow the descent down.",
    ]),
    ("partial_rep", [
        "Finish the rep all the way up.",
        "Stand up fully before descending again.",
    ]),
    ("good_set", [
        "Great set.",
        "Clean form, nice work.",
    ]),
    ("not_in_position", [
        "Step into the frame.",
        "I can't see your whole side.",
    ]),
    # -- instruction sequences --------------------------------------------
    ("welcome", [
        "Welcome to your form coach.",
        "Place the camera to your side, at about hip height.",
        "Stand roughly six feet from the camera so your whole body fits in the frame.",
        "I'll count your reps and speak corrections in real time.",
    ]),
    ("how_to", [
        "Stand with your feet shoulder-width apart, toes slightly turned out.",
        "Brace your core, and keep your chest up.",
        "Push your hips back first, then bend your knees.",
        "Lower until your hips are at least level with your knees.",
        "Drive through your heels to stand, and squeeze your glutes at the top.",
    ]),
]


# ---------------------------------------------------------------------------
# ElevenLabs client
# ---------------------------------------------------------------------------

API_ROOT = "https://api.elevenlabs.io/v1"


def synthesize_pcm(text: str, voice_id: str, model_id: str, api_key: str) -> bytes:
    """Call ElevenLabs and return raw PCM bytes (int16 little-endian mono)."""

    url = f"{API_ROOT}/text-to-speech/{voice_id}?output_format={OUTPUT_FORMAT}"
    payload = json.dumps({
        "text": text,
        "model_id": model_id,
        "voice_settings": VOICE_SETTINGS,
    }).encode("utf-8")

    req = request.Request(url, data=payload, method="POST")
    req.add_header("xi-api-key", api_key)
    req.add_header("Content-Type", "application/json")
    req.add_header("Accept", "audio/pcm")

    try:
        with request.urlopen(req, timeout=60) as resp:
            return resp.read()
    except error.HTTPError as e:
        body = e.read().decode("utf-8", errors="replace")
        raise RuntimeError(
            f"ElevenLabs returned HTTP {e.code} for text {text!r}: {body}"
        ) from e


def write_wav(path: Path, pcm: bytes) -> None:
    """Wrap raw PCM bytes in a WAV container (16-bit mono @ SAMPLE_RATE)."""

    path.parent.mkdir(parents=True, exist_ok=True)
    with wave.open(str(path), "wb") as wf:
        wf.setnchannels(1)
        wf.setsampwidth(2)
        wf.setframerate(SAMPLE_RATE)
        wf.writeframes(pcm)


# ---------------------------------------------------------------------------
# Driver
# ---------------------------------------------------------------------------

def iter_selected(cues: list[tuple[str, list[str]]],
                  filters: Iterable[str]) -> list[tuple[str, list[str]]]:
    wanted = list(filters)
    if not wanted:
        return cues
    by_key = {k: lines for k, lines in cues}
    missing = [k for k in wanted if k not in by_key]
    if missing:
        raise SystemExit(f"Unknown cue key(s): {', '.join(missing)}")
    return [(k, by_key[k]) for k in wanted]


def main(argv: list[str]) -> int:
    api_key = os.environ.get("ELEVENLABS_API_KEY", "").strip()
    if not api_key:
        print("ERROR: set ELEVENLABS_API_KEY in your environment before running.",
              file=sys.stderr)
        return 2

    voice_id = os.environ.get("ELEVENLABS_VOICE_ID", DEFAULT_VOICE_ID).strip()
    model_id = os.environ.get("ELEVENLABS_MODEL_ID", DEFAULT_MODEL_ID).strip()
    skip_existing = os.environ.get("SKIP_EXISTING", "").strip() in {"1", "true", "yes"}

    script_dir = Path(__file__).resolve().parent
    out_root = script_dir.parent / "Assets" / "SquatCoach" / "Resources" / "VoiceCues"
    out_root.mkdir(parents=True, exist_ok=True)

    selected = iter_selected(CUES, argv[1:])

    print(f"Voice:   {voice_id}")
    print(f"Model:   {model_id}")
    print(f"Format:  {OUTPUT_FORMAT}  (wrapped as 16-bit mono WAV)")
    print(f"Output:  {out_root}")
    print(f"Skip existing: {skip_existing}")
    print()

    total_lines = sum(len(lines) for _, lines in selected)
    done = 0
    t0 = time.time()

    for key, lines in selected:
        cue_dir = out_root / key
        if not skip_existing and cue_dir.exists():
            # Match the shell script's clean-slate behavior so stale clips from
            # a prior run (with a different voice or rewritten text) don't stick
            # around.
            for stale in cue_dir.glob("*.wav"):
                stale.unlink()
            for stale_meta in cue_dir.glob("*.wav.meta"):
                stale_meta.unlink()

        for i, line in enumerate(lines):
            done += 1
            wav_path = cue_dir / f"{i:02d}.wav"
            if skip_existing and wav_path.exists() and wav_path.stat().st_size > 44:
                print(f"  [{key:<16} {i:>2}] (kept)  {line}")
                continue

            print(f"  [{key:<16} {i:>2}] {done:>2}/{total_lines}  {line}")
            pcm = synthesize_pcm(line, voice_id, model_id, api_key)
            write_wav(wav_path, pcm)

    elapsed = time.time() - t0
    print()
    print(f"Done in {elapsed:0.1f}s. Wrote clips to {out_root}")
    print("Unity will pick up the new WAVs on next domain reload / build.")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))
