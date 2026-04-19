namespace SquatCoach.Analysis
{
    /// <summary>
    /// Per-frame metrics emitted by SquatAnalyzer, mostly for the HUD.
    /// Mirrors the Python `FrameMetrics` dataclass.
    /// </summary>
    public struct FrameMetrics
    {
        public float KneeAngleDeg;
        public float HipAngleDeg;
        public float TorsoLeanDeg;
        public float DepthRatio;         // (hip_y - knee_y) / thigh_len
        public float KneePastToeRatio;   // signed, >0 means past toes
        public float HeelLiftRatio;      // >0 means heel above baseline
        public bool VisibilityOk;
        public string Facing;            // "left" or "right" (resolved)

        public static FrameMetrics Empty => new FrameMetrics
        {
            KneeAngleDeg = float.NaN,
            HipAngleDeg = float.NaN,
            TorsoLeanDeg = float.NaN,
            DepthRatio = float.NaN,
            KneePastToeRatio = float.NaN,
            HeelLiftRatio = float.NaN,
            VisibilityOk = false,
            Facing = "auto",
        };
    }
}
