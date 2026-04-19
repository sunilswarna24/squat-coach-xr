# Wire Protocol

All traffic is **WebSocket text frames containing UTF-8 JSON**. Every
message has a `type` discriminator. Versioned by a top-level `v` field.

The schema lives at [`../protocol/schema.json`](../protocol/schema.json) and
concrete examples are in [`../protocol/samples/`](../protocol/samples/).

## Connection lifecycle

1. Quest connects to `ws://<pi-ip>:8765`.
2. Pi immediately sends `hello` with metadata.
3. Pi then streams `pose` (or `nopose`) messages at a target rate (30 Hz).
4. Quest may send `control` messages (pause, set_fps, mirror).
5. Either side may close the connection cleanly.

## Messages

### `hello` — server → client (once, on connect)

```json
{
  "v": 1,
  "type": "hello",
  "model": "mediapipe_pose_landmarker_lite",
  "delegate": "cpu",
  "image_w": 640,
  "image_h": 480,
  "target_fps": 30,
  "server_ts_ms": 123456789
}
```

### `pose` — server → client (~30 Hz)

```json
{
  "v": 1,
  "type": "pose",
  "seq": 451,
  "ts_ms": 123456789,
  "w": 640,
  "h": 480,
  "landmarks": [
    { "x": 0.41, "y": 0.22, "z": -0.03, "v": 0.98 },
    // ... 33 total, in MediaPipe order
  ]
}
```

- `x`, `y` are normalized to the image (0..1). `y` grows downward.
- `z` is normalized depth relative to the hip; sign convention matches
  MediaPipe (negative = toward camera).
- `v` is visibility ∈ [0, 1].
- `seq` is monotonically increasing per-connection.
- `ts_ms` is the Pi's monotonic clock at capture time (for relative timing
  only; do not assume it is wall time).

### `nopose` — server → client

Sent every frame the Pi fails to detect a pose, at the same target rate as
`pose`. The Quest uses these to drive "step into frame" prompts.

```json
{ "v": 1, "type": "nopose", "seq": 452, "ts_ms": 123456822 }
```

### `control` — client → server

Reserved for future use. The Pi accepts but currently ignores these.

```json
{ "v": 1, "type": "control", "action": "pause" }
{ "v": 1, "type": "control", "action": "set_fps", "fps": 15 }
```

### `bye` — either direction

Polite close before dropping the connection. Optional.

```json
{ "v": 1, "type": "bye", "reason": "shutdown" }
```

## Landmark index reference

Matches MediaPipe Pose. Key indices we actually use on the Quest:

| Index | Name            |
| ----- | --------------- |
| 0     | nose            |
| 7     | left_ear        |
| 8     | right_ear       |
| 11    | left_shoulder   |
| 12    | right_shoulder  |
| 23    | left_hip        |
| 24    | right_hip       |
| 25    | left_knee       |
| 26    | right_knee      |
| 27    | left_ankle      |
| 28    | right_ankle     |
| 29    | left_heel       |
| 30    | right_heel      |
| 31    | left_foot_index |
| 32    | right_foot_index|

## Versioning

Breaking changes bump `v`. The Quest client refuses to parse messages with a
`v` it does not recognize and logs a user-visible update-your-apps warning.
