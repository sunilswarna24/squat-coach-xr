"""
Minimal WebSocket probe for the Pi edge server.

Connects to the Pi's ws://HOST:PORT, prints the handshake, then reads N frames
so you can confirm the full pipeline (camera -> MediaPipe -> wire protocol)
is healthy without needing a Quest on hand.

Usage:
    python scripts/probe_client.py                            # defaults
    python scripts/probe_client.py ws://192.168.1.42:8765     # override URL
    python scripts/probe_client.py ws://...  200              # read 200 frames

Output for pose frames shows the first and last landmark's normalized
coords and visibility so you can sanity-check that the person is centered
in the image and all 33 landmarks are being emitted.
"""

import asyncio
import json
import sys
import time

import websockets

URL = sys.argv[1] if len(sys.argv) > 1 else "ws://172.25.117.54:8765"
N = int(sys.argv[2]) if len(sys.argv) > 2 else 90


async def main() -> None:
    print(f"connecting to {URL} ...")
    t0 = time.time()
    async with websockets.connect(URL, ping_interval=20, ping_timeout=20) as ws:
        print(f"  open in {(time.time() - t0) * 1000:.0f} ms")

        poses = 0
        nopes = 0
        last_seq = None
        jumps = 0

        for _ in range(N):
            raw = await asyncio.wait_for(ws.recv(), timeout=5.0)
            m = json.loads(raw)
            typ = m.get("type")

            if typ == "hello":
                print(
                    f"  hello: model={m.get('model')} delegate={m.get('delegate')} "
                    f"res={m.get('image_w')}x{m.get('image_h')} fps={m.get('target_fps')}"
                )
                continue

            if typ == "pose":
                poses += 1
                seq = m.get("seq")
                if last_seq is not None and seq != last_seq + 1:
                    jumps += 1
                last_seq = seq
                if poses <= 3:
                    lms = m.get("landmarks", [])
                    if lms:
                        first, last = lms[0], lms[-1]
                        print(
                            f"  pose seq={seq} ts_ms={m.get('ts_ms')} "
                            f"landmarks={len(lms)}  "
                            f"first=({first['x']:.2f},{first['y']:.2f},v={first['v']:.2f}) "
                            f"last=({last['x']:.2f},{last['y']:.2f},v={last['v']:.2f})"
                        )
                    else:
                        print(f"  pose seq={seq} landmarks=0 (empty!)")
            elif typ == "nopose":
                nopes += 1
                if nopes <= 2:
                    print(f"  nopose seq={m.get('seq')}")
            else:
                print(f"  other: {m}")

        print(
            f"received: {poses} pose, {nopes} nopose in {N} frames "
            f"(seq jumps: {jumps})"
        )


if __name__ == "__main__":
    asyncio.run(main())
