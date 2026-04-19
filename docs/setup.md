# Setup

End-to-end guide to get the Pi and the Quest talking.

## 0. Network assumptions

- Both devices on the same WiFi (ideally 5 GHz, low congestion).
- No VLAN / client-isolation on the AP. If your router has a "guest network"
  mode that blocks device-to-device traffic, the Quest won't see the Pi.
- You have the Pi's IP address handy (e.g. `192.168.1.42`).

Find the Pi's IP with `ip addr show wlan0` on the Pi, or check your router's
DHCP leases page.

## 1. Pi side

See [`../pi/README.md`](../pi/README.md) for details. TL;DR:

```bash
cd pi
python3 -m venv .venv
source .venv/bin/activate          # on Windows: .\.venv\Scripts\Activate.ps1
pip install -r requirements.txt
python -m src.main
```

The server listens on `0.0.0.0:8765` by default. It prints the IPs you can
reach it on, e.g. `serving on 192.168.1.42:8765`.

### Sanity-check the server without the Quest

Open a second terminal on any machine on the same network, and connect any
WebSocket client (e.g. `websocat`) to verify you see `pose` JSON:

```bash
websocat ws://192.168.1.42:8765
```

You should see a `hello` message, then a stream of `pose` objects at ~30 Hz.

## 2. Quest side

See [`../quest/README.md`](../quest/README.md). TL;DR:

1. Open the `quest/` folder as a Unity 2022.3 LTS project.
2. Install the required packages (Meta XR All-in-One, NativeWebSocket,
   Newtonsoft JSON) — the Quest README lists UPM URLs.
3. Open the `Main` scene, enter the Pi's IP in the Inspector on the
   `AppController` component.
4. Build and deploy to the Quest over USB.

## 3. Using it

- Put the Quest on and launch the app.
- A small panel will ask you to enter the Pi's IP. Done once per fresh
  install; it's persisted in PlayerPrefs after that.
- Stand side-on to the camera, about 6 feet away, entire body in frame.
- Do squats. HUD updates live, voice speaks corrections.

## Troubleshooting

- **"No pose detected" forever.** Camera or MediaPipe problem on the Pi. Run
  the Pi with `--debug-preview` to see a window of what the camera sees.
- **Quest can't connect.** Network isolation, wrong IP, or firewall. On the Pi,
  `sudo ufw allow 8765/tcp` (if UFW is on). Try `websocat` from a laptop first.
- **Choppy / stale UI.** WiFi congestion, or the Pi is CPU-saturated. Drop
  target FPS with `--fps 15` on the Pi as a temporary fix.
- **Voice is cutting out.** Android TTS likes one utterance at a time; the
  `VoiceCoach` queues them. If you still hear cutouts, lengthen the cooldown
  in `SensitivityPreset`.
