"""
Camera capture wrapper.

Tries the platform-appropriate OpenCV backend first, then falls back. On
Linux (Rubik Pi, Raspberry Pi) we prefer V4L2. On Windows (development
laptop) we prefer DirectShow — it initializes faster and plays nicer with
a wider range of USB webcams like the Brio 105.
"""

from __future__ import annotations

import sys
from typing import List, Optional, Tuple

import cv2
import numpy as np


def open_camera(index: int, width: int, height: int) -> cv2.VideoCapture:
    """Open a VideoCapture robustly across platforms."""
    backends: List[Tuple[str, int]] = []
    if sys.platform.startswith("win"):
        backends.extend([("CAP_DSHOW", cv2.CAP_DSHOW), ("CAP_MSMF", cv2.CAP_MSMF)])
    elif sys.platform.startswith("linux"):
        backends.append(("CAP_V4L2", cv2.CAP_V4L2))
    backends.append(("CAP_ANY", cv2.CAP_ANY))

    tried: List[str] = []
    for name, backend in backends:
        cap = cv2.VideoCapture(index, backend)
        if cap.isOpened():
            cap.set(cv2.CAP_PROP_FRAME_WIDTH, width)
            cap.set(cv2.CAP_PROP_FRAME_HEIGHT, height)
            # Reduce buffering on Linux so we stay "live". Not all backends
            # honor this; ignored if unsupported.
            cap.set(cv2.CAP_PROP_BUFFERSIZE, 1)
            return cap
        tried.append(name)
        cap.release()

    raise RuntimeError(
        f"Could not open camera index {index}. Tried backends: {', '.join(tried)}."
    )


def read_frame(cap: cv2.VideoCapture) -> Optional[np.ndarray]:
    """Read one frame, returning None on transient failure."""
    ok, frame = cap.read()
    if not ok or frame is None:
        return None
    return frame
