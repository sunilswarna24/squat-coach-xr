using System;
using System.Collections.Generic;
using UnityEngine;
using SquatCoach.Analysis;

namespace SquatCoach.UI
{
    /// <summary>
    /// Renders a side-view mannequin from the last pose frame and
    /// highlights body parts that are currently flagged by the analyzer.
    ///
    /// Design:
    ///  - The underlying MannequinGraphic is a single UI Graphic with an
    ///    immediate-mode style API (BeginFrame / AddSegment / ... / EndFrame).
    ///  - Every LateUpdate we rebuild the mesh from the latest snapshot.
    ///  - The input landmark array is COPIED when SetPose is called, because
    ///    the WebSocket client reuses the same buffer frame-to-frame.
    ///  - We auto-fit the skeleton into the graphic's rect with a little
    ///    padding so the mannequin looks right no matter how far the user
    ///    is from the camera.
    /// </summary>
    public class MannequinRenderer : MonoBehaviour
    {
        [Header("Wiring")]
        public MannequinGraphic graphic;

        [Header("Style")]
        [Tooltip("Bone thickness in graphic-local units.")]
        public float boneThickness = 6f;
        [Tooltip("Joint radius in graphic-local units.")]
        public float jointRadius = 7f;
        [Tooltip("Correction arrow thickness.")]
        public float arrowThickness = 4f;
        [Tooltip("Correction arrow head size.")]
        public float arrowHeadSize = 14f;
        [Tooltip("Fraction of rect to leave as padding around the mannequin on each side.")]
        [Range(0f, 0.3f)] public float padding = 0.08f;
        [Tooltip("Minimum landmark visibility to include in the drawing.")]
        [Range(0f, 1f)] public float minVisibility = 0.3f;

        [Header("Colors")]
        public Color boneColor = new Color(0.90f, 0.92f, 0.96f, 1f);
        public Color jointColor = new Color(0.85f, 0.88f, 0.95f, 1f);
        public Color ghostColor = new Color(0.70f, 0.72f, 0.78f, 0.55f);
        public Color issueColor = new Color(0.95f, 0.25f, 0.20f, 1f);
        public Color arrowColor = new Color(1.00f, 0.80f, 0.20f, 1f);

        // --- snapshot held by the component -----------------------------
        private readonly Vector3[] _points = new Vector3[LM.Count];
        private readonly float[] _vis = new float[LM.Count];
        private readonly HashSet<string> _issues = new HashSet<string>();
        private string _facing = "right";
        private bool _hasPose;
        private float _lastPoseTime;

        // Fade the mannequin out if we haven't seen a pose recently.
        public float posTimeoutS = 1.0f;

        // --- public API --------------------------------------------------

        /// <summary>Accept the latest pose. Safe to call at any rate; copies the buffers.</summary>
        public void SetPose(PoseFrame frame, IList<string> activeIssues, string facing)
        {
            if (!frame.IsValid) return;
            Array.Copy(frame.Points, _points, LM.Count);
            if (frame.Vis != null) Array.Copy(frame.Vis, _vis, LM.Count);
            _issues.Clear();
            if (activeIssues != null)
            {
                for (int i = 0; i < activeIssues.Count; i++) _issues.Add(activeIssues[i]);
            }
            _facing = facing;
            _hasPose = true;
            _lastPoseTime = Time.realtimeSinceStartup;
        }

        public void ClearPose()
        {
            _hasPose = false;
            _issues.Clear();
        }

        // --- draw loop ---------------------------------------------------

        private void LateUpdate()
        {
            if (graphic == null) return;
            graphic.BeginFrame();

            if (!_hasPose)
            {
                DrawPlaceholder();
            }
            else
            {
                float age = Time.realtimeSinceStartup - _lastPoseTime;
                float fade = Mathf.Clamp01(1f - (age / Mathf.Max(0.1f, posTimeoutS)));
                DrawMannequin(fade);
            }

            graphic.EndFrame();
        }

        // --- placeholder (no pose) --------------------------------------

        private void DrawPlaceholder()
        {
            // A soft gray stick figure so the user knows the mannequin is alive.
            Rect r = graphic.rectTransform.rect;
            float cx = 0f;
            float top = r.height * 0.42f;
            float hipY = r.height * 0.0f;
            float kneeY = -r.height * 0.22f;
            float footY = -r.height * 0.42f;

            var c = ghostColor;
            graphic.AddSegment(new Vector2(cx, top), new Vector2(cx, hipY), boneThickness * 0.6f, c);
            graphic.AddSegment(new Vector2(cx, hipY), new Vector2(cx, kneeY), boneThickness * 0.6f, c);
            graphic.AddSegment(new Vector2(cx, kneeY), new Vector2(cx, footY), boneThickness * 0.6f, c);
            graphic.AddJoint(new Vector2(cx, top), jointRadius * 1.4f, c);
            graphic.AddJoint(new Vector2(cx, hipY), jointRadius, c);
            graphic.AddJoint(new Vector2(cx, kneeY), jointRadius, c);
            graphic.AddJoint(new Vector2(cx, footY), jointRadius, c);
        }

        // --- mannequin drawing -------------------------------------------

        private static readonly int[] LeftChain =
        {
            LM.LeftEar, LM.LeftShoulder, LM.LeftHip, LM.LeftKnee,
            LM.LeftAnkle, LM.LeftHeel, LM.LeftFootIndex,
        };

        private static readonly int[] RightChain =
        {
            LM.RightEar, LM.RightShoulder, LM.RightHip, LM.RightKnee,
            LM.RightAnkle, LM.RightHeel, LM.RightFootIndex,
        };

        private struct BodyPoints
        {
            public Vector2 Ear, Shoulder, Hip, Knee, Ankle, Heel, Toe;
        }

        private void DrawMannequin(float fade)
        {
            int[] chain = _facing == "left" ? LeftChain : RightChain;

            // Auto-fit: bounding box of (visible enough) chain landmarks.
            Vector2 minN = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
            Vector2 maxN = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
            int seen = 0;
            for (int i = 0; i < chain.Length; i++)
            {
                int idx = chain[i];
                if (_vis[idx] < minVisibility) continue;
                Vector2 n = new Vector2(_points[idx].x, _points[idx].y);
                if (n.x < minN.x) minN.x = n.x;
                if (n.y < minN.y) minN.y = n.y;
                if (n.x > maxN.x) maxN.x = n.x;
                if (n.y > maxN.y) maxN.y = n.y;
                seen++;
            }
            if (seen < 3)
            {
                DrawPlaceholder();
                return;
            }

            // Project each chain landmark into local rect space.
            BodyPoints bp = new BodyPoints
            {
                Ear      = MapToRect(_points[chain[0]], minN, maxN),
                Shoulder = MapToRect(_points[chain[1]], minN, maxN),
                Hip      = MapToRect(_points[chain[2]], minN, maxN),
                Knee     = MapToRect(_points[chain[3]], minN, maxN),
                Ankle    = MapToRect(_points[chain[4]], minN, maxN),
                Heel     = MapToRect(_points[chain[5]], minN, maxN),
                Toe      = MapToRect(_points[chain[6]], minN, maxN),
            };

            Color bone = ApplyFade(boneColor, fade);
            Color joint = ApplyFade(jointColor, fade);
            Color issue = ApplyFade(issueColor, fade);
            Color arrow = ApplyFade(arrowColor, fade);

            bool leanForward = _issues.Contains("lean_forward");
            bool kneesForward = _issues.Contains("knees_forward");
            bool heelLift = _issues.Contains("heel_lift");

            // Bones -------------------------------------------------------
            graphic.AddSegment(bp.Ear, bp.Shoulder, boneThickness, bone);
            graphic.AddSegment(bp.Shoulder, bp.Hip, boneThickness,
                leanForward ? issue : bone);
            graphic.AddSegment(bp.Hip, bp.Knee, boneThickness,
                kneesForward ? issue : bone);
            graphic.AddSegment(bp.Knee, bp.Ankle, boneThickness,
                kneesForward ? issue : bone);
            graphic.AddSegment(bp.Ankle, bp.Heel, boneThickness * 0.7f,
                heelLift ? issue : bone);
            graphic.AddSegment(bp.Ankle, bp.Toe, boneThickness * 0.7f, bone);
            graphic.AddSegment(bp.Heel, bp.Toe, boneThickness * 0.7f,
                heelLift ? issue : bone);

            // Joints ------------------------------------------------------
            // Head is a larger circle above the ear.
            Vector2 headCenter = bp.Ear + (bp.Ear - bp.Shoulder).normalized * jointRadius * 2.2f;
            float headR = Vector2.Distance(bp.Ear, bp.Shoulder) * 0.35f + jointRadius;
            graphic.AddJoint(headCenter, headR, bone);

            graphic.AddJoint(bp.Shoulder, jointRadius, leanForward ? issue : joint);
            graphic.AddJoint(bp.Hip, jointRadius, leanForward ? issue : joint);
            graphic.AddJoint(bp.Knee, jointRadius, kneesForward ? issue : joint);
            graphic.AddJoint(bp.Ankle, jointRadius, joint);
            graphic.AddJoint(bp.Heel, jointRadius * 0.8f, heelLift ? issue : joint);
            graphic.AddJoint(bp.Toe, jointRadius * 0.8f, joint);

            // Correction arrows -----------------------------------------
            // `lean_forward`: arrow above the shoulder pushing straight up.
            if (leanForward)
            {
                float len = Vector2.Distance(bp.Shoulder, bp.Hip) * 0.75f;
                Vector2 from = bp.Shoulder + new Vector2(0, jointRadius * 2f);
                Vector2 to = from + new Vector2(0, len);
                graphic.AddArrow(from, to, arrowThickness, arrowHeadSize, arrow);
            }
            // `knees_forward`: arrow from knee toward hip horizontally (push hips back).
            if (kneesForward)
            {
                Vector2 dir = (bp.Hip - bp.Knee);
                dir.y = 0f;
                if (dir.sqrMagnitude > 1e-4f)
                {
                    dir.Normalize();
                    float len = Vector2.Distance(bp.Hip, bp.Knee) * 0.6f;
                    Vector2 from = bp.Knee + new Vector2(0, jointRadius * 1.5f);
                    Vector2 to = from + dir * len;
                    graphic.AddArrow(from, to, arrowThickness, arrowHeadSize, arrow);
                }
            }
            // `heel_lift`: downward arrow at the heel.
            if (heelLift)
            {
                float len = Vector2.Distance(bp.Heel, bp.Toe) * 1.1f;
                Vector2 from = bp.Heel + new Vector2(0, len);
                Vector2 to = bp.Heel + new Vector2(0, jointRadius);
                graphic.AddArrow(from, to, arrowThickness, arrowHeadSize, arrow);
            }
        }

        // Map a normalized image coord (0..1, y down) into graphic-local
        // rect coords (pivot 0.5,0.5, y up), preserving the aspect of the
        // bounding box with some padding.
        private Vector2 MapToRect(Vector3 p, Vector2 minN, Vector2 maxN)
        {
            Rect r = graphic.rectTransform.rect;
            float pad = padding;
            float targetW = r.width * (1f - 2f * pad);
            float targetH = r.height * (1f - 2f * pad);

            float boxW = Mathf.Max(1e-4f, maxN.x - minN.x);
            float boxH = Mathf.Max(1e-4f, maxN.y - minN.y);

            // Fit the pose bounding box into the rect keeping its aspect.
            float scale = Mathf.Min(targetW / boxW, targetH / boxH);

            // Center the box in the rect.
            float cx = 0.5f * (minN.x + maxN.x);
            float cy = 0.5f * (minN.y + maxN.y);

            float localX = (p.x - cx) * scale;
            // Flip Y: image y grows down, UI rect y grows up.
            float localY = -(p.y - cy) * scale;
            return new Vector2(localX, localY);
        }

        private static Color ApplyFade(Color c, float fade)
        {
            c.a *= Mathf.Clamp01(fade);
            return c;
        }
    }
}
