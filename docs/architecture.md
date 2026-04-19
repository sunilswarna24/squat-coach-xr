# Architecture

## System overview

Two devices on the same WiFi network, split by responsibility:

| Concern                  | Pi (Rubik Pi 3)              | Quest 3 (Unity)            |
| ------------------------ | ---------------------------- | -------------------------- |
| Camera capture           | ✅ Logitech Brio 105 via USB |                            |
| Pose detection (33 pts)  | ✅ MediaPipe Tasks, CPU      |                            |
| Serialization / network  | ✅ WebSocket server (JSON)   | ✅ WebSocket client        |
| Form analysis            |                              | ✅ C# port of Python logic |
| Rep / set state machine  |                              | ✅                         |
| HUD rendering            |                              | ✅ Unity Canvas / UI       |
| Voice coach (TTS)        |                              | ✅ Android TextToSpeech    |
| Session persistence      |                              | ✅ Local JSON              |

## Runtime data flow

```
camera frame (BGR, 640x480 @ 30 fps)
       │
       ▼
  MediaPipe Pose Landmarker (CPU delegate)
       │
       ▼
  33 landmarks (x, y, z ∈ [0, 1], visibility)
       │
       ▼
  protocol.py   →  JSON text frame  ──WS──▶  LandmarkWebSocketClient.cs
                                                 │
                                                 ▼
                                            PoseFrame (C# struct)
                                                 │
                                                 ▼
                                         SquatAnalyzer (state machine)
                                                 │
                             ┌───────────────────┼───────────────────┐
                             ▼                   ▼                   ▼
                         HudPanel           VoiceCoach           SessionLogger
```

## Why this split

- **Keep the Pi dumb.** Bug-fixing analyzer logic is fastest when the iterated
  code is in one place (the Quest app). Redeploying Pi code over SSH for every
  threshold tweak would be painful.
- **NPU future.** The Pi stays CPU-only today, but later we can swap the
  detector for a QNN/Hexagon-delegated model without touching the Quest.
- **Swappable camera source.** The Quest never sees pixels — only landmarks.
  We can change camera or even swap the Pi for a different device entirely,
  as long as it speaks the same WebSocket JSON.

## Latency budget

Target end-to-end (camera shutter → Quest UI update): **≤ 100 ms**.

| Stage                       | Typical (CPU delegate) |
| --------------------------- | ---------------------- |
| Camera capture              | ~15 ms                 |
| MediaPipe inference (Pi)    | 30–60 ms               |
| JSON encode + WS send       | <2 ms                  |
| WiFi LAN hop                | 2–10 ms                |
| WS recv + JSON decode (C#)  | <2 ms                  |
| Analyzer + render (1 frame) | 8–16 ms                |
| **Total**                   | **~60–105 ms**         |

If this feels sluggish in practice, the biggest lever is switching the Pi's
delegate from CPU to the Hexagon NPU (expected 3–5× speed-up).

## Failure modes and how we handle them

- **Pi restarts / disconnects.** The Quest client auto-reconnects with
  exponential backoff. The analyzer's state machine is resilient: when frames
  stop arriving, it idles until frames resume.
- **Packet loss.** Each message is self-contained and timestamped. Dropped
  frames cause at most a skipped sample in the rolling smoothing window.
- **Clock skew.** We use the Pi's monotonic timestamp for sequencing, and the
  Quest's own clock for rep timing. The two clocks never need to agree on a
  wall time.
- **Camera unplugged.** The Pi sends a `nopose` message every frame it cannot
  detect, so the Quest's "step into frame" message is driven by explicit data
  rather than silence.

## Non-goals (for v1)

- Multi-user / multi-Pi coordination.
- Cloud sync of sessions.
- On-Quest pose detection (i.e. using passthrough cameras). Meta keeps those
  APIs locked down; the Pi sidecar is precisely how we sidestep that.
- Anything other than bodyweight squats. The analyzer is squat-specific by
  design; supporting other exercises means shipping additional analyzers.
