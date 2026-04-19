using System;
using System.Collections;
using System.Threading.Tasks;
using NativeWebSocket;
using UnityEngine;
using SquatCoach.Analysis;

namespace SquatCoach.Networking
{
    /// <summary>
    /// WebSocket client that parses pose / nopose frames from the Pi and
    /// surfaces them as C# events. Survives disconnects with exponential
    /// backoff reconnect.
    ///
    /// Uses NativeWebSocket (https://github.com/endel/NativeWebSocket) which
    /// works cross-platform including Android (Quest). Install via UPM git URL.
    /// </summary>
    public class LandmarkWebSocketClient : MonoBehaviour
    {
        public enum State { Disconnected, Connecting, Connected }

        public event Action<WireMessages.HelloInfo> OnHello;
        public event Action<PoseFrame> OnPose;
        public event Action OnNoPose;
        public event Action<State> OnStateChanged;
        public event Action<string> OnError;

        public State CurrentState { get; private set; } = State.Disconnected;

        [Tooltip("If true, the client auto-connects on Start(). Set false to drive from AppController.")]
        public bool autoConnect = false;

        [Tooltip("Initial reconnect delay in seconds; doubles up to MaxReconnectDelay.")]
        public float initialReconnectDelay = 0.5f;

        [Tooltip("Maximum reconnect delay in seconds.")]
        public float maxReconnectDelay = 5.0f;

        // Pre-allocated per-frame buffers. Because WebSocketSharp-style callbacks
        // fire on the main thread (via NativeWebSocket's dispatcher on platforms
        // that need it), we can write into these safely.
        private readonly Vector3[] _points = new Vector3[LM.Count];
        private readonly float[] _vis = new float[LM.Count];

        private WebSocket _ws;
        private bool _stopRequested;
        private float _reconnectDelay;

        // --- lifecycle ----------------------------------------------------

        private void Start()
        {
            _reconnectDelay = initialReconnectDelay;
            if (autoConnect) Connect();
        }

        private void Update()
        {
            // Required on non-WebGL platforms to pump the NativeWebSocket
            // message queue onto the main thread.
#if !UNITY_WEBGL || UNITY_EDITOR
            _ws?.DispatchMessageQueue();
#endif
        }

        private void OnDestroy()
        {
            _stopRequested = true;
            Close();
        }

        private void OnApplicationQuit()
        {
            _stopRequested = true;
            Close();
        }

        // --- public API ---------------------------------------------------

        public async void Connect()
        {
            if (CurrentState == State.Connecting || CurrentState == State.Connected) return;
            string url = ConnectionConfig.WebSocketUrl;
            if (string.IsNullOrEmpty(url))
            {
                OnError?.Invoke("No Pi host configured.");
                return;
            }
            await ConnectInternal(url);
        }

        public async void Close()
        {
            if (_ws != null)
            {
                try { await _ws.Close(); } catch { /* swallow */ }
                _ws = null;
            }
            SetState(State.Disconnected);
        }

        public async Task SendControlPause() => await SendText(WireMessages.ControlPause());
        public async Task SendControlResume() => await SendText(WireMessages.ControlResume());
        public async Task SendControlFps(int fps) => await SendText(WireMessages.ControlSetFps(fps));

        // --- internals ----------------------------------------------------

        private async Task SendText(string text)
        {
            if (_ws == null || _ws.State != WebSocketState.Open) return;
            try { await _ws.SendText(text); }
            catch (Exception e) { OnError?.Invoke("send failed: " + e.Message); }
        }

        private async Task ConnectInternal(string url)
        {
            SetState(State.Connecting);
            _ws = new WebSocket(url);

            _ws.OnOpen += () =>
            {
                _reconnectDelay = initialReconnectDelay; // reset backoff
                SetState(State.Connected);
            };
            _ws.OnError += (err) => OnError?.Invoke(err);
            _ws.OnClose += (code) =>
            {
                SetState(State.Disconnected);
                if (!_stopRequested)
                    StartCoroutine(ReconnectLater());
            };
            _ws.OnMessage += HandleMessage;

            try
            {
                await _ws.Connect();
            }
            catch (Exception e)
            {
                OnError?.Invoke("connect failed: " + e.Message);
                SetState(State.Disconnected);
                if (!_stopRequested)
                    StartCoroutine(ReconnectLater());
            }
        }

        private IEnumerator ReconnectLater()
        {
            float delay = Mathf.Min(_reconnectDelay, maxReconnectDelay);
            yield return new WaitForSeconds(delay);
            _reconnectDelay = Mathf.Min(_reconnectDelay * 2f, maxReconnectDelay);
            if (!_stopRequested)
                Connect();
        }

        private void HandleMessage(byte[] data)
        {
            string text;
            try { text = System.Text.Encoding.UTF8.GetString(data); }
            catch { return; }

            var kind = WireMessages.ParseKind(text, out var root);
            switch (kind)
            {
                case WireMessages.Kind.Hello:
                    OnHello?.Invoke(WireMessages.ParseHello(root));
                    break;
                case WireMessages.Kind.Pose:
                {
                    if (WireMessages.ParsePose(root, _points, _vis, out int seq, out long ts, out int w, out int h))
                    {
                        var frame = new PoseFrame
                        {
                            Seq = seq,
                            TsMs = ts,
                            ImageW = w,
                            ImageH = h,
                            Points = _points,
                            Vis = _vis,
                        };
                        OnPose?.Invoke(frame);
                    }
                    break;
                }
                case WireMessages.Kind.NoPose:
                    OnNoPose?.Invoke();
                    break;
                case WireMessages.Kind.Bye:
                    Close();
                    break;
            }
        }

        private void SetState(State s)
        {
            if (CurrentState == s) return;
            CurrentState = s;
            OnStateChanged?.Invoke(s);
        }
    }
}
