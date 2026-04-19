"""
MediaPipe Pose Landmarker wrapper.

Uses the new Tasks API (the classic `mp.solutions.pose` path was removed in
MediaPipe 0.10.33+). The `.task` model file is downloaded on first use into
`pi/models/`. We force the **CPU delegate** here — the Rubik Pi NPU path is
explicitly a later-phase upgrade.
"""

from __future__ import annotations

import time
import urllib.request
from pathlib import Path
from typing import Optional

import cv2
import numpy as np

from .config import MODELS_DIR
from .pose_types import Landmark, PoseFrame


# ---------------------------------------------------------------------------
# Model registry
# ---------------------------------------------------------------------------

_MODEL_URLS = {
    "lite":  "https://storage.googleapis.com/mediapipe-models/pose_landmarker/pose_landmarker_lite/float16/1/pose_landmarker_lite.task",
    "full":  "https://storage.googleapis.com/mediapipe-models/pose_landmarker/pose_landmarker_full/float16/1/pose_landmarker_full.task",
    "heavy": "https://storage.googleapis.com/mediapipe-models/pose_landmarker/pose_landmarker_heavy/float16/1/pose_landmarker_heavy.task",
}


def _ensure_model(variant: str) -> Path:
    if variant not in _MODEL_URLS:
        raise ValueError(
            f"Unknown pose model variant {variant!r}; expected one of {list(_MODEL_URLS)}."
        )
    MODELS_DIR.mkdir(parents=True, exist_ok=True)
    dest = MODELS_DIR / f"pose_landmarker_{variant}.task"
    if dest.exists() and dest.stat().st_size > 0:
        return dest
    url = _MODEL_URLS[variant]
    print(f"[pose] Downloading model ({variant}) from {url} ...")
    tmp = dest.with_suffix(".task.tmp")
    urllib.request.urlretrieve(url, tmp)
    tmp.replace(dest)
    print(f"[pose] Model ready: {dest}")
    return dest


# ---------------------------------------------------------------------------
# Detector
# ---------------------------------------------------------------------------

class PoseDetector:
    """
    Synchronous wrapper over MediaPipe's Pose Landmarker.

    This class is intentionally called only from the capture thread — the
    underlying C++ graph is not safe to call concurrently from multiple
    threads without locking, and we don't need the complexity.
    """

    def __init__(self, model_variant: str = "lite") -> None:
        self._model_variant = model_variant
        self._landmarker = None
        self._last_ts_us: int = -1
        self._t0_ms: int = int(time.monotonic() * 1000.0)

    def _lazy_init(self) -> None:
        if self._landmarker is not None:
            return
        # Deferred import so missing MediaPipe still allows the rest of the
        # package to load (useful for protocol tests).
        import mediapipe as mp
        from mediapipe.tasks import python as mp_python
        from mediapipe.tasks.python import vision as mp_vision

        model_path = _ensure_model(self._model_variant)
        base_options = mp_python.BaseOptions(
            model_asset_path=str(model_path),
            # CPU is the default; set explicitly so the decision is visible.
            delegate=mp_python.BaseOptions.Delegate.CPU,
        )
        options = mp_vision.PoseLandmarkerOptions(
            base_options=base_options,
            running_mode=mp_vision.RunningMode.VIDEO,
            num_poses=1,
            output_segmentation_masks=False,
        )
        self._mp = mp
        self._landmarker = mp_vision.PoseLandmarker.create_from_options(options)

    def detect(self, bgr_frame: np.ndarray, seq: int) -> Optional[PoseFrame]:
        """
        Run the model on one BGR frame. Returns None if no pose was detected.
        Landmark coordinates are normalized to the image (0..1).
        """
        self._lazy_init()
        import mediapipe as mp  # safe: _lazy_init imported it above

        h, w = bgr_frame.shape[:2]
        # `cv2.cvtColor` returns a contiguous C-ordered uint8 array, which is
        # what `mp.Image` requires. The `[..., ::-1]` shortcut yields a
        # negatively-strided view that the MediaPipe binding rejects.
        rgb = cv2.cvtColor(bgr_frame, cv2.COLOR_BGR2RGB)
        mp_image = mp.Image(image_format=mp.ImageFormat.SRGB, data=rgb)

        # `detect_for_video` requires monotonically increasing microsecond
        # timestamps. We derive them from the monotonic clock.
        ts_ms = int(time.monotonic() * 1000.0) - self._t0_ms
        ts_us = int(ts_ms * 1000)
        if ts_us <= self._last_ts_us:
            ts_us = self._last_ts_us + 1
        self._last_ts_us = ts_us

        result = self._landmarker.detect_for_video(mp_image, ts_us)
        if not result.pose_landmarks:
            return None

        pose = result.pose_landmarks[0]
        landmarks = tuple(
            Landmark(
                x=float(p.x),
                y=float(p.y),
                z=float(p.z),
                visibility=float(getattr(p, "visibility", 1.0) or 1.0),
            )
            for p in pose
        )
        return PoseFrame(
            seq=int(seq),
            ts_ms=int(ts_ms),
            image_w=int(w),
            image_h=int(h),
            landmarks=landmarks,
        )

    def close(self) -> None:
        if self._landmarker is not None:
            try:
                self._landmarker.close()
            except Exception:
                pass
            self._landmarker = None
