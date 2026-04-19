using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using SquatCoach.Analysis;

namespace SquatCoach.Networking
{
    /// <summary>
    /// Decoders for the wire protocol. The single source of truth is
    /// protocol/schema.json in the repo root. Matching Python encoder is
    /// pi/src/protocol.py. If you change one, change the others.
    ///
    /// We avoid defining one POCO per message and instead parse a JObject
    /// once, then branch on `type`. This keeps allocations low on the
    /// 30 Hz hot path.
    /// </summary>
    public static class WireMessages
    {
        public const int ProtocolVersion = 1;

        public enum Kind { Unknown, Hello, Pose, NoPose, Bye }

        public readonly struct HelloInfo
        {
            public readonly string Model;
            public readonly string Delegate;
            public readonly int ImageW, ImageH;
            public readonly int TargetFps;
            public HelloInfo(string model, string dele, int w, int h, int fps)
            {
                Model = model; Delegate = dele; ImageW = w; ImageH = h; TargetFps = fps;
            }
        }

        /// <summary>Parse one text frame. Returns Kind.Unknown if unparseable.</summary>
        public static Kind ParseKind(string text, out JObject root)
        {
            root = null;
            if (string.IsNullOrEmpty(text)) return Kind.Unknown;
            try
            {
                root = JObject.Parse(text);
            }
            catch (JsonException)
            {
                return Kind.Unknown;
            }

            if ((int?)root["v"] != ProtocolVersion) return Kind.Unknown;
            string type = (string)root["type"];
            return type switch
            {
                "hello" => Kind.Hello,
                "pose" => Kind.Pose,
                "nopose" => Kind.NoPose,
                "bye" => Kind.Bye,
                _ => Kind.Unknown,
            };
        }

        public static HelloInfo ParseHello(JObject root)
        {
            return new HelloInfo(
                (string)root["model"] ?? "",
                (string)root["delegate"] ?? "cpu",
                (int?)root["image_w"] ?? 0,
                (int?)root["image_h"] ?? 0,
                (int?)root["target_fps"] ?? 30
            );
        }

        /// <summary>
        /// Parse a pose message into a PoseFrame. Returns true on success.
        /// Reuses the caller-provided buffers so we don't churn GC at 30 Hz.
        /// </summary>
        public static bool ParsePose(
            JObject root, Vector3[] points, float[] vis,
            out int seq, out long tsMs, out int w, out int h)
        {
            seq = (int?)root["seq"] ?? 0;
            tsMs = (long?)root["ts_ms"] ?? 0L;
            w = (int?)root["w"] ?? 0;
            h = (int?)root["h"] ?? 0;

            var lms = root["landmarks"] as JArray;
            if (lms == null || lms.Count != LM.Count) return false;
            if (points == null || points.Length != LM.Count) return false;
            if (vis == null || vis.Length != LM.Count) return false;

            for (int i = 0; i < LM.Count; i++)
            {
                var o = lms[i];
                points[i] = new Vector3(
                    (float)o["x"], (float)o["y"], (float)o["z"]);
                vis[i] = (float)o["v"];
            }
            return true;
        }

        // --- outbound control (optional, reserved for future use) ----------

        public static string ControlPause() =>
            JsonConvert.SerializeObject(new { v = ProtocolVersion, type = "control", action = "pause" });

        public static string ControlResume() =>
            JsonConvert.SerializeObject(new { v = ProtocolVersion, type = "control", action = "resume" });

        public static string ControlSetFps(int fps) =>
            JsonConvert.SerializeObject(new { v = ProtocolVersion, type = "control", action = "set_fps", fps });
    }
}
