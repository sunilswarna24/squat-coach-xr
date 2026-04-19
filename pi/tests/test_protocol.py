"""
Protocol contract tests.

These tests deliberately do NOT import MediaPipe or OpenCV; they only touch
the pure-Python `protocol.py` and `pose_types.py` modules. That way CI (or a
developer on a fresh machine) can verify the wire format without needing to
install the heavy native dependencies.

The tests check two things:
1. Every sample JSON in ../../protocol/samples/ validates against the schema.
2. `encode_*` functions in protocol.py produce schema-valid output.
"""

from __future__ import annotations

import json
from pathlib import Path

import jsonschema
import pytest

from src.pose_types import Landmark, PoseFrame, NUM_LANDMARKS
from src import protocol


REPO_ROOT = Path(__file__).resolve().parents[2]
SCHEMA_PATH = REPO_ROOT / "protocol" / "schema.json"
SAMPLES_DIR = REPO_ROOT / "protocol" / "samples"


@pytest.fixture(scope="module")
def schema() -> dict:
    with SCHEMA_PATH.open("r", encoding="utf-8") as f:
        return json.load(f)


def _validate(msg: dict, schema: dict) -> None:
    jsonschema.validate(instance=msg, schema=schema)


# ---------------------------------------------------------------------------
# Sample JSON files must all validate
# ---------------------------------------------------------------------------

@pytest.mark.parametrize("path", sorted(SAMPLES_DIR.glob("*.json")))
def test_samples_validate(path: Path, schema: dict) -> None:
    with path.open("r", encoding="utf-8") as f:
        msg = json.load(f)
    _validate(msg, schema)


# ---------------------------------------------------------------------------
# encode_* outputs must validate
# ---------------------------------------------------------------------------

def test_encode_hello_validates(schema: dict) -> None:
    text = protocol.encode_hello(
        model="mediapipe_pose_landmarker_lite",
        delegate="cpu",
        image_w=640,
        image_h=480,
        target_fps=30,
        server_ts_ms=123,
    )
    msg = json.loads(text)
    _validate(msg, schema)


def test_encode_nopose_validates(schema: dict) -> None:
    text = protocol.encode_nopose(seq=7, ts_ms=123)
    msg = json.loads(text)
    _validate(msg, schema)


def test_encode_pose_validates(schema: dict) -> None:
    landmarks = tuple(
        Landmark(
            x=0.5 + 0.001 * i,
            y=0.5 - 0.001 * i,
            z=0.0,
            visibility=0.9,
        )
        for i in range(NUM_LANDMARKS)
    )
    frame = PoseFrame(
        seq=42, ts_ms=1700, image_w=640, image_h=480, landmarks=landmarks,
    )
    text = protocol.encode_pose(frame)
    msg = json.loads(text)
    _validate(msg, schema)


def test_encode_pose_filters_nan(schema: dict) -> None:
    landmarks = tuple(
        Landmark(x=float("nan"), y=float("inf"), z=-float("inf"), visibility=0.5)
        for _ in range(NUM_LANDMARKS)
    )
    frame = PoseFrame(
        seq=0, ts_ms=0, image_w=640, image_h=480, landmarks=landmarks,
    )
    text = protocol.encode_pose(frame)
    # Must parse as vanilla JSON (no NaN/Infinity tokens).
    msg = json.loads(text)
    _validate(msg, schema)


def test_decode_round_trip() -> None:
    text = protocol.encode_nopose(seq=3, ts_ms=1234)
    obj = protocol.decode(text)
    assert obj["type"] == "nopose"
    assert obj["seq"] == 3
    assert obj["ts_ms"] == 1234


def test_decode_rejects_bad_version() -> None:
    bad = json.dumps({"v": 999, "type": "nopose", "seq": 1, "ts_ms": 1})
    with pytest.raises(ValueError):
        protocol.decode(bad)
