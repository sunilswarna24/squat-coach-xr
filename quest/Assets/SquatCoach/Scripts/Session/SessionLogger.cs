using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;
using SquatCoach.Analysis;

namespace SquatCoach.Session
{
    /// <summary>
    /// Accumulates sets during a session and persists them to a JSON file
    /// under Application.persistentDataPath. Designed so a future version
    /// can POST the same record to the Pi / a cloud endpoint simply by
    /// swapping the `Save` implementation.
    /// </summary>
    public class SessionLogger
    {
        private readonly SessionRecord _record = new SessionRecord();
        private readonly string _filePath;

        public SessionLogger(string sensitivity, string depthTarget, string facing)
        {
            _record.sessionId = Guid.NewGuid().ToString("N");
            _record.startedAtIso = DateTime.UtcNow.ToString("o");
            _record.sensitivity = sensitivity;
            _record.depthTarget = depthTarget;
            _record.facing = facing;
            _filePath = Path.Combine(
                Application.persistentDataPath,
                $"session_{_record.sessionId}.json");
        }

        public string FilePath => _filePath;

        public void AddSet(SetRecord s) => _record.sets.Add(s);

        /// <summary>Compute summary statistics and write the file.</summary>
        public string Save()
        {
            _record.endedAtIso = DateTime.UtcNow.ToString("o");
            ComputeSummary();
            if (_record.summary.totalReps == 0)
            {
                Debug.Log("[Session] No reps completed; skipping save.");
                return null;
            }
            string json = JsonConvert.SerializeObject(_record, Formatting.Indented);
            File.WriteAllText(_filePath, json);
            Debug.Log($"[Session] Saved to {_filePath}");
            return _filePath;
        }

        private void ComputeSummary()
        {
            var summary = _record.summary;
            summary.totalSets = _record.sets.Count;
            int reps = 0, good = 0;
            float sumMinKnee = 0f, sumDuration = 0f, sumEcc = 0f, sumCon = 0f;
            var hist = new Dictionary<string, int>();

            foreach (var s in _record.sets)
            {
                foreach (var r in s.Reps)
                {
                    reps++;
                    if (r.IsGood) good++;
                    sumMinKnee += r.MinKneeAngleDeg;
                    sumDuration += r.DurationS;
                    sumEcc += r.EccentricS;
                    sumCon += r.ConcentricS;
                    foreach (var i in r.Issues)
                    {
                        hist.TryGetValue(i, out int c);
                        hist[i] = c + 1;
                    }
                }
            }

            summary.totalReps = reps;
            summary.goodReps = good;
            if (reps > 0)
            {
                summary.avgMinKneeAngleDeg = sumMinKnee / reps;
                summary.avgDurationS = sumDuration / reps;
                summary.avgEccentricS = sumEcc / reps;
                summary.avgConcentricS = sumCon / reps;
            }
            summary.issueHistogram = hist;
        }
    }
}
