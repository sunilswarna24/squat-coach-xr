using System;
using System.Collections.Generic;
using UnityEngine;

namespace SquatCoach.Analysis
{
    /// <summary>
    /// Side-view squat analyzer: state machine, rep counting, form checks.
    /// Port of the Python analyzer at pi-side Windows prototype
    /// (squat-posture-coach/posture_analyzer.py).
    ///
    /// Pipeline per frame:
    ///   1. Resolve which side faces the camera (auto | left | right).
    ///   2. Visibility gate on that side's landmarks.
    ///   3. Compute geometric metrics (joint angles, normalised distances).
    ///   4. Feed smoothed knee angle through hysteresis state machine.
    ///   5. During a rep, accumulate per-frame form observations.
    ///   6. When a rep completes, build a RepRecord with issues.
    ///   7. Idle timeout closes the current set.
    /// </summary>
    public class SquatAnalyzer
    {
        public enum Phase { Standing, Descending, Ascending, NotInPosition }

        public string Sensitivity { get; set; } = "medium";
        public DepthTarget Depth { get; set; } = DepthTarget.Parallel;
        public string Facing { get; set; } = "auto";      // "auto" | "left" | "right"
        public float MinVisibility { get; set; } = 0.5f;
        public float SetEndIdleS { get; set; } = 6.0f;
        public float MinRealRepDurationS { get; set; } = 0.5f;
        public int SmoothingWindow { get; set; } = 3;
        public int HeelBaselineWindow { get; set; } = 15;

        public Phase CurrentPhase { get; private set; } = Phase.Standing;
        public int SetIndex { get; private set; } = 1;
        public int RepsInSet => _currentSetReps.Count;

        public IReadOnlyList<string> InRepIssueKeys => _inRepIssueKeys;
        private static readonly string[] _inRepIssueKeys =
            { "lean_forward", "knees_forward", "heel_lift" };

        // --- public result type returned by Update ------------------------
        public struct FrameResult
        {
            public FrameMetrics Metrics;
            public Phase Phase;
            public List<string> ActiveIssues;
            public RepRecord CompletedRep;     // null if none this frame
            public SetRecord CompletedSet;     // null if none this frame
            public string StatusMessage;
        }

        // --- internal state ----------------------------------------------
        private readonly Queue<float> _angleBuf = new Queue<float>();
        private readonly Queue<float> _angleTrendBuf = new Queue<float>(capacity: 5);
        private readonly Queue<float> _heelYSamples = new Queue<float>();
        private readonly Queue<float> _footLenSamples = new Queue<float>();

        private string _resolvedFacing;
        private readonly List<float> _leftVisVotes = new List<float>();
        private readonly List<float> _rightVisVotes = new List<float>();

        private float? _heelYBaseline;
        private float? _footLenBaseline;

        private float _repMinAngle = float.PositiveInfinity;
        private float _repMaxAngle = float.NegativeInfinity;
        private float? _repTStart;
        private float? _repTBottom;
        private readonly Dictionary<string, int> _repIssueFrames = new Dictionary<string, int>();

        private readonly List<RepRecord> _currentSetReps = new List<RepRecord>();
        private float? _tLastRep;

        // --- public API ---------------------------------------------------

        public SetRecord ForceCloseSet()
        {
            var closed = CloseSetIfNonEmpty();
            ResetRepState();
            return closed;
        }

        public string FlipFacing()
        {
            string current = _resolvedFacing ?? "right";
            string next = current == "right" ? "left" : "right";
            _resolvedFacing = next;
            Facing = next;
            ResetRepState();
            return next;
        }

        public FrameResult Update(PoseFrame? frameOpt, float nowS)
        {
            var result = new FrameResult
            {
                Metrics = FrameMetrics.Empty,
                Phase = CurrentPhase,
                ActiveIssues = new List<string>(),
                StatusMessage = "",
            };

            // Idle timeout is evaluated even without a frame.
            result.CompletedSet = MaybeCloseSet(nowS);

            if (!frameOpt.HasValue || !frameOpt.Value.IsValid)
            {
                result.Phase = Phase.NotInPosition;
                result.StatusMessage = "No pose — step into frame.";
                return result;
            }
            var frame = frameOpt.Value;

            string side = ResolveFacing(frame);
            float vis = MeanVisibility(frame, side);
            if (vis < MinVisibility)
            {
                result.Metrics = new FrameMetrics { VisibilityOk = false, Facing = side };
                result.Phase = Phase.NotInPosition;
                result.StatusMessage = "Keep hips, knees and ankles in the frame.";
                return result;
            }

            ComputeMetrics(frame, side, out var metrics);
            result.Metrics = metrics;

            var completedRep = AdvanceStateMachine(metrics, nowS);
            if (completedRep != null) _tLastRep = nowS;

            result.ActiveIssues = CollectInRepIssues(metrics);
            foreach (var k in result.ActiveIssues)
            {
                _repIssueFrames.TryGetValue(k, out int c);
                _repIssueFrames[k] = c + 1;
            }

            result.Phase = CurrentPhase;
            result.CompletedRep = completedRep;
            if (completedRep != null)
                result.CompletedSet ??= MaybeCloseSet(nowS);
            return result;
        }

        // --- facing side --------------------------------------------------

        private string ResolveFacing(PoseFrame f)
        {
            if (Facing == "left" || Facing == "right")
            {
                _resolvedFacing = Facing;
                return Facing;
            }
            if (_resolvedFacing != null) return _resolvedFacing;

            _leftVisVotes.Add(MeanVisibility(f, "left"));
            _rightVisVotes.Add(MeanVisibility(f, "right"));
            if (_leftVisVotes.Count >= 30)
            {
                float l = Avg(_leftVisVotes);
                float r = Avg(_rightVisVotes);
                _resolvedFacing = l > r ? "left" : "right";
                return _resolvedFacing;
            }
            return MeanVisibility(f, "left") > MeanVisibility(f, "right") ? "left" : "right";
        }

        private static float Avg(List<float> xs)
        {
            if (xs.Count == 0) return 0f;
            float s = 0f;
            for (int i = 0; i < xs.Count; i++) s += xs[i];
            return s / xs.Count;
        }

        private static readonly Dictionary<string, int[]> SideLandmarks = new Dictionary<string, int[]>
        {
            ["left"]  = new[] { LM.LeftEar, LM.LeftShoulder, LM.LeftHip, LM.LeftKnee, LM.LeftAnkle, LM.LeftHeel, LM.LeftFootIndex },
            ["right"] = new[] { LM.RightEar, LM.RightShoulder, LM.RightHip, LM.RightKnee, LM.RightAnkle, LM.RightHeel, LM.RightFootIndex },
        };

        private static float MeanVisibility(PoseFrame f, string side)
        {
            int[] idxs = SideLandmarks[side];
            float s = 0f;
            for (int i = 0; i < idxs.Length; i++) s += f.Visibility(idxs[i]);
            return s / idxs.Length;
        }

        // --- metric computation ------------------------------------------

        private void ComputeMetrics(PoseFrame frame, string side, out FrameMetrics metrics)
        {
            int earIdx, shoulderIdx, hipIdx, kneeIdx, ankleIdx, heelIdx, toeIdx;
            if (side == "left")
            {
                earIdx = LM.LeftEar; shoulderIdx = LM.LeftShoulder;
                hipIdx = LM.LeftHip; kneeIdx = LM.LeftKnee;
                ankleIdx = LM.LeftAnkle; heelIdx = LM.LeftHeel; toeIdx = LM.LeftFootIndex;
            }
            else
            {
                earIdx = LM.RightEar; shoulderIdx = LM.RightShoulder;
                hipIdx = LM.RightHip; kneeIdx = LM.RightKnee;
                ankleIdx = LM.RightAnkle; heelIdx = LM.RightHeel; toeIdx = LM.RightFootIndex;
            }

            Vector2 shoulder = frame.Pixel(shoulderIdx);
            Vector2 hip = frame.Pixel(hipIdx);
            Vector2 knee = frame.Pixel(kneeIdx);
            Vector2 ankle = frame.Pixel(ankleIdx);
            Vector2 heel = frame.Pixel(heelIdx);
            Vector2 toe = frame.Pixel(toeIdx);

            float thighLen = Vector2.Distance(hip, knee) + 1e-6f;
            float shinLen = Vector2.Distance(knee, ankle) + 1e-6f;
            float footLen = Vector2.Distance(heel, toe) + 1e-6f;

            float kneeAngle = Geometry.AngleAt(hip, knee, ankle);
            float hipAngle = Geometry.AngleAt(shoulder, hip, knee);
            float torsoLean = Geometry.AngleFromVerticalDeg(shoulder - hip);

            float depthRatio = (hip.y - knee.y) / thighLen;

            // If the foot is badly foreshortened in the image (camera is
            // pointed at the user from too sharp an angle, or the user is
            // almost facing the camera), `forwardSign` becomes unreliable
            // and `kneePast` starts reporting huge values for normal squats.
            // Fall back to NaN so the downstream form check skips this frame.
            float kneePast;
            float footProjRatio = footLen / shinLen;
            if (footProjRatio < 0.18f)
            {
                kneePast = float.NaN;
            }
            else
            {
                float forwardSign = (toe.x - heel.x) >= 0f ? 1f : -1f;
                kneePast = ((knee.x - toe.x) * forwardSign) / shinLen;
            }

            float heelLift = float.NaN;
            if (_heelYBaseline.HasValue && _footLenBaseline.HasValue && _footLenBaseline.Value > 0f)
            {
                heelLift = (_heelYBaseline.Value - heel.y) / _footLenBaseline.Value;
            }

            metrics = new FrameMetrics
            {
                KneeAngleDeg = kneeAngle,
                HipAngleDeg = hipAngle,
                TorsoLeanDeg = torsoLean,
                DepthRatio = depthRatio,
                KneePastToeRatio = kneePast,
                HeelLiftRatio = heelLift,
                VisibilityOk = true,
                Facing = side,
            };

            if (CurrentPhase == Phase.Standing)
            {
                Enqueue(_heelYSamples, heel.y, HeelBaselineWindow);
                Enqueue(_footLenSamples, footLen, HeelBaselineWindow);
                if (_heelYSamples.Count >= 3)
                {
                    _heelYBaseline = MedianOfQueue(_heelYSamples);
                    _footLenBaseline = MedianOfQueue(_footLenSamples);
                }
            }
        }

        // --- state machine ------------------------------------------------

        private RepRecord AdvanceStateMachine(FrameMetrics metrics, float nowS)
        {
            var preset = SensitivityPreset.All[Sensitivity];

            Enqueue(_angleBuf, metrics.KneeAngleDeg, Mathf.Max(1, SmoothingWindow));
            float knee = AvgQueue(_angleBuf);

            Enqueue(_angleTrendBuf, knee, 5);

            if (CurrentPhase == Phase.Descending || CurrentPhase == Phase.Ascending)
            {
                if (knee < _repMinAngle) _repMinAngle = knee;
                if (knee > _repMaxAngle) _repMaxAngle = knee;
            }

            switch (CurrentPhase)
            {
                case Phase.Standing:
                {
                    if (knee < preset.MidpointAngleDeg)
                    {
                        CurrentPhase = Phase.Descending;
                        _repTStart = nowS;
                        _repTBottom = null;
                        _repMinAngle = knee;
                        _repMaxAngle = knee;
                        _repIssueFrames.Clear();
                    }
                    return null;
                }
                case Phase.Descending:
                {
                    if (_angleTrendBuf.Count >= 3 && knee >= _repMinAngle + 2f)
                    {
                        float[] last3 = LastThree(_angleTrendBuf);
                        if (last3[0] < last3[1] && last3[1] < last3[2])
                        {
                            CurrentPhase = Phase.Ascending;
                            _repTBottom = nowS;
                        }
                    }
                    return null;
                }
                case Phase.Ascending:
                {
                    if (knee >= preset.TopAngleDeg)
                    {
                        float dur = _repTStart.HasValue ? (nowS - _repTStart.Value) : 0f;
                        if (dur < MinRealRepDurationS)
                        {
                            CurrentPhase = Phase.Standing;
                            _repTStart = null; _repTBottom = null;
                            _repIssueFrames.Clear();
                            return null;
                        }
                        var rep = FinaliseRep(nowS);
                        CurrentPhase = Phase.Standing;
                        _repTStart = null; _repTBottom = null;
                        return rep;
                    }
                    return null;
                }
                default:
                    CurrentPhase = Phase.Standing;
                    return null;
            }
        }

        private RepRecord FinaliseRep(float nowS)
        {
            var preset = SensitivityPreset.All[Sensitivity];
            float tStart = _repTStart ?? nowS;
            float tBottom = _repTBottom ?? nowS;
            float duration = nowS - tStart;
            float ecc = Mathf.Max(0f, tBottom - tStart);
            float con = Mathf.Max(0f, nowS - tBottom);

            var issues = new List<string>();
            float depthTargetAngle = DepthTargets.AngleFor(Depth);
            string depthLabel = ClassifyDepth(_repMinAngle);
            if (_repMinAngle > depthTargetAngle) issues.Add("depth_shallow");
            if (duration < preset.MinRepDurationS) issues.Add("rushed");
            foreach (var key in _inRepIssueKeys)
            {
                _repIssueFrames.TryGetValue(key, out int frames);
                if (frames >= preset.ConsecutiveBadFrames) issues.Add(key);
            }

            var record = new RepRecord
            {
                RepIndex = _currentSetReps.Count + 1,
                MinKneeAngleDeg = _repMinAngle,
                MaxKneeAngleDeg = _repMaxAngle,
                DurationS = duration,
                EccentricS = ecc,
                ConcentricS = con,
                Issues = issues,
                IsGood = issues.Count == 0,
                DepthReached = depthLabel,
            };
            _currentSetReps.Add(record);
            _tLastRep = nowS;
            return record;
        }

        private static string ClassifyDepth(float minKneeAngle)
        {
            if (minKneeAngle <= 70f) return "atg";
            if (minKneeAngle <= 100f) return "parallel";
            if (minKneeAngle <= 120f) return "half";
            return "shallow";
        }

        // --- in-rep issues ------------------------------------------------

        private List<string> CollectInRepIssues(FrameMetrics m)
        {
            var list = new List<string>();
            if (CurrentPhase != Phase.Descending && CurrentPhase != Phase.Ascending)
                return list;
            var p = SensitivityPreset.All[Sensitivity];
            if (!float.IsNaN(m.TorsoLeanDeg) && m.TorsoLeanDeg > p.MaxTorsoLeanDeg)
                list.Add("lean_forward");
            if (!float.IsNaN(m.KneePastToeRatio) && m.KneePastToeRatio > p.KneePastToeRatio)
                list.Add("knees_forward");
            if (!float.IsNaN(m.HeelLiftRatio) && m.HeelLiftRatio > p.HeelLiftRatio)
                list.Add("heel_lift");
            return list;
        }

        // --- set tracking -------------------------------------------------

        private SetRecord MaybeCloseSet(float nowS)
        {
            if (CurrentPhase != Phase.Standing) return null;
            if (_currentSetReps.Count == 0) return null;
            if (!_tLastRep.HasValue) return null;
            if ((nowS - _tLastRep.Value) >= SetEndIdleS) return CloseSetIfNonEmpty();
            return null;
        }

        private SetRecord CloseSetIfNonEmpty()
        {
            if (_currentSetReps.Count == 0) return null;
            var r = new SetRecord
            {
                SetIndex = SetIndex,
                Reps = new List<RepRecord>(_currentSetReps),
            };
            SetIndex += 1;
            _currentSetReps.Clear();
            _tLastRep = null;
            return r;
        }

        private void ResetRepState()
        {
            _repMinAngle = float.PositiveInfinity;
            _repMaxAngle = float.NegativeInfinity;
            _repTStart = null;
            _repTBottom = null;
            _repIssueFrames.Clear();
            CurrentPhase = Phase.Standing;
            _angleTrendBuf.Clear();
        }

        // --- tiny queue helpers ------------------------------------------

        private static void Enqueue(Queue<float> q, float v, int maxSize)
        {
            q.Enqueue(v);
            while (q.Count > maxSize) q.Dequeue();
        }

        private static float AvgQueue(Queue<float> q)
        {
            if (q.Count == 0) return 0f;
            float s = 0f;
            foreach (var v in q) s += v;
            return s / q.Count;
        }

        private static float MedianOfQueue(Queue<float> q)
        {
            float[] arr = q.ToArray();
            return Geometry.Median(arr);
        }

        private static float[] LastThree(Queue<float> q)
        {
            float[] arr = q.ToArray();
            int n = arr.Length;
            return new[] { arr[n - 3], arr[n - 2], arr[n - 1] };
        }
    }
}
