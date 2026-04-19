using UnityEngine;

namespace SquatCoach.Analysis
{
    /// <summary>
    /// One pose detection, as received from the Pi and made analysis-ready.
    /// Landmark coordinates are normalized to the image (0..1), y grows
    /// downward — matching the MediaPipe convention.
    /// </summary>
    public struct PoseFrame
    {
        public int Seq;
        public long TsMs;          // Pi's monotonic clock, NOT wall time.
        public int ImageW;
        public int ImageH;
        public Vector3[] Points;   // length == LM.Count, xyz as received (z unused for now)
        public float[] Vis;        // length == LM.Count, [0..1]

        public bool IsValid => Points != null && Points.Length == LM.Count;

        public Vector2 Pixel(int idx)
        {
            return new Vector2(Points[idx].x * ImageW, Points[idx].y * ImageH);
        }

        public float Visibility(int idx) => Vis != null ? Vis[idx] : 0f;
    }
}
