using System.Collections.Generic;

namespace SquatCoach.Analysis
{
    /// <summary>
    /// Tunable thresholds for a "sensitivity" level. Values mirror the
    /// Python config on the Windows PC prototype (loosened, post-tuning).
    /// </summary>
    public readonly struct SensitivityPreset
    {
        public readonly float TopAngleDeg;
        public readonly float MidpointAngleDeg;
        public readonly float MaxTorsoLeanDeg;
        public readonly float KneePastToeRatio;
        public readonly float HeelLiftRatio;
        public readonly float MinRepDurationS;
        public readonly int ConsecutiveBadFrames;
        public readonly float VoiceCooldownS;

        public SensitivityPreset(
            float topAngleDeg, float midpointAngleDeg, float maxTorsoLeanDeg,
            float kneePastToeRatio, float heelLiftRatio, float minRepDurationS,
            int consecutiveBadFrames, float voiceCooldownS)
        {
            TopAngleDeg = topAngleDeg;
            MidpointAngleDeg = midpointAngleDeg;
            MaxTorsoLeanDeg = maxTorsoLeanDeg;
            KneePastToeRatio = kneePastToeRatio;
            HeelLiftRatio = heelLiftRatio;
            MinRepDurationS = minRepDurationS;
            ConsecutiveBadFrames = consecutiveBadFrames;
            VoiceCooldownS = voiceCooldownS;
        }

        public static readonly IReadOnlyDictionary<string, SensitivityPreset> All =
            new Dictionary<string, SensitivityPreset>
            {
                // KneePastToeRatio is sensitive to camera-axis error and
                // user-to-camera distance; values are looser than the Python
                // original after field-testing on the Quest side stream.
                ["low"]    = new SensitivityPreset(165f, 145f, 80f, 0.80f, 0.40f, 1.0f, 12, 6.0f),
                ["medium"] = new SensitivityPreset(160f, 140f, 65f, 0.60f, 0.30f, 1.2f, 8,  4.0f),
                ["high"]   = new SensitivityPreset(155f, 135f, 55f, 0.40f, 0.20f, 1.5f, 5,  2.5f),
            };
    }

    public enum DepthTarget { Half, Parallel, Atg }

    public static class DepthTargets
    {
        public static float AngleFor(DepthTarget t) => t switch
        {
            DepthTarget.Half     => 120f,
            DepthTarget.Parallel => 95f,
            DepthTarget.Atg      => 75f,
            _                    => 95f,
        };
    }
}
