using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using SquatCoach.Analysis;
using SquatCoach.Coaching;

namespace SquatCoach.UI
{
    /// <summary>
    /// Translucent, two-column HUD. Intentionally exercise-agnostic — no
    /// exercise name, no exercise-specific jargon on screen.
    ///
    ///   ┌─────────────────────────────────────────┐
    ///   │  REPS   03      │                       │
    ///   │  SET    1       │                       │
    ///   │                 │   [side-view          │
    ///   │  TOTAL  12      │    mannequin]         │
    ///   │                 │                       │
    ///   │  Connected      │                       │
    ///   │                 │                       │
    ///   │  "chest up"     │                       │
    ///   └─────────────────────────────────────────┘
    ///
    /// The root GameObject should have an Image component; this script can
    /// set its alpha from the Inspector so the panel is a "translucent
    /// window" as a single exposed knob.
    /// </summary>
    public class HudPanel : MonoBehaviour
    {
        [Header("Translucent background (optional)")]
        [Tooltip("If set, its alpha will be forced to `backgroundAlpha` at Awake.")]
        public Image backgroundImage;
        [Range(0f, 1f)] public float backgroundAlpha = 0.35f;

        [Header("Left column — counts")]
        public TMP_Text repsValueText;     // e.g. "03"
        public TMP_Text setValueText;      // e.g. "1"
        public TMP_Text totalValueText;    // e.g. "12"

        [Header("Left column — status")]
        public TMP_Text connectionText;    // "Connected" | "Reconnecting…"
        public TMP_Text statusText;        // e.g. "Step into frame"
        public TMP_Text cueText;           // the active correction caption
        public GameObject mutedBadge;

        [Header("Right column — mannequin")]
        public MannequinRenderer mannequin;

        [Header("Colors")]
        public Color valueColor = new Color(1f, 1f, 1f, 0.95f);
        public Color labelColor = new Color(0.80f, 0.82f, 0.86f, 0.80f);
        public Color okColor = new Color(0.60f, 0.85f, 0.55f);
        public Color warnColor = new Color(1.00f, 0.80f, 0.20f);
        public Color badColor = new Color(0.95f, 0.30f, 0.25f);

        [Header("Cue caption")]
        [Tooltip("Minimum time (s) a cue stays visible before it can be replaced. Keeps the text in sync with voice and prevents flicker when multiple issues fire on the same frame.")]
        [Range(0f, 2f)] public float cueMinHoldS = 0.6f;

        // --- sticky cue state ---
        private string _displayedCueKey;
        private float _displayedCueSetAt = float.NegativeInfinity;

        private void Awake()
        {
            ApplyTranslucency();
        }

        private void OnValidate()
        {
            // Live-preview the alpha tweak in the Editor.
            if (Application.isPlaying) return;
            ApplyTranslucency();
        }

        private void ApplyTranslucency()
        {
            if (backgroundImage == null) return;
            var c = backgroundImage.color;
            c.a = backgroundAlpha;
            backgroundImage.color = c;
        }

        /// <summary>
        /// Called every frame by AppController with the latest snapshot.
        /// Kept explicit (no Update polling) so it's obvious where state flows.
        /// </summary>
        public void Render(
            int setIdx, int repsInSet, int totalReps,
            SquatAnalyzer.Phase phase,
            FrameMetrics metrics,
            List<string> activeIssues,
            string status,
            string connectionLabel,
            bool connected,
            bool muted,
            PoseFrame? poseSnapshot,
            string facing)
        {
            if (repsValueText != null) repsValueText.text = repsInSet.ToString("D2");
            if (setValueText != null) setValueText.text = setIdx.ToString();
            if (totalValueText != null) totalValueText.text = totalReps.ToString();

            if (connectionText != null)
            {
                connectionText.text = connectionLabel ?? "";
                connectionText.color = connected ? okColor : warnColor;
            }

            if (statusText != null)
            {
                statusText.text = status ?? "";
                statusText.color = string.IsNullOrEmpty(status) ? labelColor : warnColor;
            }

            if (cueText != null)
            {
                string topKey = IssueMessages.PickTopPriority(activeIssues);
                string nextKey = ResolveStickyCue(topKey);
                if (!string.IsNullOrEmpty(nextKey))
                {
                    cueText.color = badColor;
                    cueText.text = HumanizeCue(nextKey);
                }
                else
                {
                    cueText.text = "";
                }
            }

            if (mutedBadge != null) mutedBadge.SetActive(muted);

            if (mannequin != null)
            {
                if (poseSnapshot.HasValue && poseSnapshot.Value.IsValid)
                    mannequin.SetPose(poseSnapshot.Value, activeIssues, facing);
                else
                    mannequin.ClearPose();
            }
        }

        /// <summary>
        /// Keep the caption visible for at least <see cref="cueMinHoldS"/>
        /// once we've shown it — that's what makes the text line up with
        /// the voice clip and prevents "Chest up → Hips back → Heels down"
        /// strobing on a single frame. Returns the key that should actually
        /// be rendered this frame.
        /// </summary>
        private string ResolveStickyCue(string topKey)
        {
            float now = Time.unscaledTime;

            if (!string.IsNullOrEmpty(topKey))
            {
                // Same cue still relevant → extend its display.
                if (topKey == _displayedCueKey)
                {
                    _displayedCueSetAt = now;
                    return _displayedCueKey;
                }
                // Different cue requested. Only allow a swap once the current
                // caption has been on screen long enough for the user to read.
                bool canReplace = string.IsNullOrEmpty(_displayedCueKey) ||
                                  (now - _displayedCueSetAt) >= cueMinHoldS;
                if (canReplace)
                {
                    _displayedCueKey = topKey;
                    _displayedCueSetAt = now;
                }
                return _displayedCueKey;
            }

            // No active issue. Keep the last caption visible for its hold
            // window, then clear it.
            if (!string.IsNullOrEmpty(_displayedCueKey) &&
                (now - _displayedCueSetAt) < cueMinHoldS)
            {
                return _displayedCueKey;
            }
            _displayedCueKey = null;
            return null;
        }

        // Map internal issue keys to short human strings. Kept separate from
        // the spoken copy so the caption can be terser than the voice line.
        private static string HumanizeCue(string key) => key switch
        {
            "lean_forward"  => "Chest up",
            "knees_forward" => "Hips back",
            "heel_lift"     => "Heels down",
            "depth_shallow" => "Go deeper",
            "rushed"        => "Slow down",
            "partial_rep"   => "Full range",
            _               => key,
        };
    }
}
