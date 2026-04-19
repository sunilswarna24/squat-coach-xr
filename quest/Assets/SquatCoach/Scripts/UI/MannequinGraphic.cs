using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace SquatCoach.UI
{
    /// <summary>
    /// A single-draw-call Unity UI primitive that renders line segments,
    /// filled circles, and arrows in the graphic's local rect space.
    ///
    /// This exists because Unity's built-in UI has no line renderer that
    /// works inside a Canvas. We override OnPopulateMesh and emit quads
    /// for each segment, triangle fans for each joint dot, and a shaft +
    /// head triangle for each arrow.
    ///
    /// Coordinate space: positions are in local rect space; (0, 0) is the
    /// graphic's pivot. For pivot (0.5, 0.5) that's the center.
    /// Y grows up, matching Unity UI.
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public class MannequinGraphic : MaskableGraphic
    {
        public struct Segment
        {
            public Vector2 A, B;
            public float Thickness;
            public Color Color;
        }

        public struct Joint
        {
            public Vector2 Center;
            public float Radius;
            public Color Color;
        }

        public struct Arrow
        {
            public Vector2 From, To;
            public float Thickness;
            public float HeadSize;
            public Color Color;
        }

        private readonly List<Segment> _segments = new List<Segment>(24);
        private readonly List<Joint> _joints = new List<Joint>(16);
        private readonly List<Arrow> _arrows = new List<Arrow>(8);

        // --- public draw API ---------------------------------------------

        public void BeginFrame()
        {
            _segments.Clear();
            _joints.Clear();
            _arrows.Clear();
        }

        public void AddSegment(Vector2 a, Vector2 b, float thickness, Color color)
        {
            _segments.Add(new Segment { A = a, B = b, Thickness = thickness, Color = color });
        }

        public void AddJoint(Vector2 center, float radius, Color color)
        {
            _joints.Add(new Joint { Center = center, Radius = radius, Color = color });
        }

        public void AddArrow(Vector2 from, Vector2 to, float thickness, float headSize, Color color)
        {
            _arrows.Add(new Arrow { From = from, To = to, Thickness = thickness, HeadSize = headSize, Color = color });
        }

        public void EndFrame() => SetVerticesDirty();

        // --- mesh generation ---------------------------------------------

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            int idx = 0;
            // Draw bones first so joints sit on top.
            for (int i = 0; i < _segments.Count; i++)
            {
                var s = _segments[i];
                idx = AppendQuad(vh, s.A, s.B, s.Thickness, s.Color, idx);
            }
            for (int i = 0; i < _arrows.Count; i++)
            {
                var a = _arrows[i];
                idx = AppendArrow(vh, a.From, a.To, a.Thickness, a.HeadSize, a.Color, idx);
            }
            for (int i = 0; i < _joints.Count; i++)
            {
                var j = _joints[i];
                idx = AppendCircle(vh, j.Center, j.Radius, j.Color, idx);
            }
        }

        // --- helpers ------------------------------------------------------

        private static int AppendQuad(
            VertexHelper vh, Vector2 a, Vector2 b, float thickness, Color color, int idx)
        {
            Vector2 d = b - a;
            if (d.sqrMagnitude < 1e-6f) return idx;
            d.Normalize();
            Vector2 perp = new Vector2(-d.y, d.x) * (thickness * 0.5f);

            AppendVertex(vh, a + perp, color);
            AppendVertex(vh, b + perp, color);
            AppendVertex(vh, b - perp, color);
            AppendVertex(vh, a - perp, color);

            vh.AddTriangle(idx + 0, idx + 1, idx + 2);
            vh.AddTriangle(idx + 0, idx + 2, idx + 3);
            return idx + 4;
        }

        private static int AppendCircle(
            VertexHelper vh, Vector2 center, float radius, Color color, int idx)
        {
            const int sides = 16;
            int centerIdx = idx;
            AppendVertex(vh, center, color);
            for (int i = 0; i <= sides; i++)
            {
                float t = (i / (float)sides) * Mathf.PI * 2f;
                Vector2 p = center + new Vector2(Mathf.Cos(t), Mathf.Sin(t)) * radius;
                AppendVertex(vh, p, color);
                if (i > 0) vh.AddTriangle(centerIdx, idx + i, idx + i + 1);
            }
            return idx + sides + 2;
        }

        private static int AppendArrow(
            VertexHelper vh, Vector2 from, Vector2 to,
            float thickness, float headSize, Color color, int idx)
        {
            Vector2 d = to - from;
            if (d.sqrMagnitude < 1e-6f) return idx;
            d.Normalize();
            // Shorten the shaft to leave room for the arrowhead.
            Vector2 shaftEnd = to - d * (headSize * 0.6f);
            idx = AppendQuad(vh, from, shaftEnd, thickness, color, idx);

            Vector2 perp = new Vector2(-d.y, d.x) * (headSize * 0.5f);
            AppendVertex(vh, to, color);
            AppendVertex(vh, shaftEnd + perp, color);
            AppendVertex(vh, shaftEnd - perp, color);
            vh.AddTriangle(idx + 0, idx + 1, idx + 2);
            return idx + 3;
        }

        private static void AppendVertex(VertexHelper vh, Vector2 pos, Color color)
        {
            UIVertex v = UIVertex.simpleVert;
            v.position = new Vector3(pos.x, pos.y, 0f);
            v.color = color;
            vh.AddVert(v);
        }
    }
}
