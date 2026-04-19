using System;
using System.Collections.Generic;

namespace SquatCoach.Analysis
{
    /// <summary>One completed squat rep.</summary>
    [Serializable]
    public class RepRecord
    {
        public int RepIndex;
        public float MinKneeAngleDeg;
        public float MaxKneeAngleDeg;
        public float DurationS;
        public float EccentricS;
        public float ConcentricS;
        public List<string> Issues = new List<string>();
        public bool IsGood;
        public string DepthReached;   // "shallow" | "half" | "parallel" | "atg"
    }

    /// <summary>One completed set of reps.</summary>
    [Serializable]
    public class SetRecord
    {
        public int SetIndex;
        public List<RepRecord> Reps = new List<RepRecord>();
        public int RepCount => Reps.Count;
        public int GoodCount
        {
            get
            {
                int n = 0;
                foreach (var r in Reps) if (r.IsGood) n++;
                return n;
            }
        }
    }
}
