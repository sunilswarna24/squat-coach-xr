# Squat Coach XR

Real-time squat form coaching in mixed reality.

A **Rubik Pi 3** with a **Logitech Brio 105** USB webcam runs MediaPipe Pose,
streams 33 landmarks per frame over WiFi to a **Meta Quest 3** app built in
**Unity**, which analyzes form, counts reps, and speaks corrections through
the Quest's speakers.

```
 ┌──────────────┐     WebSocket (JSON, 30 Hz)     ┌───────────────┐
 │  Rubik Pi 3  │ ───────────────────────────────▶│  Meta Quest 3 │
 │              │                                 │               │
 │  Brio 105  ──┤  capture → MediaPipe (CPU) →    │  parse → port │
 │  USB camera  │  landmarks on the wire          │  of analyzer  │
 │              │                                 │  → HUD + TTS  │
 └──────────────┘                                 └───────────────┘
```

This is a **monorepo** because the Pi and the Quest share one wire protocol,
and evolving them together is much easier than across two repos.

## Layout

```
squat-coach-xr/
├── docs/            Architecture, protocol spec, setup guide
├── protocol/        Single source of truth for the wire format (+ samples)
├── pi/              Python capture + MediaPipe + WebSocket server
└── quest/           Unity C# client (WebSocket + analyzer + HUD + TTS)
```

- Pi and Quest are kept deliberately **dumb in isolation**:
  - Pi only captures, detects, and streams — no analysis.
  - Quest does all analysis, state, UI, and voice.
- The wire protocol under `protocol/` is the contract between them.

## Quick start (development)

Until you have the Rubik Pi in hand, you can run the Pi side on **any machine
with a webcam** (Windows laptop works) and point the Quest at its IP.

1. Bring up the Pi side — see [`pi/README.md`](pi/README.md).
2. Bring up the Quest side — see [`quest/README.md`](quest/README.md).
3. Read the wire protocol in [`docs/wire-protocol.md`](docs/wire-protocol.md)
   if you want to write your own client for testing.

## Relationship to the original Python prototype

The all-in-one Windows prototype lives in a sibling folder
(`../squat-posture-coach/`). That repo stays as a **Windows PC single-process**
version and a reference implementation for the form-analysis logic that gets
ported to C# on the Quest side here.

## Design goals

- **Low latency** — motion to feedback should feel immediate. Budget ≤ 100 ms
  from camera frame to landmarks arriving on the Quest.
- **Robust to drops** — WebSockets over a noisy WiFi will drop packets. The
  analyzer tolerates missed frames; there's no per-frame ACK loop.
- **One source of truth for the protocol** — both sides parse the same schema.
- **Dev-friendly** — you can run the Pi side on a laptop with a webcam, and
  point a browser at the WebSocket to debug wire traffic.

## License

TBD (private project).
