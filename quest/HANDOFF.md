# Quest Side — Hackathon Hand-off

**Goal:** build a sideloadable Quest 3 APK that connects to the Pi, shows the
translucent HUD with the live side-view mannequin, and coaches form in real
time — **in under an hour**.

This file is written for both a human and a Cursor agent running on the
teammate's PC. It assumes:

- You have the repo cloned: `git clone https://github.com/sunilswarna24/squat-coach-xr.git`
- You work inside the `quest/` folder (Unity project root).
- The Pi is already running and reachable on the LAN. Confirm with:
  ```bash
  pip install websockets
  python ../scripts/probe_client.py ws://<pi-ip>:8765 30
  ```

---

## Step 1 — Install Unity (once per machine)

Install **Unity Hub** from https://unity.com/download, then in Hub:

1. **Installs** → **Install Editor** → **Unity 2022.3.48f1 (LTS)**
2. In the module picker, check:
   - **Android Build Support**
     - **OpenJDK**
     - **Android SDK & NDK Tools**
3. Click Install and wait. This is ~10 GB; it'll take 10–20 min.

> Don't have an LTS earlier than 2022.3.30? Anything in the `2022.3.X` LTS
> line works. Upgrade prompts from Unity are fine to accept.

## Step 2 — Open the project

1. Unity Hub → **Open** → point at the `quest/` folder in your clone.
2. Accept the auto package resolve. The first open takes ~3 minutes as Unity
   pulls:
   - TextMeshPro
   - Newtonsoft JSON
   - XR Plugin Management + Oculus XR Plugin
   - NativeWebSocket (fetched from GitHub)
3. When TextMeshPro pops up asking to **Import TMP Essentials**, click
   **Import TMP Essentials**. (Skip the Examples import.)

### Troubleshooting Step 2

- **NativeWebSocket failed to resolve** — rare network hiccup. Open
  `Packages/manifest.json`, delete the `com.endel.nativewebsocket` line, and
  re-add via **Window → Package Manager → + → Add package from git URL**:
  `https://github.com/endel/NativeWebSocket.git#upm`
- **Compile errors in `SquatCoach.*` namespaces** — you opened the wrong
  folder. Open the `quest/` folder specifically, not the repo root.

## Step 3 — Switch to Android target

1. **File → Build Settings → Android → Switch Platform**
2. Wait for the reimport (~2 min).
3. Still in Build Settings, **Add Open Scenes** won't do anything yet (no
   scene saved). We fix that in Step 5.

## Step 4 — Configure XR + player settings

1. **Edit → Project Settings → XR Plug-in Management → Install XR Plug-in Management** (already installed via manifest, the button may say "Initialize").
2. Select the **Android** tab. Check **Oculus**.
3. In the left panel, under **XR Plug-in Management → Oculus**, in the
   **Android** tab:
   - **Stereo Rendering Mode**: Multiview
   - **Target Devices**: check **Quest 3** (and any other Quest you own)
4. Still in Project Settings, go to **Player**:
   - **Company Name**: your team name (any string)
   - **Product Name**: `ExerciseRight` (or whatever you like)
   - **Other Settings**:
     - **Minimum API Level**: Android 12.0 (API 32) or higher
     - **Target API Level**: Automatic (highest installed)
     - **Scripting Backend**: **IL2CPP**
     - **Target Architectures**: check **ARM64**, uncheck **ARMv7**
     - **Api Compatibility Level**: .NET Standard 2.1

## Step 5 — Build the Main scene (one click)

1. In the Unity menu bar, click **SquatCoach → Build Main Scene**.
2. Confirm the prompt. The editor will create
   `Assets/SquatCoach/Scenes/Main.unity` and wire every Inspector reference
   for you (the AppController, HUD, mannequin, IP entry panel, all fields
   populated — nothing to drag).
3. The scene is automatically added to Build Settings.

### What the scene looks like

- A floating translucent HUD window in front of the user
- Left column: **REPS**, **SET**, **TOTAL**, connection status, status line,
  correction cue, muted badge
- Right column: a live side-view mannequin (stick figure) that turns body
  parts red when the analyzer flags an issue and shows a yellow arrow for
  the correction direction
- An IP entry canvas with host + port inputs and a Connect button (shown
  only on first launch)

## Step 6 — Build the APK

1. **File → Build Settings** → confirm `Main.unity` is the only checked
   scene.
2. Make sure Target is **Android**.
3. **Build**. Save as `build/ExerciseRight.apk` (anywhere is fine).
4. First build takes 5–10 minutes (shader compilation, IL2CPP codegen).

## Step 7 — Prepare the Quest 3

If Developer Mode isn't already on, follow
[`quest/QUEST_DEVMODE.md`](./QUEST_DEVMODE.md).

With the Quest in Developer Mode and connected via USB-C:

```bash
# Verify the Quest is seen
adb devices

# If the Quest is listed as "unauthorized", put it on, tap "Allow" on the
# "Allow USB debugging" prompt, then rerun `adb devices`.

# Install
adb install -r build/ExerciseRight.apk
```

## Step 8 — First run on the Quest

1. Put the Quest on. The new app is under **Unknown Sources** in the app
   launcher. Look for your team/product name.
2. On first launch you'll see the IP entry panel. Type the Pi's IP (e.g.
   `172.25.117.54`) and leave port at `8765`. Press **Connect**.
3. You should hear a welcome line over the Quest speakers (TTS), see the
   HUD flip to **Connected** in green, and watch the mannequin move in
   real-time as you or someone else stands side-on to the Pi's camera.

## If something goes wrong

| Symptom                                          | Likely cause                                                    | Fix |
| ------------------------------------------------ | --------------------------------------------------------------- | --- |
| HUD says "Connecting..." forever                 | Wrong IP, wrong port, Pi server down, firewall between devices  | Re-run the PC probe against the same URL; check Pi server log at `~/squat-coach-xr/pi/server.log` |
| HUD connects but no mannequin moves              | Nobody in the camera frame, or camera aimed wrong               | Confirm by running the PC probe — it should report non-zero `pose` frames |
| Mannequin looks random / twitchy                 | Camera too close or off-axis                                    | Pi should see the full body from the side, hip height, ~6 ft away |
| No voice                                         | TTS not available on the Quest (first launch) or Quest muted    | Check the Quest's speaker volume; second launch usually works |
| Black screen on Quest                            | XR Plug-in Management didn't include Oculus for Android         | Redo Step 4; ensure Oculus is checked under the **Android** tab |
| `adb install` fails: "INSTALL_FAILED_USER_RESTRICTED" | Developer Mode off                                         | Follow `QUEST_DEVMODE.md` |

## Iteration loop

Once it runs, iteration is fast:

```
edit C# → Build → adb install -r build/ExerciseRight.apk → put headset on
```

Most tuning is in `Assets/SquatCoach/Scripts/Analysis/SensitivityPreset.cs`
and the style fields of `MannequinRenderer` / `HudPanel` (Inspector tunable
in the scene).
