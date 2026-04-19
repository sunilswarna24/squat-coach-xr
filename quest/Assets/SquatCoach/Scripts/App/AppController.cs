using System.Collections.Generic;
using UnityEngine;
using SquatCoach.Analysis;
using SquatCoach.Coaching;
using SquatCoach.Networking;
using SquatCoach.Session;
using SquatCoach.UI;

namespace SquatCoach.App
{
    /// <summary>
    /// Top-level glue that wires the WebSocket client, the SquatAnalyzer,
    /// the voice coach, the HUD, and the session logger together.
    ///
    /// Scene setup (one-time, in the Unity editor):
    ///   1. Add an empty GameObject named "App" and put this component on it.
    ///   2. Drop the LandmarkWebSocketClient, VoiceCoach, HudPanel
    ///      (with its MannequinRenderer child), and IpEntryPanel scripts
    ///      onto child objects and assign the refs below in the Inspector.
    /// </summary>
    public class AppController : MonoBehaviour
    {
        [Header("Wiring")]
        public LandmarkWebSocketClient wsClient;
        public VoiceCoach voiceCoach;
        public HudPanel hud;
        public IpEntryPanel ipEntryPanel;

        [Header("Analyzer defaults")]
        public string sensitivity = "medium";
        public DepthTarget depthTarget = DepthTarget.Parallel;
        public string facing = "auto";

        [Header("Startup behaviour")]
        public bool speakWelcomeOnConnect = true;

        private readonly SquatAnalyzer _analyzer = new SquatAnalyzer();
        private SessionLogger _logger;
        private int _totalReps;
        private string _connectionLabel = "Disconnected";
        private bool _hasSpokenWelcome;

        // Latest values for Render(). The pose buffers are owned by us here
        // (not the WebSocket client) so the HUD can safely hold a reference.
        private FrameMetrics _lastMetrics = FrameMetrics.Empty;
        private List<string> _lastActiveIssues = new List<string>();
        private string _lastStatus = "Connecting...";
        private bool _hasPose;
        private readonly Vector3[] _poseShadowPoints = new Vector3[LM.Count];
        private readonly float[] _poseShadowVis = new float[LM.Count];
        private PoseFrame _poseShadow;
        private string _resolvedFacing = "auto";

        private void Awake()
        {
            _analyzer.Sensitivity = sensitivity;
            _analyzer.Depth = depthTarget;
            _analyzer.Facing = facing;

            _logger = new SessionLogger(
                sensitivity: sensitivity,
                depthTarget: depthTarget.ToString().ToLowerInvariant(),
                facing: facing);

            if (voiceCoach != null)
                voiceCoach.defaultCooldownS = SensitivityPreset.All[sensitivity].VoiceCooldownS;

            _poseShadow = new PoseFrame
            {
                Points = _poseShadowPoints,
                Vis = _poseShadowVis,
            };
        }

        private void OnEnable()
        {
            if (wsClient != null)
            {
                wsClient.OnStateChanged += HandleWsState;
                wsClient.OnHello += HandleHello;
                wsClient.OnPose += HandlePose;
                wsClient.OnNoPose += HandleNoPose;
                wsClient.OnError += (msg) =>
                {
                    Debug.LogWarning("[WS] " + msg);
                    _lastStatus = "Connection error.";
                };
            }
            if (ipEntryPanel != null)
                ipEntryPanel.OnConnectRequested += HandleConnectRequested;
        }

        private void OnDisable()
        {
            if (wsClient != null)
            {
                wsClient.OnStateChanged -= HandleWsState;
                wsClient.OnHello -= HandleHello;
                wsClient.OnPose -= HandlePose;
                wsClient.OnNoPose -= HandleNoPose;
            }
            if (ipEntryPanel != null)
                ipEntryPanel.OnConnectRequested -= HandleConnectRequested;
        }

        private void Start()
        {
            // Either show the IP entry panel on first run, or connect directly.
            if (!ConnectionConfig.IsConfigured && ipEntryPanel != null)
            {
                ipEntryPanel.gameObject.SetActive(true);
                _lastStatus = "Enter your server IP to begin.";
            }
            else
            {
                if (ipEntryPanel != null) ipEntryPanel.gameObject.SetActive(false);
                if (wsClient != null) wsClient.Connect();
            }
        }

        private void OnApplicationQuit()
        {
            var closed = _analyzer.ForceCloseSet();
            if (closed != null) _logger.AddSet(closed);
            _logger.Save();
        }

        // --- event handlers ------------------------------------------------

        private void HandleConnectRequested()
        {
            if (ipEntryPanel != null) ipEntryPanel.gameObject.SetActive(false);
            wsClient?.Connect();
        }

        private void HandleWsState(LandmarkWebSocketClient.State s)
        {
            switch (s)
            {
                case LandmarkWebSocketClient.State.Connecting:
                    _connectionLabel = "Connecting…";
                    break;
                case LandmarkWebSocketClient.State.Connected:
                    _connectionLabel = "Connected";
                    _lastStatus = "";
                    if (speakWelcomeOnConnect && !_hasSpokenWelcome)
                    {
                        _hasSpokenWelcome = true;
                        voiceCoach?.SpeakSequence("welcome");
                    }
                    break;
                default:
                    _connectionLabel = "Reconnecting…";
                    _lastStatus = "Waiting for the signal…";
                    _hasPose = false;
                    break;
            }
        }

        private void HandleHello(WireMessages.HelloInfo info)
        {
            Debug.Log($"[WS] hello: model={info.Model} delegate={info.Delegate} " +
                      $"res={info.ImageW}x{info.ImageH} fps={info.TargetFps}");
        }

        private void HandlePose(PoseFrame frame)
        {
            float now = Time.realtimeSinceStartup;
            var result = _analyzer.Update(frame, now);

            _lastMetrics = result.Metrics;
            _lastActiveIssues = result.ActiveIssues ?? new List<string>();
            _lastStatus = result.StatusMessage ?? "";
            _resolvedFacing = string.IsNullOrEmpty(_lastMetrics.Facing) ? facing : _lastMetrics.Facing;

            // Copy the pose into our shadow buffers. The incoming `frame`
            // references buffers owned by the WebSocket client, which will
            // overwrite them on the next message.
            System.Array.Copy(frame.Points, _poseShadowPoints, LM.Count);
            if (frame.Vis != null)
                System.Array.Copy(frame.Vis, _poseShadowVis, LM.Count);
            _poseShadow.Seq = frame.Seq;
            _poseShadow.TsMs = frame.TsMs;
            _poseShadow.ImageW = frame.ImageW;
            _poseShadow.ImageH = frame.ImageH;
            _hasPose = true;

            // Speak at most one in-rep cue per frame, picked by priority.
            // Each cue has its own cooldown, and VoiceCoach also refuses to
            // start a new cue while another is mid-playback, so this can't
            // produce overlapping speech.
            string inRepCue = PickCueFromIssues(_lastActiveIssues);
            if (inRepCue != null) voiceCoach?.SpeakIssue(inRepCue);

            if (result.CompletedRep != null)
            {
                _totalReps += 1;
                string cue = PickCueForRep(result.CompletedRep);
                if (cue != null) voiceCoach?.SpeakIssue(cue);
            }
            if (result.CompletedSet != null)
            {
                _logger.AddSet(result.CompletedSet);
                if (IsGoodSet(result.CompletedSet)) voiceCoach?.SpeakIssue("good_set");
            }
        }

        private void HandleNoPose()
        {
            float now = Time.realtimeSinceStartup;
            var result = _analyzer.Update(null, now);
            _lastMetrics = result.Metrics;
            _lastActiveIssues = result.ActiveIssues ?? new List<string>();
            _lastStatus = result.StatusMessage ?? "";
            _hasPose = false;
            if (result.CompletedSet != null) _logger.AddSet(result.CompletedSet);
        }

        // --- rendering -----------------------------------------------------

        private void LateUpdate()
        {
            if (hud == null) return;
            hud.Render(
                setIdx: _analyzer.SetIndex,
                repsInSet: _analyzer.RepsInSet,
                totalReps: _totalReps,
                phase: _analyzer.CurrentPhase,
                metrics: _lastMetrics,
                activeIssues: _lastActiveIssues,
                status: _lastStatus,
                connectionLabel: _connectionLabel,
                connected: wsClient != null &&
                           wsClient.CurrentState == LandmarkWebSocketClient.State.Connected,
                muted: voiceCoach != null && voiceCoach.Muted,
                poseSnapshot: _hasPose ? (PoseFrame?)_poseShadow : null,
                facing: _resolvedFacing);
        }

        // --- helpers -------------------------------------------------------

        private static string PickCueForRep(RepRecord rep)
            => IssueMessages.PickTopPriority(rep.Issues);

        private static string PickCueFromIssues(IList<string> issues)
            => IssueMessages.PickTopPriority(issues);

        private static bool IsGoodSet(SetRecord s)
        {
            if (s.RepCount == 0) return false;
            return (float)s.GoodCount / s.RepCount >= 0.8f;
        }
    }
}
