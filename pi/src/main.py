"""
Pi edge server entrypoint.

Spins up:
- The asyncio event loop (in a background thread) hosting the WebSocket server.
- A dedicated capture thread running OpenCV + MediaPipe at the target FPS,
  publishing each result to the broadcaster.

The two threads meet only at `PoseBroadcaster.publish()`, which is thread-safe.
"""

from __future__ import annotations

import argparse
import asyncio
import logging
import signal
import socket
import sys
import threading
import time
from typing import Optional

import cv2

from .camera import open_camera, read_frame
from .config import PiConfig
from .pose_detector import PoseDetector
from .protocol import encode_nopose, encode_pose
from .ws_server import PoseBroadcaster, build_hello_text


log = logging.getLogger("pi.main")


# ---------------------------------------------------------------------------
# Capture thread
# ---------------------------------------------------------------------------

class CaptureWorker(threading.Thread):
    """
    Runs the camera + MediaPipe loop off the asyncio event loop.

    This thread does all the heavy work (cv2.read + MediaPipe inference) so
    the WebSocket server can stay responsive even when inference stalls.
    """

    def __init__(self, cfg: PiConfig, broadcaster: PoseBroadcaster) -> None:
        super().__init__(name="capture", daemon=True)
        self._cfg = cfg
        self._bc = broadcaster
        # Do NOT name this `_stop` — threading.Thread has a private `_stop`
        # method and shadowing it with an Event breaks join()/is_alive()
        # during shutdown.
        self._stop_event = threading.Event()
        self._fps_ema: float = 0.0

    def stop(self) -> None:
        self._stop_event.set()

    def run(self) -> None:
        cfg = self._cfg
        try:
            cap = open_camera(cfg.camera_index, cfg.frame_width, cfg.frame_height)
        except Exception as exc:
            log.error("camera open failed: %s", exc)
            return

        detector = PoseDetector(cfg.model_variant)
        log.info(
            "capture: camera=%d target_fps=%d model=%s",
            cfg.camera_index, cfg.target_fps, cfg.model_variant,
        )

        seq = 0
        period = cfg.frame_period_s
        next_tick = time.monotonic()
        t_last_log = time.monotonic()

        try:
            while not self._stop_event.is_set():
                # Frame pacing — aim for target_fps, but never block longer
                # than one period so we stay responsive to shutdown.
                now = time.monotonic()
                if now < next_tick:
                    time.sleep(min(period, next_tick - now))
                    continue
                next_tick = now + period

                frame = read_frame(cap)
                if frame is None:
                    time.sleep(0.01)
                    continue

                t0 = time.monotonic()
                try:
                    pose = detector.detect(frame, seq=seq)
                except Exception as exc:
                    log.warning("detector error (skipping frame): %s", exc)
                    pose = None
                t1 = time.monotonic()

                # FPS (exponential moving average on inference + capture cost).
                inst = 1.0 / max(1e-6, t1 - t0)
                self._fps_ema = 0.9 * self._fps_ema + 0.1 * inst if self._fps_ema else inst

                if pose is None:
                    text = encode_nopose(seq=seq)
                else:
                    text = encode_pose(pose)
                self._bc.publish(text)
                seq += 1

                if cfg.preview:
                    cv2.imshow("pi-preview", frame)
                    if (cv2.waitKey(1) & 0xFF) == ord("q"):
                        self._stop_event.set()
                        break

                if seq % cfg.log_every_n_frames == 0:
                    now = time.monotonic()
                    elapsed = now - t_last_log
                    t_last_log = now
                    log.info(
                        "seq=%d  detect_fps_ema=%.1f  wall_fps=%.1f",
                        seq, self._fps_ema, cfg.log_every_n_frames / max(1e-6, elapsed),
                    )
        finally:
            if cfg.preview:
                try:
                    cv2.destroyAllWindows()
                except Exception:
                    pass
            try:
                cap.release()
            except Exception:
                pass
            detector.close()
            log.info("capture thread exited")


# ---------------------------------------------------------------------------
# Entrypoint
# ---------------------------------------------------------------------------

def _print_reachable_addresses(port: int) -> None:
    """Print the IPv4 addresses this host is reachable at, to help the user."""
    try:
        hostname = socket.gethostname()
        addrs = socket.gethostbyname_ex(hostname)[2]
    except Exception:
        addrs = []
    if not addrs:
        log.info("server reachable on port %d", port)
        return
    for a in addrs:
        log.info("server reachable at ws://%s:%d", a, port)


def parse_args(argv: Optional[list] = None) -> argparse.Namespace:
    p = argparse.ArgumentParser(description="Squat Coach XR — Pi edge server.")
    p.add_argument("--camera", type=int, default=0)
    p.add_argument("--host", type=str, default="0.0.0.0")
    p.add_argument("--port", type=int, default=8765)
    p.add_argument("--fps", type=int, default=30)
    p.add_argument("--width", type=int, default=640)
    p.add_argument("--height", type=int, default=480)
    p.add_argument(
        "--model", choices=["lite", "full", "heavy"], default="lite",
        help="MediaPipe model variant (CPU delegate).",
    )
    p.add_argument(
        "--preview", action="store_true",
        help="Show an OpenCV preview window locally (dev only).",
    )
    p.add_argument(
        "--log-level", default="INFO",
        choices=["DEBUG", "INFO", "WARNING", "ERROR"],
    )
    return p.parse_args(argv)


def run(args: argparse.Namespace) -> int:
    logging.basicConfig(
        level=args.log_level,
        format="%(asctime)s %(levelname)s %(name)s: %(message)s",
        datefmt="%H:%M:%S",
    )

    cfg = PiConfig(
        camera_index=args.camera,
        frame_width=args.width,
        frame_height=args.height,
        target_fps=args.fps,
        host=args.host,
        port=args.port,
        model_variant=args.model,
        preview=args.preview,
    )

    # Start the asyncio loop in a background thread.
    loop = asyncio.new_event_loop()
    loop_thread = threading.Thread(
        target=loop.run_forever, name="asyncio-loop", daemon=True
    )
    loop_thread.start()

    hello = build_hello_text(
        model=f"mediapipe_pose_landmarker_{cfg.model_variant}",
        delegate="cpu",
        image_w=cfg.frame_width,
        image_h=cfg.frame_height,
        target_fps=cfg.target_fps,
    )
    broadcaster = PoseBroadcaster(loop, hello)

    server_future = asyncio.run_coroutine_threadsafe(
        broadcaster.serve(cfg.host, cfg.port), loop
    )
    # Give the server a moment to actually bind before announcing it.
    time.sleep(0.2)
    _print_reachable_addresses(cfg.port)

    capture = CaptureWorker(cfg, broadcaster)
    capture.start()

    # Handle Ctrl+C cleanly.
    shutdown = threading.Event()

    def _handle_sig(signum, frame):  # noqa: ARG001
        log.info("signal received, shutting down")
        shutdown.set()

    signal.signal(signal.SIGINT, _handle_sig)
    if hasattr(signal, "SIGTERM"):
        signal.signal(signal.SIGTERM, _handle_sig)

    try:
        while not shutdown.is_set() and capture.is_alive():
            shutdown.wait(timeout=0.5)
    finally:
        log.info("stopping capture...")
        capture.stop()
        capture.join(timeout=3.0)

        log.info("closing connections...")
        broadcaster.close()

        # Stop the asyncio loop.
        server_future.cancel()
        loop.call_soon_threadsafe(loop.stop)
        loop_thread.join(timeout=2.0)

    log.info("bye")
    return 0


if __name__ == "__main__":
    try:
        sys.exit(run(parse_args()))
    except KeyboardInterrupt:
        sys.exit(130)
