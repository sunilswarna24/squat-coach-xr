# Pi (edge) — capture + MediaPipe + WebSocket server

Runs on the **Rubik Pi 3** (or any Linux/Windows machine with a webcam during
development). Captures frames from the Logitech Brio 105 over USB, runs
MediaPipe Pose on the CPU, and streams 33 landmarks per frame over a
WebSocket to any connected client (the Meta Quest 3 Unity app).

## Install

### Linux (Rubik Pi, Raspberry Pi, etc.)

```bash
cd pi
python3 -m venv .venv
source .venv/bin/activate
pip install -r requirements.txt
```

You may need a few system packages on the Pi first:

```bash
sudo apt update
sudo apt install -y python3-venv python3-pip libgl1 libglib2.0-0
```

### Windows (development)

```powershell
cd pi
python -m venv .venv
.\.venv\Scripts\Activate.ps1
pip install -r requirements.txt
```

## Run

```bash
python -m src.main
# or with options:
python -m src.main --camera 0 --port 8765 --fps 30 --preview
```

Flags:

| Flag          | Default | Meaning                                             |
| ------------- | ------- | --------------------------------------------------- |
| `--camera N`  | 0       | OpenCV camera index                                 |
| `--port N`    | 8765    | WebSocket port                                      |
| `--host H`    | 0.0.0.0 | Bind address                                        |
| `--fps N`     | 30      | Target streaming rate                               |
| `--width W`   | 640     | Requested capture width                             |
| `--height H`  | 480     | Requested capture height                            |
| `--preview`   | off     | Show an OpenCV preview window (dev only)            |
| `--model V`   | lite    | MediaPipe model: `lite`, `full`, `heavy`            |

On first run the server downloads the `.task` model into `pi/models/` (a few
MB). That directory is gitignored.

## How it's put together

```
src/
├── main.py           # argparse + lifecycle
├── config.py         # dataclasses for runtime config
├── camera.py         # OpenCV VideoCapture wrapper
├── pose_detector.py  # MediaPipe Tasks wrapper (CPU delegate)
├── pose_types.py     # Landmark / PoseFrame dataclasses
├── protocol.py       # Build + validate wire messages
└── ws_server.py      # asyncio WebSocket server that fans out pose frames
```

- The **camera loop** runs in a dedicated thread. It blocks on `cv2.read()`
  and drops the oldest unsent frame if the asyncio broadcaster falls behind —
  we'd rather show fresh data than play catch-up.
- The **asyncio event loop** owns the WebSocket server. A `janus`-style
  thread-safe queue bridges the camera thread and asyncio coroutines.
- **MediaPipe runs in the camera thread** (inference is CPU-bound; keeping
  it off asyncio stops the socket loop from stalling).

## Sanity check on a laptop before you own the Pi

Run the server on Windows, then use any WebSocket debugger to confirm the
stream:

```powershell
python -m src.main --port 8765
```

Then in a browser console:

```js
const ws = new WebSocket("ws://localhost:8765");
ws.onmessage = (e) => console.log(JSON.parse(e.data).type);
```

You should see one `hello`, then a stream of `pose`/`nopose`.

## Tests

```bash
python -m pytest tests/ -q
```

The tests cover the wire protocol: every sample under `../protocol/samples/`
round-trips through `protocol.py` and validates against `schema.json`.
