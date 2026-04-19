"""
WebSocket server: fans out the Pi's pose stream to any connected clients.

Threading model:
- The capture thread (see main.py) does camera reads and MediaPipe inference.
  When a new pose (or `nopose`) is ready, it calls `PoseBroadcaster.publish()`.
- The asyncio event loop owns the WebSocket server. Each connected client has
  its own per-client asyncio.Queue. `publish()` schedules a thread-safe
  enqueue onto the asyncio loop via `loop.call_soon_threadsafe`.
- Each connection coroutine drains its queue to its socket. Slow clients are
  dropped rather than allowed to hold up others.

The "latest wins" strategy: if a client is slow, its queue is capped, and we
overwrite the pending item rather than grow unbounded. A stale pose is worse
than a dropped pose for real-time coaching.
"""

from __future__ import annotations

import asyncio
import logging
from typing import Optional, Set

import websockets
from websockets.server import WebSocketServerProtocol

from .protocol import decode, encode_bye, encode_hello


log = logging.getLogger("pi.ws")


class _ClientHandle:
    """Per-connection state: a bounded queue of pending text frames."""

    __slots__ = ("ws", "queue", "seq_sent")

    def __init__(self, ws: WebSocketServerProtocol, maxsize: int = 2) -> None:
        self.ws = ws
        # Small queue so we drop stale frames rather than let them pile up.
        self.queue: asyncio.Queue[str] = asyncio.Queue(maxsize=maxsize)
        self.seq_sent: int = 0


class PoseBroadcaster:
    """
    Accepts frames from the capture thread and fans them out to WS clients.

    Usage (in main.py):
        loop = asyncio.new_event_loop()
        bc = PoseBroadcaster(loop, hello_payload)
        # start the server:
        asyncio.run_coroutine_threadsafe(bc.serve(host, port), loop)
        # from any thread:
        bc.publish(encoded_pose_json_text)
    """

    def __init__(self, loop: asyncio.AbstractEventLoop, hello_text: str) -> None:
        self._loop = loop
        self._hello_text = hello_text
        self._clients: Set[_ClientHandle] = set()
        self._server: Optional[websockets.server.Serve] = None

    # -- async side ---------------------------------------------------------

    async def serve(self, host: str, port: int) -> None:
        """Start the WebSocket server. Blocks until the server closes."""
        async with websockets.serve(self._on_connect, host, port, ping_interval=20):
            log.info("WebSocket server listening on ws://%s:%d", host, port)
            await asyncio.Future()  # run forever

    async def _on_connect(self, ws: WebSocketServerProtocol) -> None:
        handle = _ClientHandle(ws)
        self._clients.add(handle)
        remote = ws.remote_address
        log.info("client connected: %s (clients=%d)", remote, len(self._clients))
        try:
            # Send hello immediately.
            await ws.send(self._hello_text)
            # Run reader + writer in parallel; either ending tears down both.
            reader = asyncio.create_task(self._reader_loop(handle))
            writer = asyncio.create_task(self._writer_loop(handle))
            done, pending = await asyncio.wait(
                {reader, writer}, return_when=asyncio.FIRST_COMPLETED
            )
            for t in pending:
                t.cancel()
        except websockets.ConnectionClosed:
            pass
        except Exception:
            log.exception("connection error for %s", remote)
        finally:
            self._clients.discard(handle)
            log.info("client disconnected: %s (clients=%d)", remote, len(self._clients))
            try:
                await ws.close()
            except Exception:
                pass

    async def _reader_loop(self, handle: _ClientHandle) -> None:
        """Consume inbound control messages. Currently we just log them."""
        async for raw in handle.ws:
            if not isinstance(raw, str):
                continue
            try:
                msg = decode(raw)
            except Exception as exc:
                log.warning("bad inbound message: %s (%s)", exc, raw[:200])
                continue
            if msg.get("type") == "control":
                log.info("control: %s", msg)
                # Hook: apply pause/fps/mirror later.
            elif msg.get("type") == "bye":
                log.info("client said bye")
                break

    async def _writer_loop(self, handle: _ClientHandle) -> None:
        while True:
            text = await handle.queue.get()
            try:
                await handle.ws.send(text)
            except websockets.ConnectionClosed:
                break

    # -- callable from any thread ------------------------------------------

    def publish(self, text: str) -> None:
        """Enqueue a pose or nopose frame for every connected client."""
        self._loop.call_soon_threadsafe(self._enqueue_everyone, text)

    def _enqueue_everyone(self, text: str) -> None:
        for handle in list(self._clients):
            q = handle.queue
            if q.full():
                # Drop the oldest (stalest) pending frame; we'd rather send
                # fresh data than a backlog.
                try:
                    q.get_nowait()
                except asyncio.QueueEmpty:
                    pass
            try:
                q.put_nowait(text)
            except asyncio.QueueFull:
                # Shouldn't happen after the drain above, but be defensive.
                pass

    def close(self) -> None:
        async def _close_all() -> None:
            bye = encode_bye("shutdown")
            for handle in list(self._clients):
                try:
                    await handle.ws.send(bye)
                    await handle.ws.close()
                except Exception:
                    pass

        try:
            asyncio.run_coroutine_threadsafe(_close_all(), self._loop).result(
                timeout=1.0
            )
        except Exception:
            pass


def build_hello_text(
    model: str, delegate: str, image_w: int, image_h: int, target_fps: int
) -> str:
    return encode_hello(
        model=model,
        delegate=delegate,
        image_w=image_w,
        image_h=image_h,
        target_fps=target_fps,
    )
