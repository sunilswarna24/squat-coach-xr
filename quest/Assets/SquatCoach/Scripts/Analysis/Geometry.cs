using System;
using UnityEngine;

namespace SquatCoach.Analysis
{
    /// <summary>
    /// Geometry helpers used by the analyzer. All operate in 2D pixel space
    /// (the side-view camera). Mirrors pi-side Python helpers.
    /// </summary>
    public static class Geometry
    {
        /// <summary>Angle in degrees at vertex b between vectors b->a and b->c.</summary>
        public static float AngleAt(Vector2 a, Vector2 b, Vector2 c)
        {
            Vector2 ba = a - b;
            Vector2 bc = c - b;
            float denom = (ba.magnitude * bc.magnitude) + 1e-6f;
            float cos = Mathf.Clamp(Vector2.Dot(ba, bc) / denom, -1f, 1f);
            return Mathf.Acos(cos) * Mathf.Rad2Deg;
        }

        /// <summary>
        /// Angle (0..180°) of a 2D vector from image "up" (0, -1).
        /// Image y grows downward; this is intentional.
        /// </summary>
        public static float AngleFromVerticalDeg(Vector2 vec)
        {
            Vector2 up = new Vector2(0f, -1f);
            float denom = vec.magnitude + 1e-6f;
            float cos = Mathf.Clamp(Vector2.Dot(vec, up) / denom, -1f, 1f);
            return Mathf.Acos(cos) * Mathf.Rad2Deg;
        }

        /// <summary>Running median over a fixed-capacity circular buffer.</summary>
        public static float Median(ReadOnlySpan<float> values)
        {
            if (values.Length == 0) return 0f;
            Span<float> copy = stackalloc float[values.Length];
            values.CopyTo(copy);
            copy.Sort();
            int n = copy.Length;
            return (n % 2 == 1) ? copy[n / 2] : 0.5f * (copy[n / 2 - 1] + copy[n / 2]);
        }
    }
}
