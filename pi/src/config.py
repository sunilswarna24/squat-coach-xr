"""Runtime configuration for the Pi edge server."""

from __future__ import annotations

from dataclasses import dataclass
from pathlib import Path


PROTOCOL_VERSION: int = 1

# Path to the models directory, relative to the pi/ root.
MODELS_DIR: Path = Path(__file__).resolve().parent.parent / "models"


@dataclass
class PiConfig:
    # Camera
    camera_index: int = 0
    frame_width: int = 640
    frame_height: int = 480

    # Streaming
    target_fps: int = 30
    host: str = "0.0.0.0"
    port: int = 8765

    # MediaPipe
    model_variant: str = "lite"          # "lite" | "full" | "heavy"
    min_visibility: float = 0.0          # Pi does not filter by visibility;
                                         # the Quest decides what to do with
                                         # low-confidence points.

    # Dev / debug
    preview: bool = False                # show an OpenCV window locally
    log_every_n_frames: int = 90         # print throughput every ~3 s at 30 Hz

    @property
    def frame_period_s(self) -> float:
        return 1.0 / max(1, self.target_fps)
