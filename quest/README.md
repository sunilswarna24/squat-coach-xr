# Quest (Unity) — WebSocket client + analyzer + HUD + TTS

Unity-side of Squat Coach XR. Consumes the Pi's WebSocket landmark stream,
runs the squat analyzer in C# (ported from the Python prototype), renders a
floating 2D HUD, and speaks corrections through the Quest's speakers via the
Android `TextToSpeech` API.

> The scripts under `Assets/SquatCoach/Scripts/` are complete and self-
> consistent, but Unity projects cannot be fully represented in git without
> the generated `Library/`, `Packages/`, and `ProjectSettings/` folders. See
> "First-time Unity setup" below for how to turn this folder into a running
> Unity project.

## Requirements

- Unity **2022.3 LTS** (tested path; 6.x Android builds also work)
- Meta Quest 3 in Developer Mode
- A USB-C cable for deployment
- On your dev machine: Android Build Support + Android SDK/NDK (installed
  via Unity Hub → Add Modules)

## First-time Unity setup

1. **Create a new Unity project** pointing at this folder.
   - In Unity Hub: *Projects → Add → Select this `quest/` folder*.
   - Template: **3D (URP)**.
   - Unity will create the missing `Library/`, `Packages/`, `ProjectSettings/`
     folders. Those are gitignored at the repo root; only `Assets/` is tracked.

2. **Install the required UPM packages** from *Window → Package Manager*:
   - **Meta XR All-in-One SDK** (com.meta.xr.sdk.all)
     *Or at minimum: Meta XR Core SDK + Meta XR Interaction SDK.*
   - **Newtonsoft JSON** (`com.unity.nuget.newtonsoft-json`)
     — needed by the WebSocket parser.
   - **NativeWebSocket** via *Add package from git URL*:
     `https://github.com/endel/NativeWebSocket.git#upm`
   - **TextMeshPro** (usually auto-installed the first time you use a TMP text).

3. **Switch build target to Android.**
   *File → Build Settings → Android → Switch Platform.*
   - Minimum API: 29
   - Target API: latest installed
   - Graphics API: `Vulkan` (recommended) or `OpenGLES3`
   - Scripting Backend: `IL2CPP`
   - Target Architectures: `ARM64` (required for Quest)

4. **Meta XR project setup.** In *Meta → Tools → Project Setup Tool*, apply
   all recommended fixes for Quest.

## Building the scene

Make one scene called `Main` under `Assets/SquatCoach/Scenes/`.

### GameObject hierarchy

```
Main (Scene)
├── OVRCameraRig                         # from Meta XR SDK
├── App (empty)                          # → AppController
│   ├── WebSocketClient                  # → LandmarkWebSocketClient
│   └── VoiceCoach                       # → VoiceCoach
├── HUDCanvas (World-Space Canvas)
│   └── HudPanel (translucent window)    # → HudPanel, + Image (background)
│       ├── LeftColumn
│       │   ├── RepsValue (TMP_Text)
│       │   ├── SetValue  (TMP_Text)
│       │   ├── TotalValue (TMP_Text)
│       │   ├── Connection (TMP_Text)
│       │   ├── Status     (TMP_Text)
│       │   ├── Cue        (TMP_Text)       # shows "Chest up" etc.
│       │   └── MutedBadge (GameObject)
│       └── RightColumn
│           └── Mannequin (RectTransform)    # → MannequinRenderer
│               └── MannequinGraphic         # → MannequinGraphic
└── IPEntryCanvas (World-Space Canvas)
    └── IpEntryPanel                        # → IpEntryPanel
```

### AppController wiring

On the `App` GameObject, add an **AppController** component and drag:

- `WebSocketClient` → **wsClient**
- `VoiceCoach` → **voiceCoach**
- `HudPanel` → **hud**
- `IpEntryPanel` → **ipEntryPanel**

Leave `autoConnect` off on `LandmarkWebSocketClient` — the AppController
drives it (it needs to know whether the IP is configured first).

### HUD panel wiring

The HUD is a **plain translucent window**. It has a solid `Image` component
as its background (any color is fine; the script forces the alpha). Drop the
following TMP text children in and wire them up in the `HudPanel` Inspector:

| Field              | What it displays              |
| ------------------ | ----------------------------- |
| `backgroundImage`  | The root `Image` component    |
| `backgroundAlpha`  | Window translucency (0..1)    |
| `repsValueText`    | Reps in the current set       |
| `setValueText`     | Current set number            |
| `totalValueText`   | Total reps across the session |
| `connectionText`   | "Connected" / "Reconnecting…" |
| `statusText`       | "Step into frame" etc.        |
| `cueText`          | "Chest up", "Hips back", …    |
| `mutedBadge`       | A tiny icon shown when muted  |
| `mannequin`        | The `MannequinRenderer` in the right column |

### Mannequin wiring

The right column contains a child `RectTransform` sized to taste (the live
mannequin auto-fits whatever rect you give it). Put both scripts on it:

- **MannequinRenderer** — orchestrates the drawing. Assign its `graphic`
  field to the sibling MannequinGraphic. Tune colors and thickness in the
  Inspector.
- **MannequinGraphic** — the custom UI Graphic that does the actual mesh
  generation. No extra setup required.

The mannequin is exercise-agnostic: it draws a side-view stick figure from
the live landmarks, colors the bones/joints red when the analyzer flags an
issue, and adds a yellow arrow showing the correction direction (e.g. chest
up for a forward lean, hips back for knees drifting past the toes, heels
down when a heel lift is detected).

### IP entry wiring

In `IpEntryPanel`:

- `hostInput` — a `TMP_InputField` (placeholder: "192.168.1.42")
- `portInput` — a `TMP_InputField` (default "8765")
- `connectButton` — any `Button`
- `statusText` — a `TMP_Text` for feedback

## Build and deploy

1. Plug the Quest in over USB. Accept the "Allow USB Debugging" prompt in
   the headset the first time.
2. *File → Build Settings → Build And Run*.
3. On the Quest, the app appears under *Library → Unknown Sources*.

## Running it

1. Start the Pi side (see `../pi/README.md`).
2. Put the Quest on, launch the app.
3. On first run, enter the Pi's IP (e.g. `192.168.1.42`) and tap **Connect**.
4. Stand side-on, ~6 feet from the camera, whole body in frame.
5. Do squats. HUD updates live; Android TTS speaks corrections.

## Script layout

```
Assets/SquatCoach/Scripts/
├── App/
│   └── AppController.cs            Top-level glue
├── Networking/
│   ├── ConnectionConfig.cs         Persisted Pi host/port
│   ├── LandmarkWebSocketClient.cs  NativeWebSocket client + reconnect
│   └── WireMessages.cs             JSON parse / build
├── Analysis/
│   ├── LM.cs                       Landmark indices
│   ├── PoseFrame.cs                Per-frame pose struct
│   ├── Geometry.cs                 Angle helpers
│   ├── FrameMetrics.cs             Per-frame numbers for HUD
│   ├── SensitivityPreset.cs        Preset thresholds (low/medium/high)
│   ├── RepRecord.cs                Rep + Set records
│   └── SquatAnalyzer.cs            State machine + rep counting (port)
├── Coaching/
│   ├── AndroidTts.cs               android.speech.tts wrapper
│   ├── IssueMessages.cs            Spoken copy
│   └── VoiceCoach.cs               Cooldown + cycling
├── Session/
│   ├── SessionRecord.cs            JSON shape
│   └── SessionLogger.cs            Local persistence
└── UI/
    ├── HudPanel.cs                 HUD rendering (translucent, two columns)
    ├── MannequinGraphic.cs         Custom UI graphic: bones, joints, arrows
    ├── MannequinRenderer.cs        Maps landmarks + issues to the mannequin
    └── IpEntryPanel.cs             First-run IP entry
```

## Platform notes

- **Editor play mode** works without a Quest: Android TTS is stubbed to
  `Debug.Log`, and NativeWebSocket works from the Editor on Windows/macOS/Linux
  so you can test against the Pi running on the same laptop.
- **NewtonSoft vs JsonUtility.** Unity's built-in `JsonUtility` can't parse
  the landmarks array cleanly (it requires concrete POCOs and doesn't handle
  dictionaries well). We use Newtonsoft for inbound traffic and let the
  ~30 Hz parsing allocate mildly — it's still well under a ms per frame.
- **Mono vs IL2CPP.** IL2CPP is required for device builds. The scripts use
  only serialization patterns that IL2CPP can AOT-compile without
  link.xml tweaks.

## Troubleshooting

- **`CS0234: namespace 'NativeWebSocket' does not exist`** — install the
  package via UPM git URL (step 2 above).
- **TTS silent on Quest** — Meta's permissions model allows TTS without
  extra permissions, but if you hear nothing, check the Quest system volume
  and try opening the project from *Meta → Tools → Project Setup Tool* which
  sometimes flags missing audio settings.
- **WebSocket fails to connect** — confirm the Pi is reachable with
  `websocat ws://<pi-ip>:8765` from your laptop first. If that works but
  the Quest can't connect, your WiFi may have AP client isolation on.
