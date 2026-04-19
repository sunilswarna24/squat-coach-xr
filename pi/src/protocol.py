"""
Wire protocol encode / decode.

The single source of truth for message shapes is
`../../protocol/schema.json`. Everything here must stay in sync with that
schema and the samples next to it. The unit tests in
`../tests/test_protocol.py` enforce this.
"""

from __future__ import annotations

import json
import time
from pathlib import Path
from typing import Any, Dict, Optional

from .config import PROTOCOL_VERSION
from .pose_types import PoseFrame


# ---------------------------------------------------------------------------
# Schema loading (optional, only needed when validation is requested)
# ---------------------------------------------------------------------------

_SCHEMA_PATH = (
    Path(__file__).resolve().parent.parent.parent / "protocol" / "schema.json"
)
_SCHEMA: Optional[Dict[str, Any]] = None


def load_schema() -> Dict[str, Any]:
    global _SCHEMA
    if _SCHEMA is None:
        with _SCHEMA_PATH.open("r", encoding="utf-8") as f:
            _SCHEMA = json.load(f)
    return _SCHEMA


# ---------------------------------------------------------------------------
# Encoders
# ---------------------------------------------------------------------------

def encode_hello(
    model: str,
    delegate: str,
    image_w: int,
    image_h: int,
    target_fps: int,
    server_ts_ms: Optional[int] = None,
) -> str:
    msg = {
        "v": PROTOCOL_VERSION,
        "type": "hello",
        "model": model,
        "delegate": delegate,
        "image_w": int(image_w),
        "image_h": int(image_h),
        "target_fps": int(target_fps),
        "server_ts_ms": int(server_ts_ms if server_ts_ms is not None else _now_ms()),
    }
    return json.dumps(msg, separators=(",", ":"))


def encode_pose(frame: PoseFrame) -> str:
    """Serialize a PoseFrame to a wire JSON string."""
    msg = {
        "v": PROTOCOL_VERSION,
        "type": "pose",
        "seq": int(frame.seq),
        "ts_ms": int(frame.ts_ms),
        "w": int(frame.image_w),
        "h": int(frame.image_h),
        "landmarks": [
            {
                "x": _clean_float(lm.x),
                "y": _clean_float(lm.y),
                "z": _clean_float(lm.z),
                "v": _clean_float(lm.visibility),
            }
            for lm in frame.landmarks
        ],
    }
    return json.dumps(msg, separators=(",", ":"))


def encode_nopose(seq: int, ts_ms: Optional[int] = None) -> str:
    msg = {
        "v": PROTOCOL_VERSION,
        "type": "nopose",
        "seq": int(seq),
        "ts_ms": int(ts_ms if ts_ms is not None else _now_ms()),
    }
    return json.dumps(msg, separators=(",", ":"))


def encode_bye(reason: str = "") -> str:
    msg: Dict[str, Any] = {"v": PROTOCOL_VERSION, "type": "bye"}
    if reason:
        msg["reason"] = str(reason)
    return json.dumps(msg, separators=(",", ":"))


# ---------------------------------------------------------------------------
# Decoders (only needed for inbound control messages)
# ---------------------------------------------------------------------------

def decode(text: str) -> Dict[str, Any]:
    """Parse any message. Does not validate; callers should branch on `type`."""
    obj = json.loads(text)
    if not isinstance(obj, dict):
        raise ValueError("Protocol messages must be JSON objects.")
    if obj.get("v") != PROTOCOL_VERSION:
        raise ValueError(f"Unsupported protocol version: {obj.get('v')!r}")
    if "type" not in obj:
        raise ValueError("Message missing required 'type'.")
    return obj


# ---------------------------------------------------------------------------
# Helpers
# ---------------------------------------------------------------------------

def _now_ms() -> int:
    """Monotonic time in milliseconds. Not wall-clock."""
    return int(time.monotonic() * 1000.0)


def _clean_float(x: float) -> float:
    """
    Guard against NaN/Inf leaking into JSON. json.dumps would emit `NaN`
    by default, which many JSON parsers on the Quest side will reject.
    """
    if x != x or x in (float("inf"), float("-inf")):
        return 0.0
    return float(round(x, 6))
