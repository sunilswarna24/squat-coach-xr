using System;
using System.Collections.Generic;
using SquatCoach.Analysis;

namespace SquatCoach.Session
{
    /// <summary>
    /// Top-level JSON-serialisable record for one workout session. Matches
    /// the Python `session_logger.py` schema conceptually — field names are
    /// different because Unity's JsonUtility uses field names verbatim.
    /// </summary>
    [Serializable]
    public class SessionRecord
    {
        public string sessionId;
        public string startedAtIso;
        public string endedAtIso;
        public string sensitivity;
        public string depthTarget;
        public string facing;
        public List<SetRecord> sets = new List<SetRecord>();
        public SessionSummary summary = new SessionSummary();
    }

    [Serializable]
    public class SessionSummary
    {
        public int totalSets;
        public int totalReps;
        public int goodReps;
        public float avgMinKneeAngleDeg;
        public float avgDurationS;
        public float avgEccentricS;
        public float avgConcentricS;
        public Dictionary<string, int> issueHistogram = new Dictionary<string, int>();
    }
}
