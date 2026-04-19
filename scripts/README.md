# scripts/

Small operator tools — not part of the library, but handy when bringing
the Pi up or debugging the Quest connection.

| Script             | What it does                                                          | Runs on |
| ------------------ | --------------------------------------------------------------------- | ------- |
| `launch_pi.sh`     | Start the Pi edge server detached, with a log at `pi/server.log`. Kills any previous `src.main` first. | Pi      |
| `probe_client.py`  | Connect to the Pi's WebSocket, print the `hello` handshake, and read N frames. Verifies camera + MediaPipe + wire protocol end-to-end without a Quest. | PC      |

## Common flows

Bring the Pi up and verify from your laptop:

```bash
# On the Pi
./scripts/launch_pi.sh

# On your laptop
pip install websockets                 # once
python scripts/probe_client.py ws://<pi-ip>:8765 90
```

Expected output when a person is in frame:

```
  hello: model=mediapipe_pose_landmarker_lite delegate=cpu res=640x480 fps=30
  pose seq=830 ts_ms=72471 landmarks=33  first=(0.25,0.10,v=0.98) last=(0.24,0.83,v=0.31)
received: 78 pose, 11 nopose in 90 frames (seq jumps: 5)
```

## Environment overrides

`launch_pi.sh` respects these env vars:

| Var      | Default     | Meaning                              |
| -------- | ----------- | ------------------------------------ |
| `HOST`   | `0.0.0.0`   | WebSocket bind address               |
| `PORT`   | `8765`      | WebSocket port                       |
| `CAMERA` | `0`         | `/dev/video{N}` index                |
| `FPS`    | `30`        | Target capture FPS                   |
| `VENV`   | `$REPO/.venv` | Path to the Python virtual environment |
